using System.Collections.Generic;

namespace AqlanDentalPro.Application.DTOs.Finance;

public class FinanceSummaryDto
{
    public decimal TodayCollected { get; set; }
    public decimal MonthCollected { get; set; }
    public decimal TotalOutstanding { get; set; }
    public int ActiveContracts { get; set; }
    public int UnpaidInvoicesCount { get; set; }
    public int DraftInvoicesCount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal PendingCommissionsAmount { get; set; }
    public List<PaymentDto> RecentPayments { get; set; } = [];
    public List<RecentInvoiceDto> RecentInvoices { get; set; } = [];
}
