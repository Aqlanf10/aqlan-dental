namespace AqlanDentalPro.Application.DTOs.Finance;

public class ExpenseDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ExpenseDate { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? CategoryAr { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentMethodAr { get; set; }
    public string? Recipient { get; set; }
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateExpenseRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? ExpenseDate { get; set; }
    public string? Category { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Recipient { get; set; }
    public string? Notes { get; set; }
}

public class UpdateExpenseRequest
{
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? ExpenseDate { get; set; }
    public string? Category { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Recipient { get; set; }
    public string? Notes { get; set; }
}

public class ExpenseSummaryDto
{
    public decimal TodayExpenses { get; set; }
    public decimal MonthExpenses { get; set; }
    public decimal TotalExpenses { get; set; }
    public int ExpenseCount { get; set; }
    public List<ExpenseByCategoryDto> ByCategory { get; set; } = [];
}

public class ExpenseByCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public string CategoryAr { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public int Count { get; set; }
}
