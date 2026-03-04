using System.Text.Json;
using System.Text.Json.Nodes;
using ZohoCLI.Auth;

namespace ZohoCLI.Commands.Jobs;

public class JobsGetCommand(HttpClient httpClient, TokenStore tokenStore, OAuthService oauthService)
    : AuthenticatedCommand(httpClient, tokenStore, oauthService)
{
    protected override async Task ExecuteAuthenticated(CancellationToken cancellationToken)
    {
        // using var clientsRequest = new HttpRequestMessage(HttpMethod.Get,
        //     "https://people.zoho.eu/people/api/timetracker/getclients");
        // var clientsResponse = await SendAuthenticatedAsync(clientsRequest, cancellationToken);
        // if (!clientsResponse.IsSuccessStatusCode)
        // {
        //     var errorContent = await clientsResponse.Content.ReadAsStringAsync(cancellationToken);
        //     await Console.Error.WriteLineAsync(
        //         $"Failed to get clients: {clientsResponse.StatusCode} - {errorContent}");
        //     Environment.Exit(1);
        // }
        //
        // var clientProjects = new Dictionary<(string, string), List<(string, string)>>();
        //
        // await using var clientsContent = await clientsResponse.Content.ReadAsStreamAsync(cancellationToken);
        // var clients = await JsonNode.ParseAsync(clientsContent, cancellationToken: cancellationToken);
        // foreach (var clientNode in clients?["response"]?["result"]?.AsArray() ?? [])
        // {
        //     var obj = clientNode!.AsObject();
        //     clientProjects.Add((obj["clientName"]?.GetValue<string>() ?? string.Empty,
        //         obj["clientId"]?.GetValue<string>() ?? string.Empty), []);
        // }
        //
        // foreach ((string clientName, string clientId) x in clientProjects.Keys)
        // {
        //     
        // }

        // using var projectsRequest = new HttpRequestMessage(HttpMethod.Get,
        //     "https://people.zoho.eu/people/api/timetracker/getprojects?assignedTo=all");
        // using var projectsResponse = await SendAuthenticatedAsync(projectsRequest, cancellationToken);
        //
        // if (!projectsResponse.IsSuccessStatusCode)
        // {
        //     var errorContent = await projectsResponse.Content.ReadAsStringAsync(cancellationToken);
        //     await Console.Error.WriteLineAsync(
        //         $"Failed to get projects: {projectsResponse.StatusCode} - {errorContent}");
        //     Environment.Exit(1);
        // }
        //
        // var clientProjects =
        //     new Dictionary<(string clientId, string clientName), HashSet<(string projectId, string projectName)>>();
        //
        // await using var projectsContent = await projectsResponse.Content.ReadAsStreamAsync(cancellationToken);
        // var projectsNode =
        //     await JsonSerializer.DeserializeAsync<JsonNode>(projectsContent, cancellationToken: cancellationToken);
        // foreach (var projectNode in projectsNode?["response"]?["result"]?.AsArray() ?? [])
        // {
        //     var obj = projectNode!.AsObject();
        //
        //     var client = (obj["clientId"]?.GetValue<string>() ?? string.Empty,
        //         obj["clientName"]?.GetValue<string>() ?? string.Empty);
        //     var project = (obj["projectId"]?.GetValue<string>() ?? string.Empty,
        //         obj["projectName"]?.GetValue<string>() ?? string.Empty);
        //
        //     if (clientProjects.TryGetValue(client, out var projectsForClient))
        //         projectsForClient.Add(project);
        //     else
        //         clientProjects.Add(client, [project]);
        // }

        //await using var content = File.OpenRead(@"C:\Users\marti\Downloads\jobs.json");

        var activeJobs = new Dictionary<string, JobInfo>();

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://people.{ZohoEnv.Default.Domain}/people/api/timetracker/getjobs?assignedTo=all&limit=200");

        var response = await SendAuthenticatedAsync(request, cancellationToken);
        
        var jobsContentNode = await response.GetJsonResponse(cancellationToken);

        foreach (var jobNode in jobsContentNode?["response"]?["result"]?.AsArray() ?? [])
        {
            var jobObj = jobNode!.AsObject();
            if (jobObj["isActive"]?.GetValue<bool>() != true) continue;

            var isBillable = jobObj["jobBillableStatus"]?.GetValue<string>() == "Billable";
            var jobName = jobObj["jobName"]?.GetValue<string>() ?? string.Empty;
            var projectName = jobObj["projectName"]?.GetValue<string>() ?? string.Empty;

            var key = $"{projectName}##{jobName}";
            key = key.Replace(" ", "").Replace("_", "").Replace("-", "");

            activeJobs.Add(key, new JobInfo
            {
                ClientName = jobObj["clientName"]?.GetValue<string>() ?? string.Empty,
                ClientId = jobObj["clientId"]?.GetValue<string>() ?? string.Empty,
                ProjectId = jobObj["projectId"]?.GetValue<string>() ?? string.Empty,
                ProjectName = projectName,
                JobId = jobObj["jobId"]?.GetValue<string>() ?? string.Empty,
                JobName = jobName,
                IsBillable = isBillable
            });
        }

        Console.WriteLine(JsonSerializer.Serialize(activeJobs, JobInfoJsonContext.Default.DictionaryStringJobInfo));
    }
}