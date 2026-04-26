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

    public CephAnalysis Analysis { get; set; } = null!;
}
