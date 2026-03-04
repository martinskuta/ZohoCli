using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZohoCLI.Auth;

public class OAuthService
{
    private static readonly string ZohoAuthEndpoint = $"https://accounts.{ZohoEnv.Default.Domain}/oauth/v2";
    private const string RedirectUri = "http://localhost:8080/callback";

    private const string ClientId = "1000.YNFJBIDBFBB9OPHLI4EIHUQZACWG1Q";
    private readonly HttpClient _httpClient = new();
    private string? _codeVerifier;

    public async Task<OAuthToken> LoginAsync(CancellationToken cancellationToken = default)
    {
        // Generate authorization URL
        var authorizationCode = await GetAuthorizationCodeAsync(cancellationToken);

        // Exchange code for token
        var token = await ExchangeCodeForTokenAsync(authorizationCode, cancellationToken);

        return token;
    }

    private async Task<string> GetAuthorizationCodeAsync(CancellationToken cancellationToken)
    {
        // Launch browser
        LaunchBrowser(BuildAuthorizationUrl());

        // Start local callback server
        var code = await WaitForCallbackAsync(cancellationToken);

        return code;
    }

    private string BuildAuthorizationUrl()
    {
        var verifierBytes = new byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        _codeVerifier = Base64UrlEncode(verifierBytes);

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(_codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeBytes);

        var parameters = new Dictionary<string, string>
        {
            { "client_id", ClientId },
            { "response_type", "code" },
            {
                "scope", "aaaserver.profile.READ,ZOHOPEOPLE.leave.READ,ZOHOPEOPLE.timetracker.ALL,ZOHOPEOPLE.forms.READ"
            },
            { "redirect_uri", RedirectUri },
            { "access_type", "offline" },
            { "code_challenge", codeChallenge },
            { "code_challenge_method", "S256" }
        };

        var queryString = string.Join("&", parameters.Select(p =>
            $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return $"{ZohoAuthEndpoint}/auth?{queryString}";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private void LaunchBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = url,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = url,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch browser: {ex.Message}", ex);
        }
    }

    private async Task<string> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        using var httpListener = new HttpListener();
        httpListener.Prefixes.Add("http://localhost:8080/");
        httpListener.Start();

        try
        {
            Console.WriteLine("Waiting for authorization callback...");

            var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tokenSource.CancelAfter(TimeSpan.FromMinutes(5)); // 5-minute timeout

            var getContextTask = httpListener.GetContextAsync();
            var delayTask = Task.Delay(TimeSpan.FromMinutes(5), tokenSource.Token);
            var completedTask = await Task.WhenAny(getContextTask, delayTask).ConfigureAwait(false);

            if (completedTask == delayTask)
            {
                throw new InvalidOperationException("Authorization timeout: no response from Zoho within 5 minutes");
            }

            var context = await getContextTask.ConfigureAwait(false);
            var request = context.Request;

            // Extract authorization code from callback
            var code = request.QueryString["code"];
            var error = request.QueryString["error"];

            if (!string.IsNullOrEmpty(error))
            {
                var errorDescription = request.QueryString["error_description"] ?? error;
                SendResponseToClient(context, false, "Authorization failed: " + errorDescription);
                throw new InvalidOperationException($"Authorization error: {errorDescription}");
            }

            if (string.IsNullOrEmpty(code))
            {
                SendResponseToClient(context, false, "Authorization code not received");
                throw new InvalidOperationException("Authorization code not received from Zoho");
            }

            SendResponseToClient(context, true, "Authorization successful! You can close this window.");
            return code;
        }
        finally
        {
            httpListener.Stop();
            httpListener.Close();
        }
    }

