using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IFinanceService
{
    Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req);
    Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status);
    Task<List<PaymentDto>> GetPaymentsAsync(int page, int pageSize, Guid? patientId);
    Task<PaymentDto?> GetPaymentByIdAsync(Guid id);
    Task<PaymentDto> CreatePaymentAsync(CreatePaymentRequest req);
    Task<PaymentDto?> UpdatePaymentAsync(Guid id, UpdatePaymentRequest req);
    Task<bool> DeletePaymentAsync(Guid id);
    Task<PaymentDto?> RefundPaymentAsync(Guid id, string? reason, decimal? partialAmount = null);
    Task TryMarkInvoicePaidAsync(Guid invoiceId);

    // NOTE: PostInvoiceIssuedEntryAsync + ReverseInvoiceIssuedEntryAsync were moved to
    // IInvoiceLedgerService (TD-021 PR A1). Update call sites to inject IInvoiceLedgerService.
    // NOTE: GetAccountStatementAsync, GetSummaryAsync, GetPatientFinanceSummaryAsync, and
    // GetOverdueContractsAsync were moved to IFinanceReadService (TD-021 PR A2). Update
    // call sites to inject IFinanceReadService.
    // NOTE: GetContractsAsync, GetContractByIdAsync, and UpdateContractAsync were moved to
    // IContractService (TD-021 PR A3). Update call sites to inject IContractService.
    // CreateContractAsync + UpdateContractStatusAsync stay here because they depend on
    // payment-side helpers (will move with PR A4 — PaymentService cluster).

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
