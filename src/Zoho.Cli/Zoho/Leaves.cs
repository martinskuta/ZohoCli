using System.Text.Json.Nodes;

namespace Zoho.Cli.Zoho;

internal sealed record Leave(DateOnly Date, int Hours, string Type, string Reason);

/// <summary>Holidays and leave records of the signed-in person: a full day is 8 hours, a half day 4.</summary>
internal sealed class LeaveService(ZohoClient zoho)
{
    public async Task<List<Leave>> GetAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var token = await zoho.GetTokenAsync(ct);
        var range = $"from={ZohoClient.FormatDate(from)}&to={ZohoClient.FormatDate(to)}&dateFormat={Uri.EscapeDataString(ZohoDate.Format)}";
        var holidays = await zoho.GetJsonAsync(
            $"/people/api/leave/v2/holidays/get?employee={Uri.EscapeDataString(token.UserEmail)}&{range}", ct);
        var leaves = await zoho.GetJsonAsync(
            $"/people/api/v2/leavetracker/leaves/records?employee={Uri.EscapeDataString($"[{token.UserId}]")}&{range}", ct);

        return ParseHolidays(holidays).Concat(ParseLeaveRecords(leaves))
            .Where(leave => leave.Date >= from && leave.Date <= to)
            .OrderBy(leave => leave.Date)
            .ToList();
    }

    internal static IEnumerable<Leave> ParseHolidays(JsonNode node)
    {
        if (node["data"] is not JsonArray holidays)
            throw new ZohoException($"Unexpected Zoho holidays: {node.ToJsonString()}");

        foreach (var holiday in holidays.OfType<JsonObject>())
            yield return new Leave(
                ZohoDate.Parse(holiday["Date"]!.GetValue<string>()),
                holiday["isHalfday"]?.GetValue<bool>() == true ? 4 : 8,
                "Holiday",
                holiday["Name"]?.GetValue<string>() ?? "");
    }

    internal static IEnumerable<Leave> ParseLeaveRecords(JsonNode node)
    {
        if (node["records"] is not JsonObject records)
            throw new ZohoException($"Unexpected Zoho leave records: {node.ToJsonString()}");

        foreach (var (_, value) in records)
        {
            if (value is not JsonObject record || record["Days"] is not JsonObject days) continue;
            var type = record["Leavetype"]?.GetValue<string>() ?? "";

            foreach (var (day, detail) in days)
            {
                var hours = detail?["LeaveCount"]?.ToString() switch
                {
                    "1.0" or "1" => 8,
                    "0.5" => 4,
                    _ => 0
                };
                if (hours > 0) yield return new Leave(ZohoDate.Parse(day), hours, "Leave", type);
            }
        }
    }
}
