using System.Text.Json.Nodes;
using Zoho.Cli.Zoho;

namespace Zoho.Cli.Tests;

public class LeaveTests
{
    private const string Holidays = """
        { "data": [
          { "Name": "Bärchtelis Day", "Date": "02-Jan-2026", "isHalfday": false },
          { "Name": "Sechseläuten", "Date": "20-Apr-2026", "isHalfday": true },
          { "Name": "Christmas", "Date": "25-Dec-2026", "isHalfday": false }
        ] }
        """;

    private const string LeaveRecords = """
        { "records": {
          "100": { "Leavetype": "Vacation", "Days": { "03-Mar-2026": { "LeaveCount": "0.5" }, "04-Mar-2026": { "LeaveCount": "1.0" } } },
          "101": { "Leavetype": "Sick", "Days": { "05-Mar-2026": { "LeaveCount": "0.0" } } }
        } }
        """;

    [Fact]
    public void Holidays_are_full_or_half_days()
    {
        var leaves = LeaveService.ParseHolidays(JsonNode.Parse(Holidays)!).ToList();

        Assert.Equal(
            [
                new Leave(new DateOnly(2026, 1, 2), 8, "Holiday", "Bärchtelis Day"),
                new Leave(new DateOnly(2026, 4, 20), 4, "Holiday", "Sechseläuten"),
                new Leave(new DateOnly(2026, 12, 25), 8, "Holiday", "Christmas")
            ],
            leaves);
    }

    [Fact]
    public void Leave_records_give_one_leave_per_day_and_skip_zero_days()
    {
        var leaves = LeaveService.ParseLeaveRecords(JsonNode.Parse(LeaveRecords)!).ToList();

        Assert.Equal(
            [
                new Leave(new DateOnly(2026, 3, 3), 4, "Leave", "Vacation"),
                new Leave(new DateOnly(2026, 3, 4), 8, "Leave", "Vacation")
            ],
            leaves);
    }

    [Fact]
    public async Task Leaves_command_keeps_the_range_in_date_order()
    {
        using var harness = new Harness(Harness.ValidToken());
        harness.Http.On("holidays/get", Holidays).On("leavetracker/leaves/records", LeaveRecords);

        var exit = await harness.RunAsync("leaves", "--from", "2026-01-01", "--to", "2026-06-30");

        Assert.Equal(0, exit);
        Assert.Equal(
            """[{"date":"2026-01-02","hours":8,"type":"Holiday","reason":"Bärchtelis Day"},{"date":"2026-03-03","hours":4,"type":"Leave","reason":"Vacation"},{"date":"2026-03-04","hours":8,"type":"Leave","reason":"Vacation"},{"date":"2026-04-20","hours":4,"type":"Holiday","reason":"Sechseläuten"}]""",
            harness.Output.ToString().Trim());
        Assert.Contains(harness.Http.Requests, url => url.Contains("from=01-Jan-2026&to=30-Jun-2026"));
        Assert.Contains(harness.Http.Requests, url => url.Contains("employee=%5B42%5D"));
    }

    [Theory]
    [InlineData("2026-02-01", "2026-01-01", "--to must not be before --from")]
    [InlineData("1.1.2026", "2026-01-31", "yyyy-MM-dd")]
    public async Task Bad_dates_fail_with_exit_code_1(string from, string to, string message)
    {
        using var harness = new Harness(Harness.ValidToken());

        var exit = await harness.RunAsync("leaves", "--from", from, "--to", to);

        Assert.Equal(1, exit);
        Assert.Contains(message, harness.Error.ToString());
        Assert.Empty(harness.Output.ToString());
    }
}
