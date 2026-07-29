namespace AqlanDentalPro.Domain.Entities;

public class Employee : BaseEntity
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public string? Position { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime? HireDate { get; set; }
    public decimal? BaseSalary { get; set; }
    public string? EmergencyContact { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? Notes { get; set; }

    public User User { get; set; } = null!;
    public Branch? Branch { get; set; }
}
