using AqlanDentalPro.Domain.Enums;

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

    /// <summary>
    /// Type-safe accessor for Status — converts the stored string to <see cref="LabOrderStatus"/> enum.
    /// Returns null if the stored string doesn't match any enum value.
    /// The underlying database column remains string for backward compatibility.
    /// </summary>
    public LabOrderStatus? StatusEnum => Enum.TryParse<LabOrderStatus>(Status, ignoreCase: true, out var s) ? s : null;

    /// <summary>
    /// Type-safe accessor for Priority — converts the stored string to <see cref="LabOrderPriority"/> enum.
    /// Returns null if the stored string doesn't match any enum value.
    /// </summary>
    public LabOrderPriority? PriorityEnum => Enum.TryParse<LabOrderPriority>(Priority, ignoreCase: true, out var p) ? p : null;
    public string? Instructions { get; set; }
    public decimal? Cost { get; set; }
    public string Currency { get; set; } = "YER";
    public decimal ExchangeRateToYer { get; set; } = 1m;
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

    // Lab Sprint 4 — Remake/Return fields
    public string? RemakeReason { get; set; }
    public string? ReturnReason { get; set; }
    public decimal? RemakeCost { get; set; }
    public bool IsFreeRemake { get; set; }
    public Guid? OriginalOrderId { get; set; }
    public int RemakeCount { get; set; }

    public Patient Patient { get; set; } = null!;
    public OrthoCase? OrthoCase { get; set; }
    public Doctor? Doctor { get; set; }
    public Visit? Visit { get; set; }
    public Lab? Lab { get; set; }

    // Lab Sprint 3 — Navigation to order items
    public ICollection<LabOrderItem> Items { get; set; } = [];

    // Lab Sprint 4 — Navigation to history and attachments
    public ICollection<LabOrderStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<LabOrderAttachment> Attachments { get; set; } = [];

    // Lab Sprint 4 — Navigation to original order (for remakes)
    public LabOrder? OriginalOrder { get; set; }
}
