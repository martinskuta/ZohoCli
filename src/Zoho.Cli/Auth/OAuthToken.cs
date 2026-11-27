using System.Text.Json.Serialization;

namespace Zoho.Cli.Auth;

/// <summary>The saved Zoho token. Property names match the token timeshiit saves, so both tools share it.</summary>
internal sealed class OAuthToken
{
    [JsonPropertyName("user_email")] public string UserEmail { get; set; } = string.Empty;
    [JsonPropertyName("user_id")] public long UserId { get; set; } = -1;
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("scope")] public string Scope { get; set; } = string.Empty;
    [JsonPropertyName("api_domain")] public string ApiDomain { get; set; } = string.Empty;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }

    /// <summary>In UTC.</summary>
    [JsonPropertyName("expires_at")] public DateTimeOffset ExpiresAt { get; set; }

    public bool IsExpired(TimeProvider time) => time.GetUtcNow() >= ExpiresAt;
}
