namespace AqlanDentalPro.Application.DTOs.Finance;

public class UpdateContractRequest
{
    public string? Specialty { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? DownPayment { get; set; }
    public int? InstallmentsCount { get; set; }
    public decimal? InstallmentAmount { get; set; }
    public string? StartDate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}
