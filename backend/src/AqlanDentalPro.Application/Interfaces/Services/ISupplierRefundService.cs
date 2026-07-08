using AqlanDentalPro.Application.DTOs.Finance;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Supplier-payables / credit-note refund service — extracted from
/// <see cref="IFinanceService"/> as part of TD-021 PR A4 (slice 3).
/// </summary>
public interface ISupplierRefundService
{
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
