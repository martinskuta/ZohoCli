using System.Text.Json.Nodes;
using Zoho.Cli.Zoho;

namespace Zoho.Cli.Tests;

public class JobTests
{
    private static JsonObject Project(string name, string zohoId, string clientId, string client) =>
        new() { ["Project_Name"] = name, ["Zoho_ID"] = zohoId, ["ClientId.ID"] = clientId, ["ClientId"] = client };

    private static JsonObject Job(string project, string projectId, string taskCode, string name) =>
        new() { ["Project"] = project, ["Project.ID"] = projectId, ["Task_Code"] = taskCode, ["Job_Name"] = name };

    private static string Page(params JsonObject[] records) => new JsonObject
    {
        ["response"] = new JsonObject
        {
            ["status"] = 0,
            ["result"] = new JsonArray(records.Select((r, i) => (JsonNode)new JsonObject { [$"{i}"] = new JsonArray(r.DeepClone()) }).ToArray())
        }
    }.ToJsonString();

    private const string NoMoreRecords = """{ "response": { "status": 1, "errors": { "code": 7024, "message": "No records found" } } }""";

    [Fact]
    public void Jobs_take_the_client_of_their_project_by_id_when_names_repeat()
    {
        var projects = new[] { Project("Skye", "p1", "c1", "Innoveo"), Project("Skye", "p2", "c2", "Other") };
        var jobs = new[] { Job("Skye", "p2", "DEV", "Development") };

        var result = JobService.Join(projects, jobs);

        Assert.Equal([new Job("c2", "Other", "p2", "Skye", "DEV", "Development")], result);
    }

    [Fact]
    public void Jobs_fall_back_to_the_only_project_of_that_name_and_drop_incomplete_ones()
    {
        var projects = new[] { Project("Skye", "p1", "c1", "Innoveo") };
        var jobs = new[] { Job("Skye", "other-id", "DEV", "Development"), Job("Unknown", "x", "DEV", "Development"), Job("Skye", "p1", "", "No code") };

        var result = JobService.Join(projects, jobs);

        Assert.Equal([new Job("c1", "Innoveo", "other-id", "Skye", "DEV", "Development")], result);
    }

    [Fact]
    public void A_page_with_error_7024_means_no_more_records()
    {
        Assert.Null(JobService.ReadPage("P_TimesheetJob", JsonNode.Parse(NoMoreRecords)!));
    }

    [Fact]
    public void Another_form_error_is_reported()
    {
        var node = JsonNode.Parse("""{ "response": { "status": 1, "errors": { "code": 7011, "message": "Invalid form" } } }""")!;

        var error = Assert.Throws<ZohoException>(() => JobService.ReadPage("P_TimesheetJob", node));
        Assert.Contains("7011", error.Message);
    }

    [Fact]
    public async Task Form_records_are_read_page_by_page_until_7024()
    {
        using var harness = new Harness(Harness.ValidToken());
        var fullPage = Page(Enumerable.Range(0, 200).Select(i => Job("Skye", "p1", $"T{i}", $"Job {i}")).ToArray());
        harness.Http.On("sIndex=1&", fullPage).On("sIndex=201&", NoMoreRecords);

        var records = await new JobService(harness.Client()).GetFormRecordsAsync("P_TimesheetJob", CancellationToken.None);

        Assert.Equal(200, records.Count);
        Assert.Equal(2, harness.Http.Requests.Count);
    }

    [Fact]
    public async Task Jobs_command_uses_the_cache_for_the_same_person_and_refresh_skips_it()
    {
        using var harness = new Harness(Harness.ValidToken());
        harness.Http
            .On("P_TimesheetJobsList", Page(Project("Skye", "p1", "c1", "Innoveo")))
            .On("P_TimesheetJob/", Page(Job("Skye", "p1", "DEV", "Development")));
        const string expected = """[{"clientId":"c1","clientName":"Innoveo","projectId":"p1","projectName":"Skye","taskCode":"DEV","jobName":"Development"}]""";

        Assert.Equal(0, await harness.RunAsync("jobs"));
        Assert.Equal(0, await harness.RunAsync("jobs"));
        Assert.Equal(2, harness.Http.Requests.Count);
        Assert.Equal([expected, expected], harness.Output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(0, await harness.RunAsync("jobs", "--refresh"));
        Assert.Equal(4, harness.Http.Requests.Count);
    }

    [Fact]
    public async Task The_cache_expires_after_a_month_and_belongs_to_one_person()
    {
        using var harness = new Harness();
        var cache = new JobsCache(harness.CachePath, harness.Time);
        var key = new JobsCacheKey(42, "jane@example.com", "zoho.eu");
        await cache.SaveAsync(key, [new Job("c1", "Innoveo", "p1", "Skye", "DEV", "Development")], CancellationToken.None);

        Assert.NotNull(await cache.LoadAsync(key with { UserEmail = "JANE@example.com" }, CancellationToken.None));
        Assert.Null(await cache.LoadAsync(key with { UserId = 7 }, CancellationToken.None));
        Assert.Null(await cache.LoadAsync(key with { Domain = "zoho.com" }, CancellationToken.None));

        harness.Time.Now = Harness.Now.AddMonths(1).AddMinutes(1);
        Assert.Null(await cache.LoadAsync(key, CancellationToken.None));
    }
}
