using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Leave;

public class LeaveGetAll(DateOnly fromDate, DateOnly toDate, HttpClient httpClient, TokenStore tokenStore, OAuthService oauthService) : AuthenticatedCommand(httpClient, tokenStore, oauthService)
{
    protected override async Task ExecuteAuthenticated(CancellationToken cancellationToken)
    {
        var userEmail = await GetUserEmailAsync(cancellationToken);
        
        using var holidayRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://people.{ZohoEnv.Default.Domain}/people/api/leave/v2/holidays/get?employee={UriFormatter.FormatString(userEmail)}&from={UriFormatter.FormatDate(fromDate)}&to={UriFormatter.FormatDate(toDate)}&dateFormat={UriFormatter.FormattedDefaultDateFormat}");
        var holidayResponse = await SendAuthenticatedAsync(holidayRequest, cancellationToken);
        if (!holidayResponse.IsSuccessStatusCode)
        {
            var errorContent = await holidayResponse.Content.ReadAsStringAsync(cancellationToken);
            await Console.Error.WriteLineAsync($"Failed to get holidays for user '{userEmail}': {holidayResponse.StatusCode} - {errorContent}");
            Environment.Exit(1);
        }
        
        var userId = await GetUserIdAsync(cancellationToken);
        using var leaveRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://people.{ZohoEnv.Default.Domain}/people/api/v2/leavetracker/leaves/records?employee={UriFormatter.FormatString($"[{userId}]")}&from={UriFormatter.FormatDate(fromDate)}&to={UriFormatter.FormatDate(toDate)}&dateFormat={UriFormatter.FormattedDefaultDateFormat}");

        var leaveResponse = await SendAuthenticatedAsync(leaveRequest, cancellationToken);
        if (!leaveResponse.IsSuccessStatusCode)
        {
            var errorContent = await leaveResponse.Content.ReadAsStringAsync(cancellationToken);
            await Console.Error.WriteLineAsync($"Failed to get leave for user '{userEmail}': {leaveResponse.StatusCode} - {errorContent}");
            Environment.Exit(1);
        }

        var leaves = new List<LeaveInfo>();
        
        await using var holidaysContent = await holidayResponse.Content.ReadAsStreamAsync(cancellationToken);
        var holidays = await JsonNode.ParseAsync(holidaysContent, cancellationToken: cancellationToken);
        foreach (var holidayNode in holidays!["data"]!.AsArray())
        {
            var holiday = holidayNode!.AsObject();
            var holidayName = holiday["Name"]!.GetValue<string>();
            var holidayDate = DateOnly.ParseExact(holiday["Date"]!.GetValue<string>(), "dd-MMM-yyyy", CultureInfo.InvariantCulture);
            var isHalfDay = holiday["isHalfday"]!.GetValue<bool>();
            
            var leave = new LeaveInfo
            {
                Reason = holidayName,
                Date = holidayDate,
                Hours = isHalfDay ? 4 : 8,
                Type = "Holiday"
            };
            leaves.Add(leave);
        }
        
        await using var leaveContent = await leaveResponse.Content.ReadAsStreamAsync(cancellationToken);
        var leaveRecords = await JsonNode.ParseAsync(leaveContent, cancellationToken: cancellationToken);
        
        foreach (var leaveNode in leaveRecords!["records"]!.AsObject())
        {
            var leaveObj = leaveNode!.Value!.AsObject();
            var leaveType = leaveObj["Leavetype"]!.GetValue<string>();

            var days = leaveObj["Days"]!.AsObject();
            foreach (var dayObj in days)
            {
                var leaveCount = dayObj!.Value!.AsObject()["LeaveCount"]!.GetValue<string>();
                switch (leaveCount)
                {
                    case "0.0" or "0":
                        continue;
                    case "1.0":
                        leaves.Add(new LeaveInfo
                        {
                            Reason = leaveType,
                            Date = DateOnly.ParseExact(dayObj.Key, "dd-MMM-yyyy", CultureInfo.InvariantCulture),
                            Hours = 8,
                            Type = "Leave"
                        });
                        break;
                    case "0.5":
                        leaves.Add(new LeaveInfo
                        {
                            Reason = leaveType,
                            Date = DateOnly.ParseExact(dayObj.Key, "dd-MMM-yyyy", CultureInfo.InvariantCulture),
                            Hours = 4,
                            Type = "Leave"
                        });
                        break;
                }
            }
        }
        
        Console.WriteLine(JsonSerializer.Serialize(leaves, LeaveInfoJsonContext.Default.ListLeaveInfo));
    }
}