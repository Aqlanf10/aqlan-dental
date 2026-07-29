using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// TD-021 PR A4 (slice 1): the shared treasury + Finance V3 dual-write helpers,
/// extracted verbatim from <c>FinanceService</c>. This is the ledger-writing core
/// that every payment/refund/supplier money movement funnels through:
/// treasury resolution + balance mutation (NoSave variants for atomic
/// transactions) and the JournalEntry dual-writes.
///
/// Static with explicit dependencies — same pattern as <see cref="FinanceMappers"/>
/// and <c>DoctorRoomResolver</c> — deliberately NOT an injected service: ~30 test
/// call sites construct <c>FinanceService</c> directly, and a constructor change
/// would turn a pure code move into a 15-file edit. A future PaymentService /
/// SupplierRefundService slice calls these the same way FinanceService does.
///
/// Behavior contract (unchanged from the FinanceService originals):
/// - *NoSave methods never call SaveChangesAsync — the caller's transaction
///   persists all tracked changes together.
/// - Dual-write methods DO call SaveChangesAsync after auto-posting, exactly as
///   before (their callers invoke them inside an open transaction).
/// </summary>
public static class FinanceLedgerWriter
{
    public static string NormalizePaymentMethod(string? method)
    {
        var value = (method ?? "cash").Trim().ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");

        return value switch
        {
            "" or "cash" or "نقدي" or "نقدا" => "cash",
            "card" or "credit card" or "debit card" or "بطاقة" => "card",
            "bank" or "bank transfer" or "transfer" or "تحويل بنكي" or "حوالة" or "karimey" or "jawaly" or "check" => "bank",
            _ => value
        };
    }

    /// <summary>
    /// Updates the branch treasury balance WITHOUT calling SaveChangesAsync.
    /// Used within atomic dual-write transactions (CreatePaymentAsync, DeletePaymentAsync, RefundPaymentAsync)
    /// where all entity changes must be tracked in the DbContext and persisted together
    /// at the end of the transaction.
    /// </summary>
    public static async Task UpdateTreasuryBalanceNoSaveAsync(AppDbContext db, Guid branchId, decimal amount, string? paymentMethod, string? currency = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury balance update");

        var normalizedPaymentMethod = NormalizePaymentMethod(paymentMethod);
        var normalizedCurrency = FinanceMappers.NormalizeCurrency(currency);
        var type = (normalizedPaymentMethod == "card" || normalizedPaymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        // Previously used hardcoded names ("حساب بنك التضامن", "درج كاشير الاستقبال")
        // which would fail if the treasury was renamed. Now we find the first active
        // treasury of the correct type for the branch, regardless of its name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.Currency == normalizedCurrency && t.IsActive);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == type
                    && e.Entity.Currency == normalizedCurrency
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        if (treasury == null)
        {
            // Only use default name when auto-creating a new treasury
            var defaultName = normalizedCurrency == FinanceMappers.BaseCurrency
                ? (type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")
                : $"{(type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")} - {normalizedCurrency}";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Currency = normalizedCurrency,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
        }

        // Direct balance update (no raw SQL) — ExecuteSqlRawAsync causes DbContext
        // concurrency issues inside transactions. The tracked entity update is safe
        // because the caller's transaction provides atomicity.
        treasury.Balance += amount;

        // Do NOT call SaveChangesAsync — the caller persists all changes together
    }

    /// <summary>
    /// Resolves (or creates, tracked-only) the treasury for a branch/method/currency
    /// WITHOUT calling SaveChangesAsync. Used within atomic dual-write transactions
    /// where the caller persists all changes.
    /// NOTE: preserves the original's payment-method matching verbatim (raw
    /// "card"/"bank_transfer"/"bank" comparison, unlike the normalized variant above).
    /// </summary>
    public static async Task<Treasury> ResolveTreasuryNoSaveAsync(AppDbContext db, Guid branchId, string? paymentMethod, string? currency = null)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("BranchId is required for treasury resolution and cannot be Guid.Empty");

