namespace ZohoCLI;

public record ZohoEnv(string Domain)
{
    public static ZohoEnv Default { get; } = new(Environment.GetEnvironmentVariable("ZOHO_CLI_DOMAIN") ?? "zoho.eu");
}