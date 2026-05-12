namespace AqlanDentalPro.Application.DTOs.Finance;

public class UpdateContractRequest
{
    public decimal? TotalAmount { get; init; }
    public decimal? DownPayment { get; init; }
    public int? InstallmentsCount { get; init; }
    public decimal? InstallmentAmount { get; init; }
    public decimal? DiscountAmount { get; init; }
    public string? DiscountReason { get; init; }
    public string? Status { get; init; }
    public string? Notes { get; init; }
}
