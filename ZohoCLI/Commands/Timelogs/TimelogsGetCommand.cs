using System.Text.Json.Nodes;
using System.Security.Authentication;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Timelogs;

public class TimelogsGetCommand(string? user, DateOnly? fromDate, DateOnly? toDate, HttpClient httpClient, TokenStore tokenStore, OAuthService oauthService)
    : AuthenticatedCommand(httpClient, tokenStore, oauthService)
{
    private const int MaxPageSize = 200;

    protected override async Task ExecuteAuthenticated(CancellationToken cancellationToken)
    {
        var selectedUser = user;
        if (string.IsNullOrWhiteSpace(selectedUser))
        {
            try
            {
                selectedUser = await GetUserEmailAsync(cancellationToken);
            }
            catch (AuthenticationException)
            {
                await Console.Error.WriteLineAsync("⛔ User not authenticated! Use 'auth login' command to authenticate first.");
                Environment.Exit(1);
                return;
            }
        }

        var allTimelogs = new JsonArray();
        var startIndex = 0;

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                BuildRequestUri(selectedUser, startIndex));
            var response = await SendAuthenticatedAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                await Console.Error.WriteLineAsync($"Failed to get timelogs for user '{selectedUser}': {response.StatusCode} - {errorContent}");
                Environment.Exit(1);
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            var timelogsResponse = await JsonNode.ParseAsync(content, cancellationToken: cancellationToken);
            var pageTimelogs = timelogsResponse?["response"]?["result"]?.AsArray();
            if (pageTimelogs == null || pageTimelogs.Count == 0)
            {
                break;
            }

            foreach (var timelog in pageTimelogs)
            {
                allTimelogs.Add(timelog?.DeepClone());
            }

            if (pageTimelogs.Count < MaxPageSize)
            {
                break;
            }

            startIndex += MaxPageSize;
        }

        Console.WriteLine(allTimelogs.ToJsonString());
    }

    private string BuildRequestUri(string selectedUser, int startIndex)
    {
        var parameters = new List<string>
        {
            $"user={UriFormatter.FormatString(selectedUser)}",
            $"limit={MaxPageSize}",
            $"sIndex={startIndex}"
        };

        if (fromDate.HasValue)
        {
            parameters.Add($"fromDate={UriFormatter.FormatDate(fromDate.Value)}");
        }

        if (toDate.HasValue)
        {
            parameters.Add($"toDate={UriFormatter.FormatDate(toDate.Value)}");
        }

        return $"https://people.zoho.eu/people/api/timesheet/get-timelogs?{string.Join("&", parameters)}";
    }
}
