namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Invoice-ledger posting service — extracted from <see cref="IFinanceService"/> as part of
/// TD-021 PR A1 (god-service extraction). Owns the accrual journal entries that mirror
/// invoice state changes (Draft → Issued → Cancelled).
///
/// Behaviour-preserving move: the implementation is byte-for-byte identical to the previous
/// FinanceService methods. No business rule changes; only the host type changed.
///
/// Slicing rationale (see docs/technical-debt/TD-021-god-service-extraction-plan.md):
/// - Self-contained: deps are db + journalEntryService + currentUser + logger only.
/// - No shared private helpers with the rest of FinanceService.
/// - Lowest-risk slice to prove the extraction pattern before tackling Payments/Refund
///   (which share DualWrite* + ResolveTreasuryNoSaveAsync and must move together).
/// </summary>
public interface IInvoiceLedgerService
{
    /// <summary>
    /// Posts the accrual journal entry for an invoice issuance:
    /// Debit PatientReceivable / Credit Revenue.
    /// Called when an invoice transitions from Draft to Issued.
    /// Auto-posts the entry. Idempotency is the caller's responsibility (controller wraps
    /// status change + this call in one transaction and reloads on failure).
    /// </summary>
    Task PostInvoiceIssuedEntryAsync(Guid invoiceId);

    /// <summary>
    /// Reverses the original invoice issuance JournalEntry for a cancelled invoice.
    /// Finds the original issuance JE (Debit PatientReceivable / Credit Revenue)
    /// and creates a reversal entry (Credit PatientReceivable / Debit Revenue).
    /// Auto-posts the reversal. Used when cancelling an Issued invoice.
    /// If no original issuance JE is found, logs a warning and returns silently
    /// (caller is responsible for the idempotency check before invoking this).
    /// </summary>
    Task ReverseInvoiceIssuedEntryAsync(Guid invoiceId);
}
