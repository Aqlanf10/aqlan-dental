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
    public string Status { get; set; } = "active";
    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }

    public Patient Patient { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
