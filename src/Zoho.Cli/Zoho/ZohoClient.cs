using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Zoho.Cli.Auth;

namespace Zoho.Cli.Zoho;

/// <summary>Authenticated GET requests to Zoho People, refreshing the saved token when it has expired.</summary>
internal sealed class ZohoClient(HttpClient http, ITokenStore tokens, OAuthClient oauth, ZohoSettings settings, TimeProvider time)
{
    public ZohoSettings Settings => settings;

    public async Task<OAuthToken> GetTokenAsync(CancellationToken ct)
    {
        var token = await tokens.LoadAsync() ?? throw new NotSignedInException("no saved Zoho token.");
        if (!token.IsExpired(time)) return token;

        token = await oauth.RefreshAsync(token, ct);
        await tokens.SaveAsync(token);
        return token;
    }

    public async Task<JsonNode> GetJsonAsync(string pathAndQuery, CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.PeopleUrl + pathAndQuery);
        request.Headers.Add("Authorization", "Zoho-oauthtoken " + token.AccessToken);
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new NotSignedInException($"Zoho refused the token: {body}");
        if (!response.IsSuccessStatusCode)
            throw new ZohoException($"Zoho request {pathAndQuery} failed: {(int)response.StatusCode} {body}");

        try
        {
            return JsonNode.Parse(body) ?? throw new ZohoException($"Zoho request {pathAndQuery} returned nothing.");
        }
        catch (JsonException)
        {
            throw new ZohoException($"Zoho request {pathAndQuery} did not return JSON: {body}");
        }
    }

    /// <summary>The signed-in person's employee record, from the form P_EmployeeView.</summary>
    public async Task<Profile> GetProfileAsync(CancellationToken ct)
    {
        var token = await GetTokenAsync(ct);
        var node = await GetJsonAsync(
            $"/api/forms/P_EmployeeView/records?searchColumn=EMPLOYEEMAILALIAS&searchValue={Uri.EscapeDataString(token.UserEmail)}", ct);
        return ParseProfile(token.UserEmail, node);
    }

    internal static Profile ParseProfile(string email, JsonNode node)
    {
        if (node is not JsonArray { Count: > 0 } records || records[0] is not JsonObject record)
            throw new ZohoException($"No Zoho employee record for {email}: {node.ToJsonString()}");

        var recordId = record["recordId"]?.ToString();
        var employeeId = record["Employee ID"]?.ToString();
        if (!long.TryParse(recordId, CultureInfo.InvariantCulture, out var id) || string.IsNullOrWhiteSpace(employeeId))
            throw new ZohoException($"The Zoho employee record of {email} has no recordId or Employee ID. Fields: {string.Join(", ", record.Select(p => p.Key))}");

        return new Profile(email, employeeId, id);
    }

    public static string FormatDate(DateOnly date) =>
        Uri.EscapeDataString(date.ToString(ZohoDate.Format, CultureInfo.InvariantCulture));
}

internal sealed record Profile(string Email, string EmployeeId, long RecordId);

internal static class ZohoDate
{
    /// <summary>The date format of the Zoho People API, with English month names.</summary>
    public const string Format = "dd-MMM-yyyy";

    public static DateOnly Parse(string value) => DateOnly.ParseExact(value, Format, CultureInfo.InvariantCulture);
}
