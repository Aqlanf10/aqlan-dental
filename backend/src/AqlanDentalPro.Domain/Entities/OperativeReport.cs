namespace AqlanDentalPro.Domain.Entities;

public class OperativeReport : BaseEntity
{
    public Guid SurgeryCaseId { get; set; }
    public DateTime? SurgeryDateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? AnesthesiaUsed { get; set; }
    public string? Technique { get; set; }
    public string? DetailedDescription { get; set; }
    public string? Outcome { get; set; }
    public string? Complications { get; set; }
    public int? SuturesCount { get; set; }
    public bool SpecimenSent { get; set; } = false;
    public Guid? DoctorId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public SurgeryCase SurgeryCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
