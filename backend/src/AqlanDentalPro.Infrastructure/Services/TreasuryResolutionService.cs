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

        if (treasury.Balance - amount < 0)
        {
            // Configurable guard (Settings key below, value "true" to enforce).
            // Default is warn-only so existing deployments with unseeded opening
            // balances keep working until the accountant enables enforcement.
            if (await IsNegativeBalanceBlockedAsync(ct))
                throw new ArgumentException(
                    $"عذراً، رصيد الخزينة «{treasury.Name}» غير كافٍ لهذه العملية. الرصيد الحالي: {treasury.Balance:N0} والمطلوب: {amount:N0}. يمكن للإدارة تعديل هذا الإعداد من إعدادات النظام.");

            logger.LogWarning(
                "Treasury {TreasuryId} ({Name}) balance going NEGATIVE: balance {Balance} - outflow {Amount}. Enable setting '{SettingKey}' to block this.",
                treasury.Id, treasury.Name, treasury.Balance, amount, PreventNegativeBalanceSettingKey);
        }

        await MutateTreasuryBalanceAsync(treasury, -amount, ct);

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
        await MutateTreasuryBalanceAsync(treasury, amount, ct);

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

        await MutateTreasuryBalanceAsync(treasury, amount, ct);

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
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static bool IsBankPaymentMethod(string? paymentMethod)
    {
        return string.Equals(paymentMethod, "card", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod, "bank_transfer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentMethod, "bank", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mutates treasury balance atomically. Uses raw SQL on relational DB for concurrency safety.
    /// Falls back to in-memory mutation for InMemory provider (test scenarios).
    /// Does NOT call SaveChangesAsync.
    /// </summary>
    private async Task MutateTreasuryBalanceAsync(Treasury treasury, decimal delta, CancellationToken ct)
    {
        // Direct balance update (no raw SQL). ExecuteSqlRawAsync inside a transaction
        // causes DbContext concurrency issues ("A second operation was started on this
        // context instance"). The caller's transaction provides atomicity guarantees.
        treasury.Balance += delta;
    }
}
