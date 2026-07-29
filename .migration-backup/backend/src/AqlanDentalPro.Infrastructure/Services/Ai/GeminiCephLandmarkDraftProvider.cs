using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Gemini multimodal landmark draft provider. This is an assistive first pass,
/// not a clinically validated automatic tracing model. The caller must keep
/// the result unsaved until an orthodontist reviews and adjusts every point.
/// </summary>
public sealed class GeminiCephLandmarkDraftProvider(
    IHttpClientFactory httpClientFactory) : ICephLandmarkDraftProvider
{
    public string ProviderName => "gemini";
    public string ApiKeyEnvVar => "GEMINI_API_KEY";

    public async Task<IReadOnlyList<CephAiLandmarkPoint>> GenerateAsync(
        byte[] imageBytes,
        string mimeType,
        string model,
        string apiKey,
        CephAiPrecision precision,
        CancellationToken cancellationToken)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = Convert.ToBase64String(imageBytes),
                            },
                        },
                        new { text = CephLandmarkDraftShared.BuildPrompt(precision) },
                    },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                // Draft mode: deterministic and fast. High-precision mode: tiny
                // amount of temperature lets the model reconsider borderline
                // landmarks without hallucinating anatomy.
                temperature = precision == CephAiPrecision.High ? 0.1 : 0.0,
                maxOutputTokens = precision == CephAiPrecision.High ? 5000 : 3000,
            },
        });

        var generatedText = await CallGeminiAsync(requestBody, model, apiKey, cancellationToken);
        var points = CephLandmarkDraftShared.ParsePoints(generatedText);

        // In high-precision mode, drop anything the model itself rates below
        // 0.5 — the orthodontist asked for a careful pass, so an uncertain
        // guess is worse than an explicit "I can't tell, place it yourself".
        if (precision == CephAiPrecision.High)
            points = points.Where(p => (p.Confidence ?? 0) >= 0.5).ToList();

        if (points.Count < 8)
            throw new CephAiUpstreamException("insufficient_landmarks");

        return points;
    }

    public async Task<CephAiLandmarkPoint?> RefineAsync(
        byte[] imageBytes,
        string mimeType,
        string model,
        string apiKey,
        CephLandmarkRefineTarget target,
        CancellationToken cancellationToken)
    {
        if (!CephLandmarkDraftShared.AllowedKeys.Contains(target.Key))
            throw new ArgumentException("invalid_landmark_key");

        var requestBody = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = Convert.ToBase64String(imageBytes),
                            },
                        },
                        new { text = CephLandmarkDraftShared.BuildRefinePrompt(target) },
                    },
                },
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.0,
                maxOutputTokens = 800,
            },
        });

        var generatedText = await CallGeminiAsync(requestBody, model, apiKey, cancellationToken);
        var points = CephLandmarkDraftShared.ParsePoints(generatedText);
        // The refine prompt asks for exactly one landmark; keep only the one we
        // asked for (the model occasionally echoes a neighbor by mistake).
        var match = points.FirstOrDefault(p => p.Key == target.Key);
        return match;
    }

    private async Task<string> CallGeminiAsync(
        string requestBody, string model, string apiKey, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(CephAiDraftService.HttpClientName);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{GeminiAiDraftProvider.BaseUrl}/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add(GeminiAiDraftProvider.ApiKeyHeader, apiKey);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new CephAiUpstreamException($"HTTP {(int)response.StatusCode} from AI API");

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractGeneratedText(responseJson);
    }

    private static string ExtractGeneratedText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.TryGetProperty("candidates", out var candidates)
            && candidates.ValueKind == JsonValueKind.Array
            && candidates.GetArrayLength() > 0
            && candidates[0].TryGetProperty("content", out var content)
            && content.TryGetProperty("parts", out var parts)
            && parts.ValueKind == JsonValueKind.Array)
        {
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement)
                    && !string.IsNullOrWhiteSpace(textElement.GetString()))
                {
                    return textElement.GetString()!;
                }
            }
        }

        throw new CephAiUpstreamException("empty_ai_response");
    }
}
