using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Anthropic (Claude) multimodal landmark draft provider — the vision counterpart
/// of <see cref="AnthropicAiDraftProvider"/> (which does text-only Ceph diagnosis
/// drafts). Gives the clinic ceph auto-trace + refinement parity: the feature now
/// works whether the configured <c>ai.provider</c> is <c>gemini</c> OR <c>anthropic</c>,
/// exactly like the text-drafting features already do.
///
/// Same safety contract as the Gemini provider: assistive first pass only, never a
/// clinically validated model — the caller keeps the result UNSAVED until an
/// orthodontist reviews and adjusts every point. Prompts + parsing are the shared
/// <see cref="CephLandmarkDraftShared"/> logic, so both providers draw to identical
/// anatomical definitions.
///
/// Security: the API key is sent ONLY via the "x-api-key" header, never in the URL.
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
        CephAiPrecision precision,
        CancellationToken cancellationToken)
    {
        // Draft mode: deterministic. High-precision mode: a hair of temperature lets
        // the model reconsider borderline landmarks without hallucinating anatomy
        // (mirrors the Gemini provider's 0.0 / 0.1 split).
        var requestBody = BuildRequest(
            imageBytes, mimeType, model,
            CephLandmarkDraftShared.BuildPrompt(precision),
            maxTokens: precision == CephAiPrecision.High ? 5000 : 3000,
            temperature: precision == CephAiPrecision.High ? 0.1 : 0.0);

        var generatedText = await CallAnthropicAsync(requestBody, apiKey, cancellationToken);
        var points = CephLandmarkDraftShared.ParsePoints(generatedText);

        // In high-precision mode, drop anything the model itself rates below 0.5 —
        // the orthodontist asked for a careful pass, so an uncertain guess is worse
        // than an explicit "place it yourself".
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

        var requestBody = BuildRequest(
            imageBytes, mimeType, model,
            CephLandmarkDraftShared.BuildRefinePrompt(target),
            maxTokens: 800,
            temperature: 0.0);

        var generatedText = await CallAnthropicAsync(requestBody, apiKey, cancellationToken);
        var points = CephLandmarkDraftShared.ParsePoints(generatedText);
        // The refine prompt asks for exactly one landmark; keep only the one we
        // asked for (the model occasionally echoes a neighbor by mistake).
        return points.FirstOrDefault(p => p.Key == target.Key);
    }

    private static string BuildRequest(
        byte[] imageBytes, string mimeType, string model, string prompt, int maxTokens, double temperature) =>
        JsonSerializer.Serialize(new
        {
            model,
            max_tokens = maxTokens,
            temperature,
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
                                media_type = NormalizeMediaType(mimeType),
                                data = Convert.ToBase64String(imageBytes),
                            },
                        },
                        new { type = "text", text = prompt },
                    },
                },
            },
        });

    private async Task<string> CallAnthropicAsync(
        string requestBody, string apiKey, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(CephAiDraftService.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicAiDraftProvider.MessagesUrl);
        request.Headers.Add(AnthropicAiDraftProvider.ApiKeyHeader, apiKey); // header only — never the URL
        request.Headers.Add("anthropic-version", AnthropicAiDraftProvider.AnthropicVersion);
        request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new CephAiUpstreamException($"HTTP {(int)response.StatusCode} from AI API");

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        return ExtractGeneratedText(responseJson);
    }

    /// <summary>
    /// Anthropic accepts only image/jpeg|png|gif|webp. The uploads pipeline stores
    /// "image/jpg" for some files — remap it to the canonical "image/jpeg".
    /// </summary>
    private static string NormalizeMediaType(string mimeType) =>
        mimeType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : mimeType;

    private static string ExtractGeneratedText(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        if (root.TryGetProperty("content", out var content)
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
