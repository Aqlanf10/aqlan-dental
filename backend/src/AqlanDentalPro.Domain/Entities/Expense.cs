namespace AqlanDentalPro.Domain.Entities;

public class Expense : BaseEntity
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string? Category { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Recipient { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? RecordedBy { get; set; }
    public string? Notes { get; set; }

    public Branch? Branch { get; set; }
}
