namespace Zoho.Cli.Auth;

/// <summary>No token, or Zoho refused it: the person has to run 'zoho login'. Maps to exit code 2.</summary>
internal sealed class NotSignedInException(string message) : Exception(message);
