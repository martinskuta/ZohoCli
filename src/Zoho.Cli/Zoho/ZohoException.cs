namespace Zoho.Cli.Zoho;

/// <summary>Zoho answered with an error, or with something the CLI cannot read. Maps to exit code 1.</summary>
internal sealed class ZohoException(string message) : Exception(message);
