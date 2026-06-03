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

    // Sprint 2 — Daily Operations: extended fields
    public string? Shade { get; set; }
    public string? RestorationType { get; set; }
    public Guid? VisitId { get; set; }
    public DateOnly? DeliveredDate { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? BranchId { get; set; }

    // Lab Sprint 2 — Lab entity reference (nullable for backward compatibility with free-text LabName)
    public Guid? LabId { get; set; }

    // Lab Sprint 3 — Professional order fields
    public decimal? TotalCost { get; set; }
    public Guid? InvoiceLineItemId { get; set; }

    public Patient Patient { get; set; } = null!;
    public OrthoCase? OrthoCase { get; set; }
    public Doctor? Doctor { get; set; }
    public Visit? Visit { get; set; }
    public Lab? Lab { get; set; }

    // Lab Sprint 3 — Navigation to order items
    public ICollection<LabOrderItem> Items { get; set; } = [];
}