    private void SendResponseToClient(HttpListenerContext context, bool success, string message)
    {
        var response = context.Response;
        response.ContentType = "text/html; charset=utf-8";
        response.StatusCode = success ? 200 : 400;

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>ZohoCLI Authorization</title>
    <style>
        body {{ font-family: sans-serif; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f5f5f5; }}
        .container {{ background: white; padding: 40px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); text-align: center; }}
        h1 {{ color: {(success ? "#28a745" : "#dc3545")}; margin-top: 0; }}
        p {{ color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>{(success ? "✓ Success" : "✗ Error")}</h1>
        <p>{message}</p>
    </div>
</body>
</html>";

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        using var output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
    }

    private async Task<OAuthToken> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken)
    {
        var tokenRequest = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "client_id", ClientId },
            {
                "code_verifier",
                _codeVerifier ??
                throw new InvalidOperationException(
                    "code_verifier not set — BuildAuthorizationUrl must be called first")
            },
            { "code", code },
            { "redirect_uri", RedirectUri }
        };

        using var content = new FormUrlEncodedContent(tokenRequest);
        var response = await _httpClient.PostAsync($"{ZohoAuthEndpoint}/token", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to exchange authorization code: {response.StatusCode} - {errorContent}");
        }

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var token = JsonSerializer.Deserialize<OAuthToken>(jsonContent, OAuthTokenJsonContext.Default.OAuthToken)
                    ?? throw new InvalidOperationException("Failed to deserialize token response");
        
        return token;
    }

    public async Task<OAuthToken> RefreshTokenAsync(OAuthToken token, CancellationToken cancellationToken = default)
    {
        var tokenRequest = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "client_id", ClientId },
            { "refresh_token", token.RefreshToken }
        };

        using var content = new FormUrlEncodedContent(tokenRequest);
        var response = await _httpClient.PostAsync($"{ZohoAuthEndpoint}/token", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to refresh token: {response.StatusCode} - {errorContent}");
        }

        await using var jsonContent = await response.Content.ReadAsStreamAsync(cancellationToken);
        var newToken = JsonSerializer.Deserialize<OAuthToken>(jsonContent, OAuthTokenJsonContext.Default.OAuthToken)
                       ?? throw new InvalidOperationException("Failed to deserialize token response");

        return newToken;
    }

    public async Task RevokeRefreshTokenAsync(OAuthToken token, CancellationToken cancellationToken = default)
    {
        var url = $"{ZohoAuthEndpoint}/token/revoke?token={Uri.EscapeDataString(token.RefreshToken)}";
        var response = await _httpClient.PostAsync(url, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to revoke token: {response.StatusCode} - {errorContent}");
        }
    }

    public async Task<UserInfo> GetUserInfoAsync(OAuthToken token, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ZohoAuthEndpoint}/user/info");
        request.Headers.Add("Authorization", "Zoho-oauthtoken " + token.AccessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Failed to get user info: {response.StatusCode} - {errorContent}");
        }

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var userInfo = JsonSerializer.Deserialize<UserInfo>(jsonContent, UserInfoJsonContext.Default.UserInfo)
                       ?? throw new InvalidOperationException("Failed to deserialize user info response");

        return userInfo;
    }
}

public class OAuthToken : IJsonOnDeserialized
{
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;

    [JsonPropertyName("user_id")] public long UserId { get; set; } = -1;

    [JsonPropertyName("access_token")] public required string AccessToken { get; set; }

    [JsonPropertyName("refresh_token")] public required string RefreshToken { get; set; }

    [JsonPropertyName("scope")] public required string Scope { get; set; }

    [JsonPropertyName("api_domain")] public required string ApiDomain { get; set; }

    [JsonPropertyName("token_type")] public required string TokenType { get; set; }

    [JsonPropertyName("expires_in")] public required int ExpiresIn { get; set; }

    /// <summary>
    /// In UTC time
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonIgnore] public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    void IJsonOnDeserialized.OnDeserialized()
        => ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(ExpiresIn - 15);
}

public class UserInfo
{
    [JsonPropertyName("Email")] public string Email { get; set; } = string.Empty;

    [JsonPropertyName("First_Name")] public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("Last_Name")] public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("ZUID")] public long ZohoUserId { get; set; } = -1;

    [JsonPropertyName("Display_Name")] public string DisplayName { get; set; } = string.Empty;
}

[JsonSerializable(typeof(UserInfo))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
public partial class UserInfoJsonContext : JsonSerializerContext;

[JsonSerializable(typeof(OAuthToken))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(long))]
public partial class OAuthTokenJsonContext : JsonSerializerContext;