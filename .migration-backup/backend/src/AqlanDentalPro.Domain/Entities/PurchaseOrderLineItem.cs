namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A single line item on a purchase order.
/// Can optionally link to an inventory item for automatic stock updates on receiving.
/// </summary>
public class PurchaseOrderLineItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>Linked inventory item (optional — for auto stock updates).</summary>
    public Guid? InventoryItemId { get; set; }
    public Inventory? InventoryItem { get; set; }

    /// <summary>Name of the item at time of order creation.</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>Description of the line item.</summary>
    public string? ItemDescription { get; set; }

    /// <summary>Ordered quantity.</summary>
    public int Quantity { get; set; }

    /// <summary>Quantity received so far.</summary>
    public int ReceivedQuantity { get; set; }

    /// <summary>Unit cost at time of ordering.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>Total price = Quantity * UnitCost.</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; }
}
