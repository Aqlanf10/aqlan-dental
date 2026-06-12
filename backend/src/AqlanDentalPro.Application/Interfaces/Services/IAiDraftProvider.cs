namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// One LLM provider for the Ceph C-D draft-diagnosis assistant (gemini,
/// anthropic, …). Implementations live in Infrastructure/Services/Ai and use
/// the raw named HttpClient — no SDK packages.
///
/// Hard security rule for every implementation: the API key travels ONLY in a
/// request header — never in the URL/query string (URLs end up in logs).
/// </summary>
public interface IAiDraftProvider
{
    /// <summary>Provider key as stored in the Settings row "ai.provider" (e.g. "gemini").</summary>
    string ProviderName { get; }

    /// <summary>Environment variable holding the API key (e.g. "GEMINI_API_KEY").</summary>
    string ApiKeyEnvVar { get; }

    /// <summary>
    /// Calls the provider's API and returns the generated text.
    /// Throws CephAiUpstreamException (short reason, no secrets) on a
    /// non-success HTTP status or an empty/unusable response body.
    /// </summary>
    Task<string> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        int maxTokens,
        double temperature,
        string apiKey,
        CancellationToken ct);
}
