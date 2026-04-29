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

    public Branch? Branch { get; set; }
}
