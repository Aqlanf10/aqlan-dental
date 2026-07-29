using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Google Gemini provider for the Ceph C-D draft-diagnosis assistant.
/// Raw HttpClient against the generateContent REST endpoint — no SDK package.
///
/// Security: the API key is sent ONLY via the "x-goog-api-key" header, never
/// as a query-string parameter (URLs must never carry secrets — they end up
/// in logs and proxies).
/// </summary>
public class GeminiAiDraftProvider(IHttpClientFactory httpClientFactory) : IAiDraftProvider
{
    public const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    public const string ApiKeyHeader = "x-goog-api-key";

    public string ProviderName => "gemini";
    public string ApiKeyEnvVar => "GEMINI_API_KEY";

    public async Task<string> GenerateAsync(
        string systemPrompt, string userPrompt, string model,
        int maxTokens, double temperature, string apiKey, CancellationToken ct)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new { maxOutputTokens = maxTokens, temperature },
        });

        var client = httpClientFactory.CreateClient(CephAiDraftService.HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{BaseUrl}/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add(ApiKeyHeader, apiKey); // header only — never the URL
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new CephAiUpstreamException($"HTTP {(int)response.StatusCode} from AI API");

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(responseJson);

        // candidates[0].content.parts[0].text
        string? text = null;
        if (doc.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array
            && parts.GetArrayLength() > 0
            && parts[0].TryGetProperty("text", out var textProp))
        {
            text = textProp.GetString();
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new CephAiUpstreamException("empty_ai_response");

        return text;
    }
}
