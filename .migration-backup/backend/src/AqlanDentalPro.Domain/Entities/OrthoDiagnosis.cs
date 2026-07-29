namespace AqlanDentalPro.Domain.Entities;

public class OrthoDiagnosis : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public string? SkeletalClassification { get; set; }
    public string? DentalClassification { get; set; }
    public string? FacialPattern { get; set; }
    public decimal? ANB { get; set; }
    public decimal? Wits { get; set; }
    public decimal? FMA { get; set; }
    public decimal? SNA { get; set; }
    public decimal? SNB { get; set; }
    public decimal? IMPA { get; set; }
    public string? SoftTissueDiagnosis { get; set; }
    public string? FunctionalDiagnosis { get; set; }
    public string? Etiology { get; set; }
    public string? Summary { get; set; }
    public Guid? CephSourceAnalysisId { get; set; }
    public DateTime? CephSyncedAt { get; set; }
    public string? PhotoAnalysisSummary { get; set; }
    public Guid? ProfileSourceAnalysisId { get; set; }
    public Guid? FrontalSourceAnalysisId { get; set; }
    public DateTime? PhotoAnalysisSyncedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public CephAnalysis? CephSourceAnalysis { get; set; }
    public PhotoAnalysis? ProfileSourceAnalysis { get; set; }
    public PhotoAnalysis? FrontalSourceAnalysis { get; set; }
    public Doctor? ApprovedByDoctor { get; set; }
}
