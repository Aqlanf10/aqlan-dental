namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>Per-landmark refinement target passed to the provider.</summary>
public sealed record CephLandmarkRefineTarget(
    string Key,
    double XNormalized,
    double YNormalized);

/// <summary>
/// Precision mode for the AI landmark draft.
/// <list type="bullet">
/// <item><c>draft</c> — fast first-pass; default. Returns every defensible landmark.</item>
/// <item><c>high</c> — slower, deliberate pass; the model is asked to cross-check
/// each landmark against its anatomical references and to omit anything whose
/// confidence it cannot push above 0.5.</item>
/// </list>
/// </summary>
public enum CephAiPrecision
{
    Draft = 0,
    High = 1,
}

/// <summary>
/// Produces an unsaved AI landmark draft from a lateral cephalogram.
/// Coordinates are normalized to the Gemini vision range 0..1000.
/// </summary>
public interface ICephLandmarkDraftProvider
{
    string ProviderName { get; }
    string ApiKeyEnvVar { get; }

    Task<IReadOnlyList<CephAiLandmarkPoint>> GenerateAsync(
        byte[] imageBytes,
        string mimeType,
        string model,
        string apiKey,
        CephAiPrecision precision,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refine the position of a single landmark. The provider is given the full
    /// image plus the current normalized coordinates so it can re-evaluate just
    /// that landmark against the surrounding anatomy. Returns at most one point
    /// (null when the model declines to refine, e.g. confidence too low).
    /// </summary>
    Task<CephAiLandmarkPoint?> RefineAsync(
        byte[] imageBytes,
        string mimeType,
        string model,
        string apiKey,
        CephLandmarkRefineTarget target,
        CancellationToken cancellationToken);
}

/// <summary>
/// One AI-proposed landmark. <see cref="Reasoning"/> is an optional short
/// Arabic/English note explaining WHY the model placed the point there — the
/// orthodontist can read it in the UI before accepting or moving the point.
/// </summary>
public sealed record CephAiLandmarkPoint(
    string Key,
    double XNormalized,
    double YNormalized,
    double? Confidence,
    string? Reasoning = null);
