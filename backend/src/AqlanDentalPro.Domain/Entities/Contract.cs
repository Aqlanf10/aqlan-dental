using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Contract : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? Specialty { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; } = 0;
    public int InstallmentsCount { get; set; } = 1;
    public decimal? InstallmentAmount { get; set; }
    public DateOnly? StartDate { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public string? DiscountReason { get; set; }
    /// <summary>M2 FIX: Changed from string to ContractStatus enum with HasConversion&lt;string&gt; for DB compatibility.</summary>
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }

    public Patient Patient { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
