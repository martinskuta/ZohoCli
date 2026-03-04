using System.Text.Json.Nodes;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Timelogs;

public class TimelogsGetCommand(
    string? user,
    DateOnly fromDate,
    DateOnly toDate,
    HttpClient httpClient,
    TokenStore tokenStore,
    OAuthService oauthService)
    : AuthenticatedCommand(httpClient, tokenStore, oauthService)
{
    private readonly string _timelogsEndpoint = $"https://people.{ZohoEnv.Default.Domain}/people/api/timetracker/gettimelogs";
    private readonly int _maxPageSize = 200;

    protected override async Task ExecuteAuthenticated(CancellationToken cancellationToken)
    {
        var effectiveUser = string.IsNullOrWhiteSpace(user) ? await GetUserEmailAsync(cancellationToken) : user;

        var allTimelogs = new JsonArray();
        
        var currentFromDate = fromDate;
        var currentToDate = GetNextToDate(currentFromDate);

        //Zoho allows to get timelogs only for maximum of 1 month, so we need to split the request into multiple requests if the time range is longer than 1 month
        do
        {
            var startIndex = 0;
            
            while (true) //Then each month response is paginated into 200 timelogs, so we iterate all pages here
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(effectiveUser, currentFromDate, currentToDate, startIndex));
                var response = await SendAuthenticatedAsync(request, cancellationToken);

                var jsonResponse = await response.GetJsonResponse(cancellationToken);
                var pageTimelogs = jsonResponse?["response"]?["result"]?.AsArray();
                if (pageTimelogs == null || pageTimelogs.Count == 0)
                {
                    break;
                }

                foreach (var timelog in pageTimelogs)
                {
                    if (timelog != null)
                    {
                        allTimelogs.Add(timelog.DeepClone());
                    }
                }

                if (pageTimelogs.Count < _maxPageSize)
                {
                    break;
                }

                startIndex += _maxPageSize;
            }
            
            var nextMonth = currentFromDate.Month == 12 ? 1 : currentFromDate.Month + 1;
            var nextYear = nextMonth == 1 ? currentFromDate.Year + 1 : currentFromDate.Year;
            
            currentFromDate = new DateOnly(nextYear, nextMonth, 1);
            currentToDate = GetNextToDate(currentFromDate);
            
        } while (currentFromDate < toDate && currentToDate <= toDate);

        Console.WriteLine(allTimelogs.ToJsonString());
    }

    private DateOnly GetNextToDate(DateOnly currentFromDate)
    {
        var nextToDate = new DateOnly(currentFromDate.Year, currentFromDate.Month,
            DateTime.DaysInMonth(currentFromDate.Year, currentFromDate.Month));
        return nextToDate < toDate ? nextToDate : toDate;
    }

    private string BuildRequestUri(string selectedUser, DateOnly currentFromDate, DateOnly currentToDate, int startIndex)
    {
        var parameters = new List<string>
        {
            $"user={UriFormatter.FormatString(selectedUser)}",
            $"limit={_maxPageSize}",
            $"sIndex={startIndex}",
            $"dateFormat={UriFormatter.DefaultDateFormat}",
            $"fromDate={UriFormatter.FormatDate(currentFromDate)}",
            $"toDate={UriFormatter.FormatDate(currentToDate)}"
        };

        return $"{_timelogsEndpoint}?{string.Join("&", parameters)}";
    }
}