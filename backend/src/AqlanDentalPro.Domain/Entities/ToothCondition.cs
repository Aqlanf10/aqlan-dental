namespace AqlanDentalPro.Domain.Entities;

public class ToothCondition : BaseEntity
{
    public Guid ChartId { get; set; }
    public string ToothNumber { get; set; } = string.Empty; // FDI: 11-48
    public string? Condition { get; set; }
    public string? SurfacesAffected { get; set; }
    public string? Notes { get; set; }
    public string? TreatmentDone { get; set; }

    public DentalChart Chart { get; set; } = null!;
}
