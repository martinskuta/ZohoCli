using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Zoho.Cli.Zoho;

internal sealed record Job(string ClientId, string ClientName, string ProjectId, string ProjectName, string TaskCode, string JobName)
{
    public bool IsComplete() => new[] { ClientId, ClientName, ProjectId, ProjectName, TaskCode, JobName }.All(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>Timesheet jobs from the forms P_TimesheetJobsList (projects with their client) and P_TimesheetJob.</summary>
internal sealed class JobService(ZohoClient zoho)
{
    private const int PageSize = 200;

    public async Task<List<Job>> GetAsync(CancellationToken ct)
    {
        var projects = await GetFormRecordsAsync("P_TimesheetJobsList", ct);
        var jobs = await GetFormRecordsAsync("P_TimesheetJob", ct);
        return Join(projects, jobs);
    }

    internal static List<Job> Join(IEnumerable<JsonObject> projects, IEnumerable<JsonObject> jobs)
    {
        var projectsByName = projects
            .Where(p => !string.IsNullOrEmpty(p["Project_Name"]?.ToString()))
            .ToLookup(p => p["Project_Name"]!.ToString(), StringComparer.Ordinal);

        return jobs.Select(job =>
            {
                var projectName = job["Project"]?.ToString() ?? "";
                var projectId = job["Project.ID"]?.ToString() ?? "";
                var candidates = projectsByName[projectName].ToList();
                var project = candidates.FirstOrDefault(p => p["Zoho_ID"]?.ToString() == projectId)
                              ?? (candidates.Count == 1 ? candidates[0] : null);

                return new Job(
                    project?["ClientId.ID"]?.ToString() ?? "",
                    project?["ClientId"]?.ToString() ?? "",
                    projectId,
                    projectName,
                    job["Task_Code"]?.ToString() ?? "",
                    job["Job_Name"]?.ToString() ?? "");
            })
            .Where(job => job.IsComplete())
            .ToList();
    }

    internal async Task<List<JsonObject>> GetFormRecordsAsync(string form, CancellationToken ct)
    {
        var records = new List<JsonObject>();
        for (var start = 1; ; start += PageSize)
        {
            var node = await zoho.GetJsonAsync(
                $"/people/api/forms/{form}/getRecords?sIndex={start.ToString(CultureInfo.InvariantCulture)}&limit={PageSize}", ct);
            var page = ReadPage(form, node);
            if (page == null) break;

            records.AddRange(page);
            if (page.Count < PageSize) break;
        }

        return records;
    }

    /// <summary>The records of one page, or null when Zoho says there are no more (error 7024).</summary>
    internal static List<JsonObject>? ReadPage(string form, JsonNode node)
    {
        var response = node["response"];
        if (response?["status"]?.GetValue<int>() != 0)
        {
            if (response?["errors"]?["code"]?.ToString() == "7024") return null;
            throw new ZohoException($"Zoho form {form} failed: {response?.ToJsonString() ?? node.ToJsonString()}");
        }

        if (response["result"] is not JsonArray result)
            throw new ZohoException($"Zoho form {form} returned no records: {response.ToJsonString()}");

        // Each entry is { "<record id>": [ { fields } ] }.
        var records = new List<JsonObject>();
        foreach (var entry in result)
        {
            if (entry is not JsonObject { Count: 1 } envelope || envelope.First().Value is not JsonArray values)
                throw new ZohoException($"Unexpected record in Zoho form {form}: {entry?.ToJsonString()}");
            records.AddRange(values.OfType<JsonObject>());
        }

        return records;
    }
}

internal sealed record JobsCacheKey(long UserId, string UserEmail, string Domain);

internal sealed record JobsCacheEntry(int Version, long UserId, string UserEmail, string Domain, DateTimeOffset FetchedAt, List<Job> Jobs);

/// <summary>The jobs of one person and domain, kept for a month.</summary>
internal sealed class JobsCache(string path, TimeProvider time)
{
    private const int Version = 1;

    public async Task<List<Job>?> LoadAsync(JobsCacheKey key, CancellationToken ct)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var entry = await JsonSerializer.DeserializeAsync(stream, ZohoJson.Default.JobsCacheEntry, ct);
            var now = time.GetUtcNow();
            var valid = entry is { Version: Version }
                        && entry.UserId == key.UserId
                        && string.Equals(entry.UserEmail, key.UserEmail, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(entry.Domain, key.Domain, StringComparison.OrdinalIgnoreCase)
                        && entry.FetchedAt <= now && entry.FetchedAt > now.AddMonths(-1);
            return valid ? entry!.Jobs : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(JobsCacheKey key, List<Job> jobs, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream,
                new JobsCacheEntry(Version, key.UserId, key.UserEmail, key.Domain, time.GetUtcNow(), jobs),
                ZohoJson.Default.JobsCacheEntry, ct);
        }

        File.Move(temporary, path, true);
    }
}
