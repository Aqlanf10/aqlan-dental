using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Claude (Anthropic) multimodal landmark draft provider — the vision-capable
/// counterpart to GeminiCephLandmarkDraftProvider, giving clinics configured
/// on Anthropic the same ceph auto-trace assistive draft. This is an
/// assistive first pass, not a clinically validated automatic tracing model;
/// the caller must keep the result unsaved until an orthodontist reviews and
/// adjusts every point.
///
/// Security: the API key is sent ONLY via the "x-api-key" header, never in
/// the URL.
/// </summary>
public sealed class AnthropicCephLandmarkDraftProvider(
    IHttpClientFactory httpClientFactory) : ICephLandmarkDraftProvider
{
    public string ProviderName => "anthropic";
    public string ApiKeyEnvVar => "ANTHROPIC_API_KEY";

    public async Task<IReadOnlyList<CephAiLandmarkPoint>> GenerateAsync(
        byte[] imageBytes,
        string mimeType,
        string model,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model,
            max_tokens = 3000,
            temperature = 0.0,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = mimeType,
                                data = Convert.ToBase64String(imageBytes),
                            },
                        },
                        new { type = "text", text = CephLandmarkPromptShared.BuildPrompt() },
                    },
                },
            },
        });

        var client = httpClientFactory.CreateClient(CephAiDraftService.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicAiDraftProvider.MessagesUrl);
        request.Headers.Add(AnthropicAiDraftProvider.ApiKeyHeader, apiKey); // header only — never the URL
        request.Headers.Add("anthropic-version", AnthropicAiDraftProvider.AnthropicVersion);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new CephAiUpstreamException($"HTTP {(int)response.StatusCode} from AI API");

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var generatedText = ExtractGeneratedText(responseJson);
        var points = CephLandmarkPromptShared.ParsePoints(generatedText);
        if (points.Count < 8)
            throw new CephAiUpstreamException("insufficient_landmarks");

        return points;
    }

    private static string ExtractGeneratedText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("text", out var textElement)
                    && !string.IsNullOrWhiteSpace(textElement.GetString()))
                {
                    return textElement.GetString()!;
                }
            }
        }

        throw new CephAiUpstreamException("empty_ai_response");
    }
}
