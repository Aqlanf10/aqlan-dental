using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Centralized treasury resolution service for Finance V3.
/// Routes payments to the correct treasury based on payment method:
/// - cash → Vault treasury (linked to CashierSession when available)
/// - card / bank / bank_transfer → Bank treasury
///
/// This ensures Treasury.Balance stays in sync with JournalEntry + CashFlowTransaction
/// and that cash vs bank routing is always correct.
/// </summary>
public class TreasuryResolutionService(
    AppDbContext db,
    ILogger<TreasuryResolutionService> logger) : ITreasuryResolutionService
{
    // Phase 6: Default treasury names for auto-creation only.
    // Lookup is now by BranchId + Type (not name), so renamed treasuries are still found.
    private const string DefaultVaultName = "درج كاشير";
    private const string DefaultBankName = "حساب بنكي";

    /// <inheritdoc />
    public async Task<Treasury> ResolveTreasuryAsync(
        Guid branchId,
        string? paymentMethod,
        Guid? cashierSessionId = null,
        CancellationToken ct = default)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("عذراً، الفرع غير محدد. لا يمكن تحديد الخزينة.");

        var isBankPayment = IsBankPaymentMethod(paymentMethod);

        // For cash payments with a CashierSession, try to use the session's treasury first
        if (!isBankPayment && cashierSessionId.HasValue)
        {
            var session = await db.CashierSessions.FindAsync(new object[] { cashierSessionId.Value }, ct);
            if (session != null && session.IsActive)
            {
                if (session.TreasuryId.HasValue)
                {
                    var sessionTreasury = await db.Treasuries.FindAsync(new object[] { session.TreasuryId.Value }, ct);
                    if (sessionTreasury != null && sessionTreasury.IsActive)
                    {
                        return sessionTreasury;
                    }
                }
            }
        }

        // Fall back to standard treasury resolution by type
        var treasuryType = isBankPayment ? TreasuryType.Bank : TreasuryType.Vault;

        // Phase 6: Lookup by BranchId + Type instead of hardcoded name.
        var treasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == treasuryType && t.IsActive, ct);

        if (treasury == null)
        {
            // Check ChangeTracker for a locally added treasury not yet persisted
            treasury = db.ChangeTracker.Entries<Treasury>()
                .Where(e => e.State == EntityState.Added
                    && e.Entity.BranchId == branchId
                    && e.Entity.Type == treasuryType
                    && e.Entity.IsActive)
                .Select(e => e.Entity)
                .FirstOrDefault();
        }

        if (treasury == null)
        {
            // Auto-create the treasury for the branch (same behavior as FinanceService)
            var defaultName = isBankPayment ? DefaultBankName : DefaultVaultName;
            treasury = new Treasury
            {
                Name = defaultName,
                Type = treasuryType,
                Balance = 0,
                BranchId = branchId,
                IsActive = true
            };
            db.Treasuries.Add(treasury);
            // Do NOT call SaveChangesAsync — caller persists all changes together
            logger.LogInformation("Auto-creating {Type} treasury '{Name}' for branch {BranchId}", treasuryType, defaultName, branchId);
        }

        return treasury;
    }

    /// <inheritdoc />
    public async Task DecrementTreasuryBalanceAsync(
        Guid branchId,
        string? paymentMethod,
        decimal amount,
        Guid? cashierSessionId = null,
        CancellationToken ct = default)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("عذراً، الفرع غير محدد. لا يمكن تحديث رصيد الخزينة.");

        if (amount <= 0)
            throw new ArgumentException("مبلغ الصرف يجب أن يكون أكبر من الصفر.");

        var treasury = await ResolveTreasuryAsync(branchId, paymentMethod, cashierSessionId, ct);
        var blocked = await IsNegativeBalanceBlockedAsync(ct);

        // When enforcement is OFF (Admin opt-out), keep the legacy warn-only behavior. The
        // actual block, when enforcement is ON, is applied atomically inside
        // MutateTreasuryBalanceAsync so concurrent outflows can't bypass it.
        if (!blocked && treasury.Balance - amount < 0)
            logger.LogWarning(
                "Treasury {TreasuryId} ({Name}) balance going NEGATIVE: balance {Balance} - outflow {Amount}. Set '{SettingKey}' to true to block this.",
                treasury.Id, treasury.Name, treasury.Balance, amount, PreventNegativeBalanceSettingKey);

        await MutateTreasuryBalanceAsync(treasury, -amount, enforceNonNegative: blocked, ct);

        logger.LogInformation(
            "Treasury {TreasuryId} ({Type}) decremented by {Amount} for {PaymentMethod} outflow",
            treasury.Id, treasury.Type, amount, paymentMethod);
    }

    /// <inheritdoc />
    public async Task IncrementTreasuryBalanceAsync(
        Guid branchId,
        string? paymentMethod,
        decimal amount,
        CancellationToken ct = default)
    {
        if (branchId == Guid.Empty)
            throw new ArgumentException("عذراً، الفرع غير محدد. لا يمكن تحديث رصيد الخزينة.");

        if (amount <= 0)
            throw new ArgumentException("مبلغ الإضافة يجب أن يكون أكبر من الصفر.");

        var treasury = await ResolveTreasuryAsync(branchId, paymentMethod, null, ct);
        await MutateTreasuryBalanceAsync(treasury, amount, enforceNonNegative: false, ct);

        logger.LogInformation(
            "Treasury {TreasuryId} ({Type}) incremented by {Amount} for {PaymentMethod} reversal",
            treasury.Id, treasury.Type, amount, paymentMethod);
    }

    /// <inheritdoc />
    public async Task IncrementTreasuryBalanceByTreasuryIdAsync(
        Guid treasuryId,
        decimal amount,
        CancellationToken ct = default)
    {
        if (treasuryId == Guid.Empty)
            throw new ArgumentException("عذراً، معرف الخزينة غير محدد. لا يمكن استعادة الرصيد.");

        if (amount <= 0)
            throw new ArgumentException("مبلغ الإضافة يجب أن يكون أكبر من الصفر.");

        var treasury = await db.Treasuries.FindAsync(new object[] { treasuryId }, ct);
        if (treasury == null || !treasury.IsActive)
            throw new ArgumentException("عذراً، الخزينة الأصلية غير موجودة أو غير مفعلة. لا يمكن عكس القيد المالي — تواصل مع المحاسب.");

        await MutateTreasuryBalanceAsync(treasury, amount, enforceNonNegative: false, ct);

        logger.LogInformation(
            "Treasury {TreasuryId} ({Type}) incremented by {Amount} via exact TreasuryId for reversal",
            treasury.Id, treasury.Type, amount);
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Settings key that, when set to "true"/"1", blocks any treasury outflow
    /// that would drive the balance negative (expenses, salaries, lab payables,
    /// supplier bills, commission payouts, advances).
    /// </summary>
    public const string PreventNegativeBalanceSettingKey = "finance.prevent_negative_treasury_balance";

    private async Task<bool> IsNegativeBalanceBlockedAsync(CancellationToken ct)
    {
        var value = await db.Settings
            .Where(s => s.Key == PreventNegativeBalanceSettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

        // Audit §5.2: BLOCK BY DEFAULT. A missing/empty value enforces the guard so a clinic
        // can never silently overdraft its treasury. Only an explicit opt-out
        // ("false"/"0"/"off"/"no") set by the Admin from Settings disables it (warn-only).
        if (string.IsNullOrWhiteSpace(value)) return true;
        return !(string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                 || value == "0"
                 || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(value, "no", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBankPaymentMethod(string? paymentMethod)
    {
        return string.Equals(paymentMethod, "card", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod, "bank_transfer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod, "bank", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mutates treasury balance. On a relational store the increment is applied as a single
    /// set-based UPDATE (<c>Balance = Balance + delta</c>) so concurrent outflows/inflows on the
    /// same treasury cannot lose updates. Falls back to in-memory mutation for newly-added rows
    /// and the InMemory provider (unit tests). Runs inside the caller's transaction.
    /// </summary>
    private async Task MutateTreasuryBalanceAsync(Treasury treasury, decimal delta, bool enforceNonNegative, CancellationToken ct)
    {
        var entry = db.Entry(treasury);

        // A newly auto-created treasury is not yet inserted, so a set-based UPDATE would match
        // zero rows; and the InMemory provider (unit tests) does not support ExecuteUpdateAsync.
        // In both cases mutate in memory and let the caller's SaveChanges insert/persist it.
        if (entry.State == EntityState.Added || !db.Database.IsRelational())
        {
            if (enforceNonNegative && treasury.Balance + delta < 0)
                throw InsufficientTreasuryBalance(treasury.Name, treasury.Balance, -delta);
            treasury.Balance += delta;
            return;
        }

        if (enforceNonNegative)
        {
            // Atomic block (audit §5.2 + §5.1): apply the delta ONLY if the balance stays >= 0.
            // The guard and the write are a single statement under a row-level lock, so two
            // concurrent outflows cannot both pass an in-memory check and overdraw the treasury.
            var rows = await db.Treasuries
                .Where(t => t.Id == treasury.Id && t.Balance + delta >= 0)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Balance, t => t.Balance + delta), ct);
            if (rows == 0)
            {
                var current = await db.Treasuries
                    .Where(t => t.Id == treasury.Id)
                    .Select(t => t.Balance)
                    .FirstOrDefaultAsync(ct);
                throw InsufficientTreasuryBalance(treasury.Name, current, -delta);
            }
        }
        else
        {
            // Existing row on a relational store: apply the delta as one atomic set-based UPDATE
            // so the read-and-write happen together under a row-level lock. Removes the lost-update
            // window and avoids spurious xmin concurrency failures (bypasses the change tracker).
            await db.Treasuries
                .Where(t => t.Id == treasury.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Balance, t => t.Balance + delta), ct);
        }

        // Reflect the new value on the tracked entity for in-request reads, but mark it
        // unmodified so the caller's SaveChanges does not re-issue a stale read-modify-write
        // (which would double-apply the delta and trip the xmin concurrency token).
        treasury.Balance += delta;
        entry.Property(t => t.Balance).IsModified = false;
    }

    private static ArgumentException InsufficientTreasuryBalance(string treasuryName, decimal currentBalance, decimal requiredAmount) =>
        new($"عذراً، رصيد الخزينة «{treasuryName}» غير كافٍ لهذه العملية. الرصيد الحالي: {currentBalance:N0} والمطلوب: {requiredAmount:N0}. يمكن للإدارة تعديل هذا الإعداد من إعدادات النظام.");
}
