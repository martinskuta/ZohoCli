namespace Zoho.Cli.Zoho;

/// <param name="Domain">The Zoho data center, such as zoho.eu (ZOHO_CLI_DOMAIN).</param>
/// <param name="ConfigDirectory">Where the token file and the jobs cache live: %APPDATA%\ZohoCLI.</param>
internal sealed record ZohoSettings(string Domain, string ConfigDirectory)
{
    public static ZohoSettings FromEnvironment() => new(
        Environment.GetEnvironmentVariable("ZOHO_CLI_DOMAIN") is { Length: > 0 } domain ? domain : "zoho.eu",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZohoCLI"));

    public string PeopleUrl => $"https://people.{Domain}";
}
