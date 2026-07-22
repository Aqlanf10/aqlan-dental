using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    private sealed record TreasurySessionMetadata(TreasuryType Type, string Currency);

    private static bool IsYemeniCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) || string.Equals(currency.Trim(), "YER", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCurrency(string? currency) =>
        string.IsNullOrWhiteSpace(currency) ? "YER" : currency.Trim().ToUpperInvariant();

    // ─── Cashier Sessions Active (Finance V3) ──────────────────────────────

    /// <summary>
    /// GET /api/finance-v3/cashier-sessions/active — Get the active cashier session for the current user.
    /// Returns the session with the proper shape expected by the Finance V3 frontend.
    /// Migration C: Expected values now calculated from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. Payment method is determined by Treasury.Type:
    ///   Vault → cash, Bank → bank_transfer/card.
    /// Inflow = Treasury Debit (money received), Outflow = Treasury Credit (money paid).
    /// Only posted JournalEntries within the session time range are included.
    /// </summary>
    [HttpGet("cashier-sessions/active")]
    public async Task<IActionResult> GetActiveCashierSessionV3()
    {
        if (!await CanAsync("finance.cashier_session", "view")) return Deny();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var cashierId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;

        var session = await db.CashierSessions
            .Include(s => s.Cashier)
            .Include(s => s.Treasury)
            .FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return Ok(new { hasActiveSession = false });

        // Migration C: Calculate expected values from JournalLine instead of CashFlowTransaction
        // Get all Treasury JournalLines from posted JournalEntries created by this cashier
        // during the session time range
        var sessionJournalLines = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.JournalEntry.IsPosted
                && l.JournalEntry.PerformedBy == cashierId
                && l.JournalEntry.CreatedAt >= session.OpeningTime
                && l.JournalEntry.CreatedAt <= DateTime.UtcNow)
            .Select(l => new
            {
                l.JournalEntryId,
                l.Debit,
                l.Credit,
                l.AccountId // TreasuryId
            })
            .ToListAsync();

        // Load treasury types for payment method mapping
        var treasuryIds = sessionJournalLines.Select(l => l.AccountId).Distinct().ToList();
        var treasuryMetadata = await db.Treasuries
            .Where(t => treasuryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => new TreasurySessionMetadata(t.Type, t.Currency));

        // Classify each line by payment method (Treasury.Type) and direction (Debit/Credit)
        // Vault-type treasury → cash, Bank-type treasury → bank/card
        decimal cashInflows = 0, cashOutflows = 0;
        decimal bankInflows = 0, bankOutflows = 0;

        foreach (var line in sessionJournalLines)
        {
            var treasury = treasuryMetadata.GetValueOrDefault(line.AccountId);
            if (treasury != null && !IsYemeniCurrency(treasury.Currency))
                continue;

            var isCash = treasury?.Type == TreasuryType.Vault || treasury == null;
            var isBank = treasury?.Type == TreasuryType.Bank;

            if (line.Debit > 0) // Inflow (money received into treasury)
            {
                if (isCash) cashInflows += line.Debit;
                else if (isBank) bankInflows += line.Debit;
            }
            else if (line.Credit > 0) // Outflow (money paid from treasury)
            {
                if (isCash) cashOutflows += line.Credit;
                else if (isBank) bankOutflows += line.Credit;
            }
        }

        var cardInflows = bankInflows;
        var cardOutflows = bankOutflows;
        var totalCollections = cashInflows + bankInflows;
        var foreignCurrencyActivity = sessionJournalLines
            .Select(line => new
            {
                Currency = treasuryMetadata.TryGetValue(line.AccountId, out var treasury)
                    ? NormalizeCurrency(treasury.Currency)
                    : "YER",
                IsCash = !treasuryMetadata.TryGetValue(line.AccountId, out var treasuryType)
                    || treasuryType.Type == TreasuryType.Vault,
                line.Debit,
                line.Credit
            })
            .Where(line => !IsYemeniCurrency(line.Currency))
            .GroupBy(line => line.Currency)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Currency = group.Key,
                CashInflows = group.Where(line => line.IsCash).Sum(line => line.Debit),
                CashOutflows = group.Where(line => line.IsCash).Sum(line => line.Credit),
                BankInflows = group.Where(line => !line.IsCash).Sum(line => line.Debit),
                BankOutflows = group.Where(line => !line.IsCash).Sum(line => line.Credit),
                NetCash = group.Where(line => line.IsCash).Sum(line => line.Debit - line.Credit),
                NetBank = group.Where(line => !line.IsCash).Sum(line => line.Debit - line.Credit)
            })
            .ToList();

        return Ok(new
        {
            hasActiveSession = true,
            session.Id,
            CashierId = session.CashierId,
            CashierName = session.Cashier?.Username ?? "",
            session.BranchId,
            OpenedAt = session.OpeningTime,
            session.ClosingTime,
            session.OpeningBalance,
            ExpectedClosingCash = session.OpeningBalance + cashInflows - cashOutflows,
            ExpectedClosingCard = cardInflows - cardOutflows,
            ExpectedClosingBank = bankInflows - bankOutflows,
            ActualClosingCash = (decimal?)session.ActualClosingCash,
            ActualClosingCard = (decimal?)session.ActualClosingCard,
            ActualClosingBank = (decimal?)session.ActualClosingBank,
            ShortageOrSurplus = (decimal?)session.ShortageOrSurplus,
            Status = session.Status.ToString(),
            session.Notes,
            session.TreasuryId,
            ForeignCurrencyActivity = foreignCurrencyActivity,
            TotalCollections = totalCollections
        });
    }

    /// <summary>
    /// POST /api/finance-v3/cashier-sessions/close — Close the active cashier session.
    /// Migration C: Expected values now calculated from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. Unlinked JournalEntries are linked to the session
    /// instead of unlinked CashFlowTransactions. Payment method is determined by Treasury.Type.
    /// </summary>
    [HttpPost("cashier-sessions/close")]
    [Authorize(Policy = "CashierAccess")]
    public async Task<IActionResult> CloseCashierSession([FromBody] CloseSessionRequest req)
    {
        if (!await CanAsync("finance.cashier_session", "create")) return Deny();
        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح." });

        // Amount validation: reject negative actual closing values
        if (req.ActualClosingCash < 0)
            return BadRequest(new { message = "النقدي الفعلي لا يمكن أن يكون سالباً" });
        if (req.ActualClosingCard < 0)
            return BadRequest(new { message = "البطاقة الفعلية لا يمكن أن تكون سالبة" });
        if (req.ActualClosingBank < 0)
            return BadRequest(new { message = "البنكي الفعلي لا يمكن أن يكون سالباً" });

        var userId = currentUser.UserId ?? Guid.Empty;

        // C-03 V3 FIX: Wrap close in a transaction + advisory lock + re-check, mirroring the
        // legacy CashierSessionsController.CloseSession pattern. Previously the V3 path loaded
        // the session with Status==Open, mutated in memory, and called SaveChangesAsync once
        // with no transaction/lock. Two concurrent close requests both passed the Open check
        // and both saved — the second won, corrupting reconciliation. Additionally, manager
        // co-sign (FIN-03) was missing on the V3 path, so a cashier could hide a large cash
        // shortage by submitting ActualClosingCash = ExpectedClosingCash.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire a deterministic transaction-scoped lock scoped to the cashier identity
            // (same key as the legacy OpenSession path) so close + open cannot race on the
            // same cashier.
            if (db.Database.IsRelational())
            {
                var cashierLockKey = StableLockKeyHelper.StableGuidToLong(userId);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", cashierLockKey);
            }

            // AUTHORITATIVE RE-CHECK inside the lock: reload the open session for this cashier.
            // A concurrent close that won the race will have set Status=Closed, so this returns null.
            var session = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

            if (session == null)
                return BadRequest(new { message = "لا يوجد صندوق مفتوح حالياً لإقفاله." });

            // Migration C: Calculate expected values from JournalLine instead of CashFlowTransaction
            var sessionJournalLines = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.JournalEntry.IsPosted
                    && l.JournalEntry.PerformedBy == userId
                    && l.JournalEntry.CreatedAt >= session.OpeningTime
                    && l.JournalEntry.CreatedAt <= DateTime.UtcNow)
                .Select(l => new
                {
                    l.JournalEntryId,
                    l.Debit,
                    l.Credit,
                    l.AccountId // TreasuryId
                })
                .ToListAsync();

            // Load treasury types for payment method mapping
            var treasuryIds = sessionJournalLines.Select(l => l.AccountId).Distinct().ToList();
            var treasuryMetadata = await db.Treasuries
                .Where(t => treasuryIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => new TreasurySessionMetadata(t.Type, t.Currency));

            // Classify each line by payment method (Treasury.Type) and direction (Debit/Credit)
            decimal cashInflows = 0, cashOutflows = 0;
            decimal bankInflows = 0, bankOutflows = 0;

            foreach (var line in sessionJournalLines)
            {
                var treasury = treasuryMetadata.GetValueOrDefault(line.AccountId);
                if (treasury != null && !IsYemeniCurrency(treasury.Currency))
                    continue;

                var isCash = treasury?.Type == TreasuryType.Vault || treasury == null;
                var isBank = treasury?.Type == TreasuryType.Bank;

                if (line.Debit > 0) // Inflow
                {
                    if (isCash) cashInflows += line.Debit;
                    else if (isBank) bankInflows += line.Debit;
                }
                else if (line.Credit > 0) // Outflow
                {
                    if (isCash) cashOutflows += line.Credit;
                    else if (isBank) bankOutflows += line.Credit;
                }
            }

            var cardInflows = bankInflows;
            var cardOutflows = bankOutflows;
            var foreignCurrencyActivity = sessionJournalLines
                .Select(line => new
                {
                    Currency = treasuryMetadata.TryGetValue(line.AccountId, out var treasury)
                        ? NormalizeCurrency(treasury.Currency)
                        : "YER",
                    IsCash = !treasuryMetadata.TryGetValue(line.AccountId, out var treasuryType)
                        || treasuryType.Type == TreasuryType.Vault,
                    line.Debit,
                    line.Credit
                })
                .Where(line => !IsYemeniCurrency(line.Currency))
                .GroupBy(line => line.Currency)
                .OrderBy(group => group.Key)
                .Select(group => new
                {
                    Currency = group.Key,
                    CashInflows = group.Where(line => line.IsCash).Sum(line => line.Debit),
                    CashOutflows = group.Where(line => line.IsCash).Sum(line => line.Credit),
                    BankInflows = group.Where(line => !line.IsCash).Sum(line => line.Debit),
                    BankOutflows = group.Where(line => !line.IsCash).Sum(line => line.Credit),
                    NetCash = group.Where(line => line.IsCash).Sum(line => line.Debit - line.Credit),
                    NetBank = group.Where(line => !line.IsCash).Sum(line => line.Debit - line.Credit)
                })
                .ToList();
            session.ExpectedClosingCash = session.OpeningBalance + cashInflows - cashOutflows;
            session.ExpectedClosingCard = cardInflows - cardOutflows;
            session.ExpectedClosingBank = bankInflows - bankOutflows;
            session.ActualClosingCash = req.ActualClosingCash;
            session.ActualClosingCard = req.ActualClosingCard;
            session.ActualClosingBank = req.ActualClosingBank;

            var expectedTotal = session.ExpectedClosingCash + session.ExpectedClosingCard + session.ExpectedClosingBank;
            var actualTotal = req.ActualClosingCash + req.ActualClosingCard + req.ActualClosingBank;
            session.ShortageOrSurplus = actualTotal - expectedTotal;
            session.ClosingTime = DateTime.UtcNow;
            session.Status = SessionStatus.Closed;
            session.Notes = req.Notes?.Trim();

            // FIN-03 V3 FIX: Manager co-sign requirement for large shortages/surpluses.
            // Mirrors the legacy CashierSessionsController.CloseSession logic. If
            // |ShortageOrSurplus| exceeds the threshold, the close is rejected UNLESS:
            //   1. req.ManagerOverrideApproved == true, AND
            //   2. The current user is Admin or Accountant (the cashier cannot self-approve).
            // This prevents a cashier from hiding a large cash shortage by submitting
            // ActualClosingCash = ExpectedClosingCash.
            var threshold = CashierClosingApprovalConfig.DefaultThreshold;
            var settingsThreshold = await db.Settings
                .Where(s => s.Key == CashierClosingApprovalConfig.SettingsKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            if (decimal.TryParse(settingsThreshold, out var configured) && configured > 0)
                threshold = configured;

            var variance = Math.Abs(session.ShortageOrSurplus ?? 0);
            if (variance > threshold)
            {
                var isManager = currentUser.IsAdmin || currentUser.Role == UserRole.Accountant;
                if (!req.ManagerOverrideApproved || !isManager)
                {
                    return BadRequest(new
                    {
                        message = $"الفرق بين الرصيد الفعلي والمتوقع ({session.ShortageOrSurplus:N0} ر.ي) يتجاوز الحد المسموح ({threshold:N0} ر.ي). " +
                                  "يلزم موافقة المدير (Admin/Accountant) مع تفعيل ManagerOverrideApproved=true لإتمام الإقفال.",
                        shortageOrSurplus = session.ShortageOrSurplus,
                        threshold,
                        requiresManagerApproval = true
                    });
                }

                // Manager approved — record in audit log.
                logger.LogWarning(
                    "FIN-03 V3: Cashier session {SessionId} closed with manager override. Variance={Variance}, Threshold={Threshold}, ApprovedBy={UserId} ({Role})",
                    session.Id, variance, threshold, currentUser.UserId, currentUser.Role);
            }

            // Migration C: Link any unlinked JournalEntries (instead of CashFlowTransactions)
            // to this session for audit trail completeness
            var unlinkedEntries = await db.JournalEntries
                .Where(je => je.CashierSessionId == null
                    && je.PerformedBy == userId
                    && je.CreatedAt >= session.OpeningTime
                    && je.IsPosted)
                .ToListAsync();
            foreach (var je in unlinkedEntries)
                je.CashierSessionId = session.Id;

            // Also link unlinked CashFlowTransactions for backward compatibility
            // (dual-write: both CashFlowTransaction and JournalEntry exist)
            var unlinkedTransactions = await db.CashFlowTransactions
                .Where(t => t.CashierSessionId == null && t.PerformedBy == userId && t.CreatedAt >= session.OpeningTime && t.IsActive)
                .ToListAsync();
            foreach (var t in unlinkedTransactions)
                t.CashierSessionId = session.Id;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Update, "CashierSession", session.Id,
                details: $"Session closed via V3, surplus/shortage: {session.ShortageOrSurplus}");

            return Ok(new
            {
                session.Id,
                session.SessionNumber,
                session.OpeningTime,
                session.ClosingTime,
                session.OpeningBalance,
                session.ExpectedClosingCash,
                session.ActualClosingCash,
                session.ExpectedClosingCard,
                session.ActualClosingCard,
                session.ExpectedClosingBank,
                session.ActualClosingBank,
                session.ShortageOrSurplus,
                ForeignCurrencyActivity = foreignCurrencyActivity,
                Status = session.Status.ToString(),
                message = "تم إقفال صندوق الاستقبال وترحيل المبالغ وتأمين القيود بنجاح"
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            // DB-02: xmin concurrency token on CashierSession detected a concurrent edit.
            await tx.RollbackAsync();
            return Conflict(new { message = "تم تعديل الجلسة من قبل مستخدم آخر، يرجى التحديث والمحاولة مرة أخرى" });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "CloseCashierSession (V3) failed for cashier {UserId}", userId);
            throw;
        }
    }
    /// <summary>
    /// PATCH /api/finance-v3/cashier-sessions/{id}/reconcile — Reconcile a closed session.
    /// </summary>
    [HttpPatch("cashier-sessions/{id:guid}/reconcile")]
    [Authorize(Policy = "ReportsAccess")]
    public async Task<IActionResult> ReconcileCashierSession(Guid id, [FromBody] string? notes)
    {
        if (!await CanAsync("finance.cashier_session", "approve")) return Deny();
        var session = await db.CashierSessions.FindAsync(id);
        if (session == null || !session.IsActive)
            return NotFound(new { message = "الوردية غير موجودة" });
        if (session.Status != SessionStatus.Closed)
            return BadRequest(new { message = "يمكن مطابقة الورديات المغلقة فقط" });

        session.Status = SessionStatus.Reconciled;
        if (!string.IsNullOrWhiteSpace(notes))
            session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                ? $"[مطابقة] {notes}" : $"{session.Notes}\n[مطابقة] {notes}";

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "CashierSession", id, details: "Session reconciled via V3");

        return Ok(new { session.Id, session.SessionNumber, Status = session.Status.ToString(), message = "تمت المطابقة والاعتماد المحاسبي للوردية اليومية بنجاح" });
    }
}
