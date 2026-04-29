namespace AqlanDentalPro.Domain.Entities;

public class LabOrder : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? OrthoCaseId { get; set; }
    public string? OrderNumber { get; set; }
    public string? ApplianceType { get; set; }
    public string? LabName { get; set; }
    public DateOnly? SentDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public string Status { get; set; } = "sent";
    public string Priority { get; set; } = "normal";
    public string? Instructions { get; set; }
    public decimal? Cost { get; set; }
    public Guid? DoctorId { get; set; }

    public Patient Patient { get; set; } = null!;
    public OrthoCase? OrthoCase { get; set; }
    public Doctor? Doctor { get; set; }
}
