namespace AqlanDentalPro.Application.Interfaces.Services;

public sealed record CephAiLandmarkPoint(
    string Key,
    double XNormalized,
    double YNormalized,
    double? Confidence);

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
        CancellationToken cancellationToken);
}
