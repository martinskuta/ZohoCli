using System.Text.Json;
using System.Text.Json.Nodes;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Auth;

public class AuthLoginCommand(HttpClient httpClient, TokenStore tokenStore, OAuthService oauthService)
    : AuthenticatedCommand(httpClient, tokenStore, oauthService)
{
    private readonly TokenStore _tokenStore = tokenStore;
    private readonly OAuthService _oauthService = oauthService;

    protected override async Task ExecuteAuthenticated(CancellationToken cancellationToken)
    {
        var token = await _tokenStore.GetTokenAsync();

        if (token != null)
        {
            Console.WriteLine("Already authenticated!");
        }
        else
        {
            Console.WriteLine("Initiating OAuth2 login with Zoho People...");
            Console.WriteLine("Your browser will open shortly.");

            token = await _oauthService.LoginAsync(cancellationToken);
            var userInfo = await _oauthService.GetUserInfoAsync(token, cancellationToken);
            token.UserEmail = userInfo.Email;
            
            // Store token securely
            await _tokenStore.SaveTokenAsync(token);

            using var userRequest = new HttpRequestMessage(HttpMethod.Get,
                $"https://people.zoho.eu/api/forms/P_EmployeeView/records?searchColumn=EMPLOYEEMAILALIAS&searchValue={UriFormatter.FormatString(token.UserEmail)}");

            var userResponse = await SendAuthenticatedAsync(userRequest, cancellationToken);

            if (!userResponse.IsSuccessStatusCode)
            {
                var errorContent = await userResponse.Content.ReadAsStringAsync(cancellationToken);
                await Console.Error.WriteLineAsync(
                    $"Failed to get info of user '{token.UserEmail}': {userResponse.StatusCode} - {errorContent}");
                Environment.Exit(1);
            }

            await using var userContent = await userResponse.Content.ReadAsStreamAsync(cancellationToken);
            var jUserContent = await JsonNode.ParseAsync(userContent, cancellationToken: cancellationToken);

            var userIdStr = jUserContent?[0]?["recordId"]?.GetValue<string>();
            if (userIdStr == null)
            {
                await _tokenStore.ClearTokenAsync();
                throw new InvalidOperationException("Could not get user id");
            }

            token.UserId = long.Parse(userIdStr);

            // Store token securely
            await _tokenStore.ClearTokenAsync();
            await _tokenStore.SaveTokenAsync(token);

            Console.WriteLine();
            Console.WriteLine($"✅ User '{userInfo.Email}' successfully authenticated!");
            Console.WriteLine("OAuth token securely stored and will be used for future requests.");
        }
    }
}