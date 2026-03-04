using System.Globalization;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Timelogs;

public class TimelogsAddCommand(
    string? user,
    string jobId,
    DateOnly date,
    string dateFormat,
    decimal hours,
    string workItem,
    string description,
    HttpClient httpClient,
    TokenStore tokenStore,
    OAuthService oauthService)
    : AuthenticatedCommand(httpClient, tokenStore, oauthService)
{
    protected override async Task ExecuteAuthenticated(CancellationToken cancellationToken)
    {
        var effectiveUser = string.IsNullOrWhiteSpace(user) ? await GetUserEmailAsync(cancellationToken) : user;

        var parameters = new Dictionary<string, string>
        {
            ["user"] = effectiveUser,
            ["jobId"] = jobId,
            ["date"] = date.ToString(dateFormat, CultureInfo.InvariantCulture),
            ["dateFormat"] = dateFormat,
            ["hours"] = hours.ToString(CultureInfo.InvariantCulture),
            ["workItem"] = workItem,
            ["description"] = description
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://people.zoho.eu/people/api/timetracker/addtimelog");
        request.Content = new FormUrlEncodedContent(parameters);
        
        var response = await SendAuthenticatedAsync(request, cancellationToken);
        var jsonResponse = await response.GetJsonResponse(cancellationToken);
        
        var timelogId = jsonResponse?["response"]?["result"]?[0]?["timeLogId"]?.ToString();

        await Console.Out.WriteLineAsync($"Timelog entry {timelogId} added successfully");
    }
}