        var normalizedCurrency = FinanceMappers.NormalizeCurrency(currency);
        var type = (paymentMethod == "card" || paymentMethod == "bank_transfer" || paymentMethod == "bank")
            ? TreasuryType.Bank
            : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == type && t.Currency == normalizedCurrency && t.IsActive);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == type
                    && e.Entity.Currency == normalizedCurrency
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        if (treasury == null)
        {
            var defaultName = normalizedCurrency == FinanceMappers.BaseCurrency
                ? (type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")
                : $"{(type == TreasuryType.Bank ? "حساب بنكي" : "درج كاشير")} - {normalizedCurrency}";
            treasury = new Treasury
            {
                Name = defaultName,
                Type = type,
                Currency = normalizedCurrency,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
            // Do NOT call SaveChangesAsync — the caller will save all tracked entities together
        }

        return treasury;
    }

    /// <summary>
    /// Dual-write journal entry for a patient payment.
    /// - If allocated to an Issued invoice: Debit Treasury / Credit PatientReceivable (settles AR, no revenue)
    /// - If unallocated advance: Debit Treasury / Credit PatientAdvance (records liability)
    /// MUST be atomic — if JE fails, the entire operation must fail.
    /// </summary>
    public static async Task DualWritePaymentEntryAsync(
        AppDbContext db, IJournalEntryService journalEntryService,
        Payment payment, CashFlowTransaction cashflow, Invoice? invoice)
    {
        if (payment.PatientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be Guid.Empty for dual-write journal entry");
        if (payment.BranchId == null || payment.BranchId == Guid.Empty)
            throw new ArgumentException("BranchId cannot be Guid.Empty for dual-write journal entry");

        var treasury = await ResolveTreasuryNoSaveAsync(db, payment.BranchId.Value, payment.PaymentMethod, payment.Currency);
        cashflow.TreasuryId = treasury.Id;
        var appliedAmount = payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount;

        var isAllocatedToInvoice = invoice != null && invoice.Status == InvoiceStatus.Issued;
        var creditAccountType = isAllocatedToInvoice ? JournalAccountType.PatientReceivable : JournalAccountType.PatientAdvance;
        var creditDescription = isAllocatedToInvoice
            ? $"تسوية ذمم مريض - سند قبض {payment.ReceiptNumber}"
            : $"دفعة مقدمة غير مخصصة - سند قبض {payment.ReceiptNumber}";

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (JournalAccountType.Treasury, treasury.Id, appliedAmount, 0m, $"تحصيل دفعة - سند قبض {payment.ReceiptNumber} ({payment.Amount:N2} {FinanceMappers.NormalizeCurrency(payment.Currency)})"),
            (creditAccountType, payment.PatientId, 0m, appliedAmount, creditDescription)
        };

        var entry = await journalEntryService.CreateEntryAsync(
            documentType: FinancialDocumentType.Payment,
            financialDocumentId: payment.Id,
            description: isAllocatedToInvoice
                ? $"تحصيل دفعة مستحقة - سند قبض {payment.ReceiptNumber}"
                : $"تحصيل دفعة مقدمة - سند قبض {payment.ReceiptNumber}",
            entryDate: payment.PaymentDate,
            branchId: payment.BranchId.Value,
            performedBy: payment.ReceivedBy ?? Guid.Empty,
            cashierSessionId: cashflow.CashierSessionId,
            treasuryId: treasury.Id,
            lines: lines,
            autoSave: false);

        // Journal lines are expressed in the invoice/contract account currency
        // (AppliedAmount), not necessarily the physical payment currency.
        entry.Currency = FinanceMappers.NormalizeCurrency(payment.AccountCurrency);
        entry.ExchangeRateToYer = GetAccountCurrencyRateToYer(payment);

        // Auto-post since this is an operational posting
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dual-write reversal entry for deleted/cancelled payments.
    /// Finds the original JournalEntry by FinancialDocumentId and creates a mirrored reversal.
    /// MUST NOT silently swallow exceptions.
    /// </summary>
    public static async Task DualWriteReversalEntryAsync(
        AppDbContext db, IJournalEntryService journalEntryService, ILogger logger,
        Guid performedBy, Guid paymentId, string reason)
    {
        var originalEntry = await db.JournalEntries
            .FirstOrDefaultAsync(e => e.FinancialDocumentId == paymentId && e.FinancialDocumentType == FinancialDocumentType.Payment && !e.IsReversal);

        if (originalEntry == null)
        {
            logger.LogWarning("No original JournalEntry found for payment {PaymentId} — skipping JE reversal", paymentId);
            return;
        }

        var reversal = await journalEntryService.CreateReversalEntryAsync(
            originalEntryId: originalEntry.Id,
            reason: reason,
            performedBy: performedBy);

        // Auto-post the reversal
        reversal.IsPosted = true;
        reversal.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dual-write refund entry.
    /// - If original payment was allocated to an invoice: Debit PatientReceivable / Credit Treasury
    /// - If original payment was unallocated advance: Debit PatientAdvance / Credit Treasury
    /// MUST NOT silently swallow exceptions.
    /// </summary>
    public static async Task DualWriteRefundEntryAsync(
        AppDbContext db, IJournalEntryService journalEntryService,
        Payment originalPayment, Payment refundPayment, decimal refundAmount)
    {
        if (originalPayment.PatientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be Guid.Empty for refund journal entry");
        if (originalPayment.BranchId == null || originalPayment.BranchId == Guid.Empty)
            throw new ArgumentException("BranchId cannot be Guid.Empty for refund journal entry");

        var treasury = await ResolveTreasuryNoSaveAsync(db, originalPayment.BranchId.Value, refundPayment.PaymentMethod, refundPayment.Currency);
        var appliedRefundAmount = Math.Abs(refundPayment.AppliedAmount == 0 ? refundAmount : refundPayment.AppliedAmount);

        var wasAllocatedToInvoice = originalPayment.InvoiceId.HasValue;
        var debitAccountType = wasAllocatedToInvoice ? JournalAccountType.PatientReceivable : JournalAccountType.PatientAdvance;
        var debitDescription = wasAllocatedToInvoice
            ? $"إعادة ذمم مدينة - استرداد سند قبض {refundPayment.ReceiptNumber}"
            : $"تخفيض دفعات مقدمة - استرداد سند قبض {refundPayment.ReceiptNumber}";

        var lines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
        {
            (debitAccountType, originalPayment.PatientId, appliedRefundAmount, 0m, debitDescription),
            (JournalAccountType.Treasury, treasury.Id, 0m, appliedRefundAmount, $"صرف استرداد - سند قبض {refundPayment.ReceiptNumber}")
        };

        var entry = await journalEntryService.CreateEntryAsync(
            documentType: FinancialDocumentType.Refund,
            financialDocumentId: refundPayment.Id,
            description: wasAllocatedToInvoice
                ? $"استرداد دفعة مستحقة - سند قبض {refundPayment.ReceiptNumber}"
                : $"استرداد دفعة مقدمة - سند قبض {refundPayment.ReceiptNumber}",
            entryDate: refundPayment.PaymentDate,
            branchId: originalPayment.BranchId.Value,
            performedBy: refundPayment.ReceivedBy ?? Guid.Empty,
            cashierSessionId: null,
            treasuryId: treasury.Id,
            lines: lines,
            autoSave: false);

        // Keep the refund in the same account currency as the original settlement.
        entry.Currency = FinanceMappers.NormalizeCurrency(originalPayment.AccountCurrency);
        entry.ExchangeRateToYer = GetAccountCurrencyRateToYer(originalPayment);

        // Auto-post
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private static decimal GetAccountCurrencyRateToYer(Payment payment)
    {
        var paymentRateToYer = payment.ExchangeRateToYer;
        var paymentToAccountRate = payment.ExchangeRateToAccountCurrency == 0m
            ? 1m
            : payment.ExchangeRateToAccountCurrency;

        // Legacy rows had no FX snapshot. Keep their existing neutral treatment;
        // all new payments are validated and persist a non-zero snapshot.
        if (paymentRateToYer <= 0m) return 1m;

        return Math.Round(
            paymentRateToYer / paymentToAccountRate,
            6,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// H9 FIX: Generates a unique receipt number using advisory lock + sequential pattern.
    /// Format: RCP-yyyyMMdd-NNN (sequential, not random).
    /// CON FIX: Uses pg_advisory_xact_lock inside an explicit transaction to prevent
    /// race conditions when multiple payments are created concurrently.
    /// Transaction-level lock is automatically released on commit/rollback — safe with
    /// connection pooling (no risk of stuck locks if the connection is returned to the pool).
    /// </summary>
    public static async Task<string> GenerateReceiptNumberAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"RCP-{datePart}-";

        // Simple sequential generation without advisory locks.
        // Advisory locks (both xact_lock and session-level lock) cause DbContext concurrency
        // issues when called from CreatePaymentAsync which uses its own transaction.
        // Instead, rely on the unique constraint on ReceiptNumber + retry logic
        // (handled by the caller's transaction rollback).
        var lastReceipt = await db.Payments
            .IgnoreQueryFilters()
            .Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNumber)
            .Select(p => p.ReceiptNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastReceipt) && lastReceipt.Length > prefix.Length)
        {
            var seqPart = lastReceipt[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

    /// <summary>
    /// H9 FIX: Generates a unique refund receipt number.
    /// Format: REF-yyyyMMdd-NNN (sequential).
    /// CON FIX: Uses pg_advisory_xact_lock inside an explicit transaction to prevent
    /// race conditions. Transaction-level lock is automatically released on commit/rollback
    /// — safe with connection pooling (no risk of stuck locks).
    /// </summary>
    public static async Task<string> GenerateRefundReceiptNumberAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"REF-{datePart}-";

        // Simple sequential generation without advisory locks (same reason as GenerateReceiptNumberAsync).
        var lastRefund = await db.Payments
            .IgnoreQueryFilters()
            .Where(p => p.ReceiptNumber != null && p.ReceiptNumber.StartsWith(prefix))
            .OrderByDescending(p => p.ReceiptNumber)
            .Select(p => p.ReceiptNumber)
            .FirstOrDefaultAsync();

        var nextSeq = 1;
        if (!string.IsNullOrEmpty(lastRefund) && lastRefund.Length > prefix.Length)
        {
            var seqPart = lastRefund[prefix.Length..];
            if (int.TryParse(seqPart, out var lastSeq))
                nextSeq = lastSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }
}
