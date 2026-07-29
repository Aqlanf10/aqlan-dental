namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 2 — Catalog of lab work types (e.g., Crown, Bridge, Veneer).
/// Seeded with default values; admin can add/edit/deactivate.
/// </summary>
public class LabWorkType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? Category { get; set; }
    public int SortOrder { get; set; }

    // Navigation — LabOrderItem references WorkTypeId FK
    public ICollection<LabOrderItem> LabOrderItems { get; set; } = [];
}
