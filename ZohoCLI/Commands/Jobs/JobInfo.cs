using System.Text.Json.Serialization;
using ZohoCLI.Commands.Leave;

namespace ZohoCLI.Commands.Jobs;

public class JobInfo
{
    public string ClientName { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ProjectId { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public string JobId { get; init; } = string.Empty;
    public string JobName { get; init; } = string.Empty;
    public bool IsBillable { get; init; }
}

[JsonSerializable(typeof(Dictionary<string, JobInfo>))]
[JsonSerializable(typeof(JobInfo))]
public partial class JobInfoJsonContext : JsonSerializerContext;