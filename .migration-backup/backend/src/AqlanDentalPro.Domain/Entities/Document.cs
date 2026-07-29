namespace AqlanDentalPro.Domain.Entities;

public class Document : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? DocumentType { get; set; }
    public string? Title { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public string? Notes { get; set; }
    public Guid? UploadedBy { get; set; }
    public bool Signed { get; set; } = false;
    public DateTime? SignedAt { get; set; }
    /// <summary>Optional link to an orthodontic case (standardized records — Phase 2).</summary>
    public Guid? OrthoCaseId { get; set; }

    public Patient Patient { get; set; } = null!;
    public OrthoCase? OrthoCase { get; set; }
}
