namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Appointment card imported from the legacy desktop system for historical reference only.
/// It must not appear in the live scheduling or queue workflow.
/// </summary>
public class LegacyAppointmentArchive : BaseEntity
{
    public Guid PatientId { get; set; }
    public string SourceSystem { get; set; } = "Dent2026";
    public string SourceAppointmentId { get; set; } = string.Empty;
    public string? LegacyFileNumber { get; set; }
    public DateTime? AppointmentAt { get; set; }
    public string? ArchiveType { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public Patient Patient { get; set; } = null!;
}
