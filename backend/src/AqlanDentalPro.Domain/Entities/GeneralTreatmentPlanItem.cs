namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Individual item in a general dentistry treatment plan.
/// </summary>
public class GeneralTreatmentPlanItem : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? ToothNumber { get; set; }
    public string Treatment { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium"; // high, medium, low
    public string Status { get; set; } = "planned";  // planned, in_progress, completed, cancelled
    public decimal? EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? DoctorId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
