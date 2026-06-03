namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 5 — Tracks lab vendor payables (amounts owed to labs).
/// Created automatically when a lab order with cost is sent/approved.
/// </summary>
public class LabPayable : BaseEntity
{
    public Guid LabOrderId { get; set; }
    public Guid LabId { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "pending"; // pending, partial, paid
    public string? Notes { get; set; }

    // Navigation
    public LabOrder LabOrder { get; set; } = null!;
    public Lab Lab { get; set; } = null!;
}
