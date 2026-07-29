using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 3 — Individual line items within a lab order.
/// Each item represents a specific work type with tooth, shade, and pricing details.
/// </summary>
public class LabOrderItem : BaseEntity
{
    public Guid LabOrderId { get; set; }
    public Guid WorkTypeId { get; set; }
    public string? ToothNumber { get; set; }
    public string? Arch { get; set; } // "upper", "lower", "both"
    public string? Shade { get; set; }
    public string? RestorationType { get; set; }
    public int UnitsCount { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Instructions { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public LabOrder LabOrder { get; set; } = null!;
    public LabWorkType WorkType { get; set; } = null!;
}
