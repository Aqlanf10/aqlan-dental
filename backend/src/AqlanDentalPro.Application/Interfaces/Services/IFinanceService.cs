using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IFinanceService
{
    Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status);
    Task<ContractDetailDto?> GetContractByIdAsync(Guid id);
    Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req);
    Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req);
    Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status);
    Task<List<OverdueContractDto>> GetOverdueContractsAsync();
    Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId);
    Task<PaymentDto?> GetPaymentByIdAsync(Guid id);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req);
    Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req);
    Task<bool> DeletePaymentAsync(Guid id);
    Task<PaymentDto?> RefundPaymentAsync(Guid id, string? reason, decimal? partialAmount = null);
    Task<AccountStatementDto?> GetAccountStatementAsync(Guid patientId);
    Task<PatientFinanceSummaryDto> GetPatientFinanceSummaryAsync(Guid patientId);
    Task<FinanceSummaryDto> GetSummaryAsync();
    Task TryMarkInvoicePaidAsync(Guid invoiceId);

    /// <summary>
    /// Posts the accrual journal entry for an invoice issuance:
    /// Debit PatientReceivable / Credit Revenue.
    /// Called when an invoice transitions from Draft to Issued.
    /// </summary>
    Task PostInvoiceIssuedEntryAsync(Guid invoiceId);

    /// <summary>
    /// Reverses the original invoice issuance JournalEntry for a cancelled invoice.
    /// Finds the original issuance JE (Debit PatientReceivable / Credit Revenue)
    /// and creates a reversal entry (Credit PatientReceivable / Debit Revenue).
    /// Auto-posts the reversal. Used when cancelling an Issued invoice.
    /// </summary>
    Task ReverseInvoiceIssuedEntryAsync(Guid invoiceId);

    /// <summary>
    /// ينشئ خطة تقسيط جديدة لعقد تقويم ويولّد الأقساط الشهرية تلقائياً.
    /// يتحقق من وجود العقد وعدم وجود خطة سابقة، ثم يحسب المبالغ ويوزعها
    /// مع معالجة فروق التقريب في الشهر الأخير.
    /// </summary>
    Task<InstallmentPlanDto> GenerateInstallmentPlanAsync(CreateInstallmentPlanRequest request);

    /// <summary>
    /// يسترجع خطة التقسيط المرتبطة بعقد معين مع جميع الأقساط المجدولة.
    /// </summary>
    Task<InstallmentPlanDto> GetInstallmentPlanByContractIdAsync(Guid contractId);
}
