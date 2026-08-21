namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Tracks all quantity changes to inventory items.
/// Created automatically on purchase order receiving or manual adjustments.
/// </summary>
public class InventoryAdjustment : BaseEntity
{
    public Guid InventoryItemId { get; set; }
    public Inventory? InventoryItem { get; set; }

    /// <summary>Quantity before the adjustment.</summary>
    public int PreviousQuantity { get; set; }

    /// <summary>Quantity after the adjustment.</summary>
    public int NewQuantity { get; set; }

    /// <summary>Change amount (can be positive or negative).</summary>
    public int Delta { get; set; }

    /// <summary>Reason for the adjustment.</summary>
    public string? Reason { get; set; }

    /// <summary>Type of adjustment: manual, purchase, correction, consumption.</summary>
    public string AdjustmentType { get; set; } = "manual";

    /// <summary>Linked purchase order line item (if adjustment was from receiving).</summary>
    public Guid? PurchaseOrderLineItemId { get; set; }

    /// <summary>
    /// LABINV-REQ-011 — the lab order this consumption belongs to, when it belongs to one.
    ///
    /// <para>
    /// Nullable and unenforced by design, exactly like <see cref="PurchaseOrderLineItemId"/>:
    /// every adjustment already in the table predates this column and none of them is wrong
    /// for having no order.
    /// </para>
    /// <para>
    /// This is a link, not a cost. Materials the clinic consumes for a case are the clinic's
    /// own cost, not part of what it owes the lab, so nothing here is ever added to
    /// <c>LabOrder.TotalCost</c> — that would inflate both the supplier bill and the lab-cost
    /// deduction inside the doctor's commission.
    /// </para>
    /// </summary>
    public Guid? LabOrderId { get; set; }

    /// <summary>User who made the adjustment.</summary>
    public Guid? AdjustedBy { get; set; }
}
