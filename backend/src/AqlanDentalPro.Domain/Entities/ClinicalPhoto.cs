namespace AqlanDentalPro.Domain.Entities;

public class ClinicalPhoto : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? OrthoCaseId { get; set; }
    public DateOnly PhotoDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Category { get; set; } // extraoral, intraoral
    public string? PhotoType { get; set; } // frontal_relaxed, frontal_smile, etc.
    public string FileUrl { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Stage { get; set; } // initial, progress, final
    public string? Notes { get; set; }
    public Guid? UploadedBy { get; set; }

    public Patient Patient { get; set; } = null!;
    public OrthoCase? OrthoCase { get; set; }
}
