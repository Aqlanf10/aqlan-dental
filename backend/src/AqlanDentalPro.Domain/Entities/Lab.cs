namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Lab Sprint 2 — Represents a dental laboratory vendor.
/// Replaces free-text LabName on LabOrder with a proper entity reference.
/// LabId on LabOrder is nullable to maintain backward compatibility.
/// </summary>
public class Lab : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public Guid? BranchId { get; set; }

    // Navigation
    public Branch? Branch { get; set; }
    public ICollection<LabOrder> LabOrders { get; set; } = [];
}
