using System.Text.Json.Nodes;

namespace ZohoCLI.Commands;

public static class ResponseHelper
{
    public static async Task<JsonNode?> GetJsonResponse(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            await Console.Error.WriteLineAsync($"Error calling Zoho API: {response.StatusCode} - {errorContent}");
            Environment.Exit(1);
        }

        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
        var jsonResponse = await JsonNode.ParseAsync(content, cancellationToken: cancellationToken);

        if (jsonResponse is null)
        {
            await Console.Error.WriteLineAsync($"Error parsing Zoho API response to JSON for request: {response.RequestMessage?.RequestUri}");
            Environment.Exit(1);
        }

        var errors = jsonResponse["response"]?["errors"]?.AsArray();
        if (errors is not null)
        {
            var errorMessages = string.Join(", ", errors.Where(e => e is not null).Select(error =>
            {
                var code = error!["code"]?.ToString();
                var message = error["message"]?.ToString();
                return message is not null ? $"{code}: {message}" : null;
            }).Where(e => e is not null));
            
            
            await Console.Error.WriteLineAsync($"Zoho API response for request: {response.RequestMessage?.RequestUri} returned errors: {errorMessages}");
            Environment.Exit(1);
        }
        
        return jsonResponse;
    }
}