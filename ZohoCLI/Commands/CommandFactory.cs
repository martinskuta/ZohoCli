using ZohoCLI.Auth;
using ZohoCLI.Commands.Auth;
using ZohoCLI.Commands.Jobs;
using ZohoCLI.Commands.Leave;

namespace ZohoCLI.Commands;

public class CommandFactory
{
    private readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());
    private readonly Lazy<TokenStore> _tokenStore = new(() => new TokenStore());
    private readonly Lazy<OAuthService> _oAuthService = new(() => new OAuthService());

    //AUTH
    public AuthStatusCommand CreateAuthStatusCommand() => new(_tokenStore.Value);
    public AuthLoginCommand CreateAuthLoginCommand() => new(_httpClient.Value, _tokenStore.Value, _oAuthService.Value);
    public AuthLogoutCommand CreateAuthLogoutCommand() => new(_tokenStore.Value, _oAuthService.Value);
    
    //JOBS
    public JobsGetCommand  CreateJobsGetCommand() => new(_httpClient.Value, _tokenStore.Value, _oAuthService.Value);
    
    //LEAVE
    public LeaveGetAll CreateLeaveGetAllCommand(DateOnly fromDate, DateOnly toDate) => new(fromDate, toDate, _httpClient.Value, _tokenStore.Value, _oAuthService.Value);
}