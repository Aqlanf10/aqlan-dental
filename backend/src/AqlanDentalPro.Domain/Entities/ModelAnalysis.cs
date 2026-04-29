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
    public string? Notes { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
}
