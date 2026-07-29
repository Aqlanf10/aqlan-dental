namespace AqlanDentalPro.Domain.Entities;

public class Inventory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; } = 0;
    public int MinQuantity { get; set; } = 0;
    public string? Unit { get; set; }
    public decimal? CostPerUnit { get; set; }
    public Guid? BranchId { get; set; }

    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public Guid? DefaultSupplierId { get; set; }
    public Supplier? DefaultSupplier { get; set; }

    // ── Sprint 4 (YOLO-S4) inventory enhancements ───────────────────────────
    // All nullable → no behavior change for existing inventory rows.
    // MinStockLevel is a parallel decimal threshold to MinQuantity (int) — kept
    // distinct so existing low-stock logic (Quantity <= MinQuantity) is unchanged.
    public decimal? MinStockLevel { get; set; }
    public string? PurchaseUnit { get; set; }
    public string? ConsumptionUnit { get; set; }
    public string? ImageUrl { get; set; }
    public string? WarehouseLocation { get; set; }

    public Branch? Branch { get; set; }
    public ICollection<InventoryAdjustment> Adjustments { get; set; } = [];
}
