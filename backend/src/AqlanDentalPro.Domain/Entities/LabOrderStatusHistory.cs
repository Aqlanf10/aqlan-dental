namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 4 — Status change history for lab orders.
/// Tracks every status transition with who changed it and why.
/// </summary>
public class LabOrderStatusHistory : BaseEntity
{
    public Guid LabOrderId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ChangedByUserId { get; set; }
    public string? Reason { get; set; }

    // Navigation
    public LabOrder LabOrder { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
