namespace AqlanDentalPro.Application.DTOs.Finance;

public class FinanceSummaryDto
{
    public decimal TodayCollected { get; set; }
    public decimal MonthCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ActiveContracts { get; set; }
    public List<PaymentDto> RecentPayments { get; set; } = [];
}
