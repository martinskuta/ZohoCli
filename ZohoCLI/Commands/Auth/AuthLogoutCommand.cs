using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Auth;

public class AuthLogoutCommand(TokenStore tokenStore, OAuthService oauthService) : CommandBase
{
    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetTokenAsync();
        if (token != null)
        {
            await Task.WhenAll(
                oauthService.RevokeRefreshTokenAsync(token, cancellationToken),
                tokenStore.ClearTokenAsync());
            Console.WriteLine("✅ User successfully logged out!");
        }
        else
        {
            Console.WriteLine("No one is currently logged in!");
        }
    }
}