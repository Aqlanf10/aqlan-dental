using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Finance V3 JournalEntry service — manages the canonical double-entry bookkeeping ledger.
/// All financial operations that create or reverse journal entries go through this service.
///
/// During the transition period, callers are responsible for dual-write:
/// they write to both CashFlowTransaction (legacy) and JournalEntry+JournalLine (new).
/// </summary>
public class JournalEntryService(
    AppDbContext db,
    ILogger<JournalEntryService> logger
) : IJournalEntryService
{
    private static readonly ConcurrentDictionary<DateOnly, int> InMemoryEntrySequences = new();

    // ─── Create Entry ────────────────────────────────────────────────────────

    public async Task<JournalEntry> CreateEntryAsync(
        FinancialDocumentType documentType,
        Guid financialDocumentId,
        string description,
        DateOnly entryDate,
        Guid branchId,
        Guid performedBy,
        Guid? cashierSessionId,
        Guid? treasuryId,
        IEnumerable<(JournalAccountType accountType, Guid accountId, decimal debit, decimal credit, string? description)> lines,
        bool autoSave = true,
        CancellationToken ct = default)
    {
        // Validate required fields — never Guid.Empty (Finance V3 Section 6.1 & 6.6)
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required and cannot be Guid.Empty", nameof(branchId));
        if (performedBy == Guid.Empty)
            throw new ArgumentException("PerformedBy is required and cannot be Guid.Empty", nameof(performedBy));
        if (financialDocumentId == Guid.Empty)
            throw new ArgumentException("FinancialDocumentId is required and cannot be Guid.Empty", nameof(financialDocumentId));

        await EnsurePeriodOpenAsync(branchId, entryDate, ct);

        // Finance V3: Validate AccountId is not Guid.Empty on each line
        var lineItems = lines.ToList();
        foreach (var (accountType, accountId, _, _, _) in lineItems)
        {
            if (accountId == Guid.Empty)
                throw new ArgumentException($"AccountId cannot be Guid.Empty for account type {accountType}");
        }

        var entryNumber = await GenerateEntryNumberAsync(ct);

        var entry = new JournalEntry
        {
            EntryNumber = entryNumber,
            FinancialDocumentId = financialDocumentId,
            FinancialDocumentType = documentType,
            Description = description,
            EntryDate = entryDate,
            BranchId = branchId,
            PerformedBy = performedBy,
            CashierSessionId = cashierSessionId,
            TreasuryId = treasuryId,
            IsPosted = false,
            IsReversal = false,
        };

        // Create lines
        foreach (var (accountType, accountId, debit, credit, lineDescription) in lineItems)
        {

            if (debit < 0 || credit < 0)
                throw new ArgumentException($"Debit and Credit must be non-negative. Got Debit={debit}, Credit={credit}");

            if (debit > 0 && credit > 0)
                throw new ArgumentException($"Debit and Credit are mutually exclusive per line. Got Debit={debit}, Credit={credit}");

            if (debit == 0 && credit == 0)
                throw new ArgumentException("Each line must have a non-zero Debit or Credit amount");

            entry.Lines.Add(new JournalLine
            {
                AccountType = accountType,
                AccountId = accountId,
                Debit = debit,
                Credit = credit,
                Description = lineDescription,
                BranchId = branchId,
            });
        }

        // Validate balancing invariant (Section 2.1)
        if (!entry.IsBalanced())
        {
            var totalDebit = entry.Lines.Sum(l => l.Debit);
            var totalCredit = entry.Lines.Sum(l => l.Credit);
            throw new InvalidOperationException(
                $"Journal entry does not balance. Debit total: {totalDebit}, Credit total: {totalCredit}. " +
                "Every JournalEntry must have SUM(Debit) = SUM(Credit).");
        }

        // Must have at least 2 lines (double-entry)
        if (entry.Lines.Count < 2)
            throw new InvalidOperationException("A JournalEntry must have at least two lines (double-entry).");

        db.JournalEntries.Add(entry);
        // Auto-save by default. Callers managing their own transaction can set autoSave: false
        // to avoid "A second operation was started on this context instance" errors.
        if (autoSave)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Created JournalEntry {EntryNumber} (Type={DocType}, Branch={BranchId}, Debit={Debit}, Credit={Credit})",
            entryNumber, documentType, branchId, entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit));

        return entry;
    }

    // ─── Create Reversal ─────────────────────────────────────────────────────

    public async Task<JournalEntry> CreateReversalEntryAsync(
        Guid originalEntryId,
        string reason,
        Guid performedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reversal reason is required.", nameof(reason));

        await using var ownedTransaction = db.Database.IsRelational()
            && db.Database.CurrentTransaction == null
                ? await db.Database.BeginTransactionAsync(ct)
                : null;

        var original = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == originalEntryId, ct);

        if (original is null)
            throw new InvalidOperationException($"JournalEntry {originalEntryId} not found.");

        if (original.IsReversal)
            throw new InvalidOperationException("Cannot reverse a reversal entry. Create a new original-type entry instead. (Section 2.3.7)");

        var existingReversalId = original.ReversedByEntryId
            ?? await db.JournalEntries
                .Where(entry => entry.ReversalOfEntryId == originalEntryId)
                .Select(entry => (Guid?)entry.Id)
                .FirstOrDefaultAsync(ct);
        if (existingReversalId.HasValue)
            throw new InvalidOperationException($"Entry {originalEntryId} has already been reversed by entry {existingReversalId}.");

        // Mirror the lines: debit → credit, credit → debit
        var reversalLines = original.Lines.Select(l => (
            l.AccountType,
            l.AccountId,
            debit: l.Credit,   // swap
            credit: l.Debit,   // swap
            description: (string?)$"Reversal: {l.Description}"
        ));

        var reversal = await CreateEntryAsync(
            documentType: original.FinancialDocumentType,
            financialDocumentId: original.FinancialDocumentId,
            description: $"Reversal of {original.EntryNumber}: {reason}",
            entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
            branchId: original.BranchId,
            performedBy: performedBy,
            cashierSessionId: original.CashierSessionId,
            treasuryId: original.TreasuryId,
            lines: reversalLines,
            autoSave: false,
            ct: ct);

        // A reversal must retain the original currency snapshot; otherwise a non-YER
        // entry would be reversed in the JournalEntry default currency.
        reversal.Currency = original.Currency;
        reversal.ExchangeRateToYer = original.ExchangeRateToYer;

        // Link reversal
        reversal.IsReversal = true;
        reversal.ReversalOfEntryId = originalEntryId;

        // Insert the reversal before setting the original's back-link. The two
        // self-referencing FKs otherwise form a circular save dependency. The
        // surrounding transaction keeps both saves atomic on relational stores.
        await db.SaveChangesAsync(ct);

        original.ReversedByEntryId = reversal.Id;
        await db.SaveChangesAsync(ct);

        if (ownedTransaction != null)
            await ownedTransaction.CommitAsync(ct);

        logger.LogInformation(
            "Created reversal JournalEntry {ReversalNumber} for original {OriginalNumber}",
            reversal.EntryNumber, original.EntryNumber);

        return reversal;
    }

    // ─── Post Entry ──────────────────────────────────────────────────────────

    public async Task PostEntryAsync(Guid entryId, CancellationToken ct = default)
    {
        var entry = await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);

        if (entry is null)
            throw new InvalidOperationException($"JournalEntry {entryId} not found.");

        if (entry.IsPosted)
            throw new InvalidOperationException($"JournalEntry {entry.EntryNumber} is already posted.");

        await EnsurePeriodOpenAsync(entry.BranchId, entry.EntryDate, ct);

        if (!entry.IsBalanced())
        {
            var totalDebit = entry.Lines.Sum(l => l.Debit);
            var totalCredit = entry.Lines.Sum(l => l.Credit);
            throw new InvalidOperationException(
                $"Cannot post unbalanced entry {entry.EntryNumber}. Debit: {totalDebit}, Credit: {totalCredit}");
        }

        entry.IsPosted = true;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Posted JournalEntry {EntryNumber}", entry.EntryNumber);
    }

    // ─── Generate Entry Number ───────────────────────────────────────────────

    public async Task<string> GenerateEntryNumberAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var prefix = $"JE-{today:yyyyMMdd}-";

        if (db.Database.IsRelational()
            && db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var sequence = await ReservePostgresSequenceAsync(today, ct);
            return $"{prefix}{sequence:D3}";
        }

        // EF InMemory is used by unit tests. Keep the same observable numbering
        // contract and reserve process-local values atomically for concurrent tests.
        var lastEntryNumber = await db.JournalEntries
            .Where(e => e.EntryNumber.StartsWith(prefix))
            .OrderByDescending(e => e.EntryNumber)
            .Select(e => e.EntryNumber)
            .FirstOrDefaultAsync(ct);

        var persistedSequence = 0;
        if (lastEntryNumber is not null)
        {
            var lastPart = lastEntryNumber[prefix.Length..];
            if (int.TryParse(lastPart, out var lastSeq))
                persistedSequence = lastSeq;
        }

        var nextSeq = InMemoryEntrySequences.AddOrUpdate(
            today,
            persistedSequence + 1,
            (_, current) => Math.Max(current + 1, persistedSequence + 1));
        return $"{prefix}{nextSeq:D3}";
    }

    private async Task<int> ReservePostgresSequenceAsync(DateOnly entryDate, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                INSERT INTO "JournalEntryNumberSequences" ("EntryDate", "LastSequence")
                VALUES (@entryDate, 1)
                ON CONFLICT ("EntryDate") DO UPDATE
                SET "LastSequence" = "JournalEntryNumberSequences"."LastSequence" + 1
                RETURNING "LastSequence";
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "entryDate";
            parameter.DbType = DbType.Date;
            parameter.Value = entryDate.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task EnsurePeriodOpenAsync(Guid branchId, DateOnly entryDate, CancellationToken ct)
    {
        var isClosed = await db.AccountingPeriods.AnyAsync(period =>
            period.BranchId == branchId
            && period.IsActive
            && period.Status == "Closed"
            && entryDate >= period.StartDate
            && entryDate <= period.EndDate,
            ct);

        if (isClosed)
            throw new InvalidOperationException(
                $"Cannot create or post a journal entry dated {entryDate:yyyy-MM-dd} in a closed accounting period.");
    }

    // ─── Get Entry By ID ─────────────────────────────────────────────────────

    public async Task<JournalEntry?> GetEntryByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    // ─── Get Entries By Branch ───────────────────────────────────────────────

    public async Task<IEnumerable<JournalEntry>> GetEntriesByBranchAsync(
        Guid branchId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var query = db.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.BranchId == branchId);

        if (fromDate.HasValue)
            query = query.Where(e => e.EntryDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.EntryDate <= toDate.Value);

        return await query
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.EntryNumber)
            .ToListAsync(ct);
    }

    // ─── Get Account Balance ─────────────────────────────────────────────────

    public async Task<decimal> GetAccountBalanceAsync(
        JournalAccountType accountType,
        Guid accountId,
        CancellationToken ct = default)
    {
        // FIX: Only include posted entries — unposted entries are not part of the canonical balance
        var lines = await db.JournalLines
            .Where(l => l.AccountType == accountType && l.AccountId == accountId && l.JournalEntry != null && l.JournalEntry.IsPosted)
            .ToListAsync(ct);

        // Balance = SUM(Debit) - SUM(Credit)
        // For Treasury (debit-normal): positive = has funds
        // For Revenue (credit-normal): negative = net revenue earned
        return lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit);
    }
}
