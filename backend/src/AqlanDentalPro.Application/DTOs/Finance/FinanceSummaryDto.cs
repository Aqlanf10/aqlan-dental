namespace AqlanDentalPro.Application.DTOs.Finance;

public class FinanceSummaryDto
{
    public decimal TodayCollected { get; set; }
    public decimal MonthCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ActiveContracts { get; set; }
    public List<PaymentDto> RecentPayments { get; set; } = [];

    // Enhanced KPIs
    public decimal AverageContractValue { get; set; }
    public decimal CollectionRate { get; set; }
    public decimal OverduePercentage { get; set; }
    public int OverdueContractsCount { get; set; }
    public decimal TotalContractsValue { get; set; }
    public decimal TotalPaidValue { get; set; }
}
