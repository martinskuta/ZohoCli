using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Auth;

public class AuthStatusCommand(TokenStore tokenStore) : CommandBase
{
    protected override async Task ExecuteInternal(CancellationToken cancellationToken)
    {
        var token = await tokenStore.GetTokenAsync();
        Console.WriteLine(token == null ? "⛔ No user is logged in." : $"✅ User '{token.UserEmail}' is logged in.");
    }
}