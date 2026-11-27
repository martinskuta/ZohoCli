using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Zoho.Cli.Zoho;

namespace Zoho.Cli.Auth;

/// <summary>Zoho OAuth with PKCE and a public client ID: no client secret is kept anywhere.</summary>
internal sealed class OAuthClient(HttpClient http, ZohoSettings settings, TimeProvider time)
{
    private const string ClientId = "1000.YNFJBIDBFBB9OPHLI4EIHUQZACWG1Q";
    private const string ListenerPrefix = "http://localhost:8080/";
    private const string RedirectUri = ListenerPrefix + "callback";
    private const string Scopes = "aaaserver.profile.READ,ZOHOPEOPLE.leave.READ,ZOHOPEOPLE.timetracker.ALL,ZOHOPEOPLE.forms.READ";

    private string AccountsUrl => $"https://accounts.{settings.Domain}";

    public async Task<OAuthToken> LoginInBrowserAsync(CancellationToken ct)
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var query = string.Join("&", new Dictionary<string, string>
        {
            ["client_id"] = ClientId,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["prompt"] = "consent",
            ["redirect_uri"] = RedirectUri,
            ["access_type"] = "offline",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        }.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        using var listener = new HttpListener();
        listener.Prefixes.Add(ListenerPrefix);
        listener.Start();
        Process.Start(new ProcessStartInfo($"{AccountsUrl}/oauth/v2/auth?{query}") { UseShellExecute = true });

        var code = await WaitForCodeAsync(listener, ct);
        return await RequestTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["code_verifier"] = verifier,
            ["code"] = code,
            ["redirect_uri"] = RedirectUri
        }, ct);
    }

    public async Task<OAuthToken> RefreshAsync(OAuthToken token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new NotSignedInException("the saved token has no refresh token.");

        OAuthToken refreshed;
        try
        {
            refreshed = await RequestTokenAsync(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = ClientId,
                ["refresh_token"] = token.RefreshToken
            }, ct);
        }
        catch (ZohoException e)
        {
            throw new NotSignedInException($"Zoho refused to refresh the token ({e.Message}).");
        }

        refreshed.UserEmail = token.UserEmail;
        refreshed.UserId = token.UserId;
        if (string.IsNullOrWhiteSpace(refreshed.RefreshToken)) refreshed.RefreshToken = token.RefreshToken;
        return refreshed;
    }

    public async Task<string> GetUserEmailAsync(OAuthToken token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{AccountsUrl}/oauth/user/info");
        request.Headers.Add("Authorization", "Zoho-oauthtoken " + token.AccessToken);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new ZohoException($"Could not read the Zoho user: {(int)response.StatusCode} {body}");

        return JsonNode.Parse(body)?["Email"]?.GetValue<string>()
               ?? throw new ZohoException($"The Zoho user has no email: {body}");
    }

    private async Task<OAuthToken> RequestTokenAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await http.PostAsync($"{AccountsUrl}/oauth/v2/token", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        // Zoho answers a refused grant with 200 and an "error" field, not with an error status.
        var node = TryParse(body);
        var error = node?["error"]?.ToString();
        if (!response.IsSuccessStatusCode || error != null || node?["access_token"] == null)
            throw new ZohoException(error ?? $"{(int)response.StatusCode} {body}");

        var token = node.Deserialize(ZohoJson.Default.OAuthToken)!;
        token.ExpiresAt = time.GetUtcNow().AddSeconds(token.ExpiresIn - 15);
        return token;
    }

    private static async Task<string> WaitForCodeAsync(HttpListener listener, CancellationToken ct)
    {
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), ct);
        }
        catch (TimeoutException)
        {
            throw new ZohoException("No answer from the Zoho sign-in within 5 minutes.");
        }

        var code = context.Request.QueryString["code"];
        var error = context.Request.QueryString["error_description"] ?? context.Request.QueryString["error"];
        var message = code != null ? "Signed in to Zoho. You can close this window." : $"Sign-in failed: {error}";
        var page = Encoding.UTF8.GetBytes($"<!DOCTYPE html><html><body style=\"font-family:sans-serif\"><p>{WebUtility.HtmlEncode(message)}</p></body></html>");
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.StatusCode = code != null ? 200 : 400;
        await context.Response.OutputStream.WriteAsync(page, ct);
        context.Response.Close();

        return code ?? throw new ZohoException($"Zoho sign-in failed: {error ?? "no authorization code"}.");
    }

    private static JsonNode? TryParse(string body)
    {
        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
