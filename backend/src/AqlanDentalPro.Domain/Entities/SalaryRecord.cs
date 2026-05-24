namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Sprint 15 — Monthly salary record for employees.
/// Tracks base salary, deductions, advances, and net salary per month.
/// </summary>
public class SalaryRecord : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Deductions { get; set; }
    public decimal Advances { get; set; }
    public decimal Bonuses { get; set; }
    public decimal NetSalary { get; set; }
    public DateTime? PaidAt { get; set; }
    public Guid? PaidBy { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public Employee Employee { get; set; } = null!;
}
