using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A purchase order sent to a supplier for dental materials/equipment.
/// Created as Draft, then submitted and tracked through receiving.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    /// <summary>Auto-generated order number (e.g. PO-20260602-001).</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Current lifecycle status of the purchase order.</summary>
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public DateOnly? ReceivedDate { get; set; }

    /// <summary>Sum of line item totals before tax.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Tax amount applied.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Final total = Subtotal + TaxAmount.</summary>
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public Guid? CreatedBy { get; set; }

    // Navigation
    public ICollection<PurchaseOrderLineItem> LineItems { get; set; } = [];
}
