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
    /// Finance Phase 1: Pays a supplier bill (partially or fully).
    /// Validates open cashier session, loads bill + supplier, updates PaidAmount/Status/Balance,
    /// creates SupplierBillPayment, CashFlowTransaction (Outflow), and double-entry journal
    /// (Debit AccountsPayable / Credit Treasury). Commits atomically.
    /// </summary>
    Task PaySupplierBillAsync(Guid billId, PaySupplierBillRequest request, Guid currentUserId);

    /// <summary>
    /// Finance Phase 1: Processes a refund for an approved Credit Note.
    /// Validates open cashier session, loads creditNote + invoice, creates refund Payment (Expense type),
    /// updates creditNote status to Refunded, creates CashFlowTransaction (Outflow), and double-entry
    /// journal (Debit SalesReturns / Credit Treasury). Commits atomically.
    /// </summary>
    Task ProcessRefundAsync(Guid creditNoteId, ProcessRefundRequest request, Guid currentUserId);
}
