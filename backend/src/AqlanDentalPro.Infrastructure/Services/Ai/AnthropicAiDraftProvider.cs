using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Anthropic (Claude) provider for the Ceph C-D draft-diagnosis assistant —
/// the original Anthropic call extracted from CephAiDraftService unchanged.
/// Raw HttpClient against the Messages API — no SDK package.
///
/// Security: the API key is sent ONLY via the "x-api-key" header, never in
/// the URL.
/// </summary>
public class AnthropicAiDraftProvider(IHttpClientFactory httpClientFactory) : IAiDraftProvider
{
    public const string MessagesUrl = "https://api.anthropic.com/v1/messages";
    public const string AnthropicVersion = "2023-06-01";
    public const string ApiKeyHeader = "x-api-key";

    public string ProviderName => "anthropic";
    public string ApiKeyEnvVar => "ANTHROPIC_API_KEY";

    public async Task<string> GenerateAsync(
        string systemPrompt, string userPrompt, string model,
        int maxTokens, double temperature, string apiKey, CancellationToken ct)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model,
            max_tokens = maxTokens,
            temperature,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userPrompt } },
        });

        var client = httpClientFactory.CreateClient(CephAiDraftService.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, MessagesUrl);
        request.Headers.Add(ApiKeyHeader, apiKey); // header only — never the URL
        request.Headers.Add("anthropic-version", AnthropicVersion);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new CephAiUpstreamException($"HTTP {(int)response.StatusCode} from AI API");

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var text = root.TryGetProperty("content", out var contentProp)
                   && contentProp.ValueKind == JsonValueKind.Array
                   && contentProp.GetArrayLength() > 0
                   && contentProp[0].TryGetProperty("text", out var textProp)
            ? textProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(text))
            throw new CephAiUpstreamException("empty_ai_response");

        return text;
    }
}
