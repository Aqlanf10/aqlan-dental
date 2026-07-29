using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 3 — Pricing per lab per work type.
/// Unique index on (LabId, WorkTypeId) ensures one price entry per combination.
/// </summary>
public class LabWorkPrice : BaseEntity
{
    public Guid LabId { get; set; }
    public Guid WorkTypeId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? UrgentSurcharge { get; set; }
    public string? UrgentSurchargeType { get; set; } // "fixed" or "percentage"
    public int? EstimatedDays { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Lab Lab { get; set; } = null!;
    public LabWorkType WorkType { get; set; } = null!;
}
