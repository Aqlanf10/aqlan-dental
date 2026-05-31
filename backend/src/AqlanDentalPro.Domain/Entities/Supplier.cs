using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A supplier/vendor that provides dental materials, equipment, or lab services.
/// Linked to purchase orders and supplier bills for payables tracking.
/// </summary>
public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Classification of the supplier type (dental lab, medical vendor, general).</summary>
    public SupplierType Type { get; set; } = SupplierType.MedicalVendor;

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }

    /// <summary>Running balance of unpaid supplier bills (positive = clinic owes supplier).</summary>
    public decimal Balance { get; set; } = 0m;

    // Navigation
    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
    public ICollection<SupplierBill> Bills { get; set; } = [];
}
