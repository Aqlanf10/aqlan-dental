namespace AqlanDentalPro.Application.DTOs.Finance;

public class CreateContractRequest
{
    public Guid PatientId { get; set; }
    public string? Specialty { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; } = 0;
    public int InstallmentsCount { get; set; } = 1;
    public decimal? InstallmentAmount { get; set; }
    public string? StartDate { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public string? DiscountReason { get; set; }
    public string? Notes { get; set; }
}
