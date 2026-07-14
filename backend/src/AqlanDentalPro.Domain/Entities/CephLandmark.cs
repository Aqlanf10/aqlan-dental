namespace AqlanDentalPro.Domain.Entities;

public class CephLandmark : BaseEntity
{
    public Guid AnalysisId { get; set; }
    public string LandmarkKey { get; set; } = string.Empty;
    public string? LandmarkName { get; set; }
    public decimal? XCoord { get; set; }
    public decimal? YCoord { get; set; }
    public bool IsAiPlaced { get; set; } = false;
    public decimal? Confidence { get; set; }
    public string PlacementSource { get; set; } = "manual";
    public string? SourceLandmarkKey { get; set; }
    public string? SourceModelId { get; set; }
    public string? Reasoning { get; set; }
    public decimal? AiProposalXCoord { get; set; }
    public decimal? AiProposalYCoord { get; set; }
    public bool IsReviewed { get; set; }
    public decimal? ReviewErrorMm { get; set; }

    public CephAnalysis Analysis { get; set; } = null!;
}
