namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A supplier/vendor that provides dental materials and equipment.
/// Linked to purchase orders for procurement tracking.
/// </summary>
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public ICollection<SupplierBill> Bills { get; set; } = [];
}
