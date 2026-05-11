namespace AqlanDentalPro.Domain.Entities;

public class Radiograph : BaseEntity
{
    public Guid PatientId { get; set; }
    public DateOnly XrayDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? XrayType { get; set; } // OPG, lateral_ceph, PA_ceph, bitewing, periapical, CBCT
    public string FileUrl { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? ToothRelated { get; set; }
    public string? Notes { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? UploadedBy { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
