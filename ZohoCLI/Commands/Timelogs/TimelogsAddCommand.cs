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

        var queryString = string.Join("&", parameters.Select(x => $"{UriFormatter.FormatString(x.Key)}={UriFormatter.FormatString(x.Value)}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://people.zoho.eu/people/api/timesheet/addtimelogs?{queryString}");
        var response = await SendAuthenticatedAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync($"Failed to add timelog entry: {response.StatusCode} - {content}");
            Environment.Exit(1);
        }

        await Console.Out.WriteLineAsync(content);
    }
}
