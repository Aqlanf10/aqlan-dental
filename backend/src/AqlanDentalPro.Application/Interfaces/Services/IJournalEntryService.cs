using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// Service for creating and managing JournalEntry + JournalLine records
/// in the Finance V3 double-entry bookkeeping model.
///
/// During the transition period (Phase 2-3), this service provides
/// dual-write capabilities: financial operations write to both
/// CashFlowTransaction (legacy) and JournalEntry+JournalLine (new).
/// </summary>
public interface IJournalEntryService
{
    /// <summary>
    /// Creates a balanced JournalEntry with the given lines.
    /// Validates that SUM(Debit) = SUM(Credit) before saving.
    /// Throws InvalidOperationException if the entry does not balance.
    /// </summary>
    Task<JournalEntry> CreateEntryAsync(
        FinancialDocumentType documentType,
        Guid financialDocumentId,
        string description,
        DateOnly entryDate,
        Guid branchId,
        Guid performedBy,
        Guid? cashierSessionId,
        Guid? treasuryId,
        IEnumerable<(JournalAccountType accountType, Guid accountId, decimal debit, decimal credit, string? description)> lines,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a reversal JournalEntry for an existing entry.
    /// Produces mirror-image lines (debit becomes credit, credit becomes debit).
    /// Sets IsReversal = true and links via ReversalOfEntryId.
    /// </summary>
    Task<JournalEntry> CreateReversalEntryAsync(
        Guid originalEntryId,
        string reason,
        Guid performedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Posts a JournalEntry after validating that it balances.
    /// Sets IsPosted = true. Throws if the entry does not balance.
    /// </summary>
    Task PostEntryAsync(Guid entryId, CancellationToken ct = default);

    /// <summary>
    /// Generates the next sequential EntryNumber (e.g., JE-20260526-001).
    /// Uses advisory locking for concurrency safety.
    /// </summary>
    Task<string> GenerateEntryNumberAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a JournalEntry by ID including its Lines.
    /// </summary>
    Task<JournalEntry?> GetEntryByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets all JournalEntries for a branch, optionally filtered by date range.
    /// </summary>
    Task<IEnumerable<JournalEntry>> GetEntriesByBranchAsync(
        Guid branchId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the account balance for a specific account type + account ID.
    /// Balance = SUM(Debit) - SUM(Credit) for the account.
    /// For Treasury accounts, a positive balance means the treasury has funds.
    /// For Revenue accounts, a negative balance means net revenue (credit-normal).
    /// </summary>
    Task<decimal> GetAccountBalanceAsync(
        JournalAccountType accountType,
        Guid accountId,
        CancellationToken ct = default);
}
