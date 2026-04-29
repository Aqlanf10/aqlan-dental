namespace AqlanDentalPro.Domain.Entities;

public class InternalReferral : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid FromDoctorId { get; set; }
    public Guid ToDoctorId { get; set; }
    public string? Reason { get; set; }
    public string Priority { get; set; } = "normal";
    public string? Notes { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? AcceptedAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor FromDoctor { get; set; } = null!;
    public Doctor ToDoctor { get; set; } = null!;
}
