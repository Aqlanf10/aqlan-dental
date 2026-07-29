namespace AqlanDentalPro.Application.DTOs.Finance;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid? ContractId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string AccountCurrency { get; set; } = "YER";
    public decimal ExchangeRateToAccountCurrency { get; set; } = 1m;
    public decimal ExchangeRateToYer { get; set; }
    public decimal AppliedAmount { get; set; }
    public string? ExchangeRateSource { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? ServiceDescription { get; set; }
    public string? Specialty { get; set; }
    public Guid? DoctorId { get; set; }
    public string? DoctorName { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public class CreatePaymentRequest
{
    public Guid PatientId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string? Currency { get; set; }
    public string? AccountCurrency { get; set; }
    public decimal? ExchangeRateToAccountCurrency { get; set; }
    public decimal? ExchangeRateToYer { get; set; }
    public string? ExchangeRateSource { get; set; }
    public string? PaymentMethod { get; set; } = "cash";
    public string? ServiceDescription { get; set; }
    public string? Specialty { get; set; }
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }
    /// <summary>
    /// Resolved branch ID from controller. When provided and valid (non-empty),
    /// FinanceService uses this instead of independently resolving from currentUser.
    /// This ensures the branch validated by the controller is the one written to
    /// financial records — prevents Guid.Empty for Admin users without an assigned branch.
    /// </summary>
    public Guid? ResolvedBranchId { get; set; }
}

public class UpdatePaymentRequest
{
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public string? AccountCurrency { get; init; }
    public decimal? ExchangeRateToAccountCurrency { get; init; }
    public decimal? ExchangeRateToYer { get; init; }
    public string? ExchangeRateSource { get; init; }
    public string? PaymentDate { get; init; }
    public string? PaymentMethod { get; init; }
    public string? ServiceDescription { get; init; }
    public string? Specialty { get; init; }
    public Guid? DoctorId { get; init; }
    public string? Notes { get; init; }
}

public class PatientFinanceSummaryDto
{
    public decimal TotalTreatmentCost { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal OverdueAmount { get; set; }
    public PaymentDto? LatestPayment { get; set; }
    public string FinancialStatus { get; set; } = "no_plan";
    public int ActiveContractsCount { get; set; }
    public int TotalPaymentsCount { get; set; }
    /// <summary>
    /// QA-594: Sum of <c>Visit.AmountDueReference</c> for visits that have no
    /// linked invoice (i.e. sessions performed without a contract or invoice).
    /// Included in <see cref="TotalTreatmentCost"/> and
    /// <see cref="OutstandingBalance"/> so that partial payments on unbilled
    /// sessions are reflected correctly instead of disappearing.
    /// </summary>
    public decimal UnbilledVisitsAmount { get; set; }
}
