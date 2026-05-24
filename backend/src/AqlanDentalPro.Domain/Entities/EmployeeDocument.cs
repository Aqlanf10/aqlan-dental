namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Sprint 15 — Employee document (مستندات الموظف).
/// Tracks uploaded documents like contracts, certificates, ID copies, etc.
/// </summary>
public class EmployeeDocument : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? FileSize { get; set; }
    public Guid UploadedBy { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
}
