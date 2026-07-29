namespace AqlanDentalPro.Domain.Entities;

/// <summary>Auditable accounting period. Closed periods reject all new journal postings.</summary>
public class AccountingPeriod : BaseEntity
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public string? Notes { get; set; }

    public Branch? Branch { get; set; }
    public User? ClosedByUser { get; set; }
}
