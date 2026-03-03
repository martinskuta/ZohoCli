using System.Security.Authentication;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands;

public abstract class AuthenticatedCommand(HttpClient httpClient, TokenStore tokenStore, OAuthService oauthService) : CommandBase
{
    protected abstract Task ExecuteAuthenticated(CancellationToken cancellationToken);
    
    protected override Task ExecuteInternal(CancellationToken cancellationToken)
    {
        try
        {
            return ExecuteAuthenticated(cancellationToken);
        }
        catch (AuthenticationException)
        {
            Console.Error.WriteLine("⛔ User not authenticated! Use 'auth login' command to authenticate first.");
            Environment.Exit(1);
            return Task.CompletedTask;
        }
    }

    protected async Task<string> GetUserEmailAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        return token.UserEmail;
    }
    
    protected async Task<long> GetUserIdAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        return token.UserId;
    }

    protected async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpRequestMessage request,
        CancellationToken cancellationToken = default)
    {
        var token = await GetTokenAsync(cancellationToken);
        request.Headers.Add("Authorization", "Zoho-oauthtoken " + Uri.EscapeDataString(token.AccessToken));
        
        return await httpClient.SendAsync(request, cancellationToken);
    }

    protected async Task<OAuthToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var token = await tokenStore.GetTokenAsync();
        if (token == null)
        {
            throw new AuthenticationException("Not authenticated! Use 'auth login' command to authenticate first.");
        }

        if (!token.IsExpired) return token;
        
        var userEmail = token.UserEmail;
        var userId = token.UserId;

        var newToken = await oauthService.RefreshTokenAsync(token, cancellationToken);
        newToken.UserEmail = userEmail;
        newToken.UserId = userId;
        await tokenStore.ClearTokenAsync();
        await tokenStore.SaveTokenAsync(newToken);

        return newToken;
    }
}