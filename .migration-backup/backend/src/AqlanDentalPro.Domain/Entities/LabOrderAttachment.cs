namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 4 — File attachments for lab orders.
/// Supports photos, STL files, shade photos, impression photos, and PDF instructions.
/// </summary>
public class LabOrderAttachment : BaseEntity
{
    public Guid LabOrderId { get; set; }
    public Guid? LabOrderItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Category { get; set; } = string.Empty; // "photo", "stl", "shade-photo", "impression-photo", "pdf-instructions"
    public string StoragePath { get; set; } = string.Empty;
    public Guid? UploadedBy { get; set; }

    // Navigation
    public LabOrder LabOrder { get; set; } = null!;
    public LabOrderItem? LabOrderItem { get; set; }
    public User? UploadedByUser { get; set; }
}
