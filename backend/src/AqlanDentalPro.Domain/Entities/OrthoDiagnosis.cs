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
    public string? Summary { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
}
