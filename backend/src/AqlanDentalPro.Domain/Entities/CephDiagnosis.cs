namespace AqlanDentalPro.Domain.Entities;

public class CephDiagnosis : BaseEntity
{
    public Guid AnalysisId { get; set; }
    public string? SkeletalClass { get; set; }
    public string? VerticalPattern { get; set; }
    public string? IncisorInclination { get; set; }
    public string? SoftTissueSummary { get; set; }
    public string? AiRecommendation { get; set; }
    public bool DoctorApproved { get; set; } = false;
    public string? FinalDiagnosis { get; set; }

    public CephAnalysis Analysis { get; set; } = null!;
}
