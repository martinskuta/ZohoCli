using System.Text.Json.Serialization;
using Zoho.Cli.Auth;
using Zoho.Cli.Zoho;

namespace Zoho.Cli;

internal sealed record ProfileOutput(string Email, string EmployeeId);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OAuthToken))]
[JsonSerializable(typeof(ProfileOutput))]
[JsonSerializable(typeof(List<Leave>))]
[JsonSerializable(typeof(List<Job>))]
[JsonSerializable(typeof(JobsCacheEntry))]
internal sealed partial class ZohoJson : JsonSerializerContext;
