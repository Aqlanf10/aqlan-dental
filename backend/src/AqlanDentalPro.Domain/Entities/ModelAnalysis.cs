namespace AqlanDentalPro.Domain.Entities;

public class ModelAnalysis : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public DateOnly? AnalysisDate { get; set; }
    public decimal? BoltonOverall { get; set; }
    public decimal? BoltonAnterior { get; set; }
    public decimal? UpperSum12 { get; set; }
    public decimal? LowerSum12 { get; set; }
    public decimal? UpperArchLength { get; set; }
    public decimal? LowerArchLength { get; set; }
    public decimal? UpperAld { get; set; }
    public decimal? LowerAld { get; set; }
    public decimal? PontIndex { get; set; }
    public string DentitionStage { get; set; } = "Permanent";
    public string AnalysisVersion { get; set; } = "2.0";
    public string InputDataJson { get; set; } = "{}";
    public string ResultDataJson { get; set; } = "{}";
    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
}
