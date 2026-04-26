namespace AqlanDentalPro.Domain.Entities;

public class Document : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? DocumentType { get; set; }
    public string? Title { get; set; }
    public string? FileUrl { get; set; }
    public bool Signed { get; set; } = false;
    public DateTime? SignedAt { get; set; }

    public Patient Patient { get; set; } = null!;
}
