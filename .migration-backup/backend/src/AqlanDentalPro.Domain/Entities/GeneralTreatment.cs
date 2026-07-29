namespace AqlanDentalPro.Domain.Entities;

public class GeneralTreatment : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? VisitId { get; set; }
    public string TreatmentType { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public string? MaterialUsed { get; set; }
    public string? AnesthesiaType { get; set; }
    public decimal? Cost { get; set; }
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }

    public Patient Patient { get; set; } = null!;
    public Visit? Visit { get; set; }
    public Doctor? Doctor { get; set; }
}
