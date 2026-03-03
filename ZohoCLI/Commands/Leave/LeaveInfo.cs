using System.Text.Json.Serialization;

namespace ZohoCLI.Commands.Leave;

public class LeaveInfo
{
    public DateOnly Date { get; set; }

    public int Hours { get; set; }
    
    public string Type { get; set; } = string.Empty;
    
    public string Reason { get; set; } = string.Empty;
}

[JsonSerializable(typeof(LeaveInfo))]
[JsonSerializable(typeof(List<LeaveInfo>))]
public partial class LeaveInfoJsonContext : JsonSerializerContext;