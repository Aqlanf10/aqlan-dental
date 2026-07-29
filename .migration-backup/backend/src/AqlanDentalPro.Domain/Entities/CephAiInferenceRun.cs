namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Append-oriented record of one landmark inference attempt. Successful output is
/// the original provider response in normalized coordinates, before doctor edits.
/// No patient demographics, file path, or image bytes are stored here.
/// </summary>
public sealed class CephAiInferenceRun : BaseEntity
{
    public Guid AnalysisId { get; set; }
    public Guid ModelVersionId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Precision { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string ImageSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "running";
    public string? FailureCode { get; set; }
    public string? OriginalPredictionsJson { get; set; }
    public string? OutputSha256 { get; set; }
    public Guid? TriggeredByUserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public CephAnalysis Analysis { get; set; } = null!;
    public CephAiModelVersion ModelVersion { get; set; } = null!;
}
