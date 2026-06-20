using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class OpenSessionRequest
{
    public decimal OpeningBalance { get; init; } // مبلغ العهدة الافتتاحية
    public string? Notes { get; init; }
}

public sealed class CloseSessionRequest
{
    public decimal ActualClosingCash { get; init; } // النقد الفعلي بالدرج
    public decimal ActualClosingCard { get; init; } // نقاط البيع الفعلية
    public decimal ActualClosingBank { get; init; } // التحويل البنكي الفعلي
    public string? Notes { get; init; }

    // FIN-03: When |ShortageOrSurplus| exceeds the threshold, a manager (Admin/Accountant)
    // must set this to true to approve the close. The controller verifies the caller's role.
    public bool ManagerOverrideApproved { get; init; }
}

// FIN-03: Threshold above which a manager must explicitly approve the closing balance.
// Shortages/surpluses within this amount are accepted as normal drawer variance; above it,
// the close is rejected with 400 until a manager co-signs (ManagerOverrideApproved=true).
// Tunable via Settings:CashierClosingApprovalThreshold; defaults to 5000 SAR.
public static class CashierClosingApprovalConfig
{
    public const decimal DefaultThreshold = 5000m;
    public const string SettingsKey = "CashierClosingApprovalThreshold";
}

[ApiController]
[Route("api/cashier-sessions")]
[Authorize(Policy = "FinanceAccess")] // Admin, Accountant, Reception
public class CashierSessionsController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit, ITreasuryResolutionService treasuryResolution, ILogger<CashierSessionsController> logger) : ControllerBase
{
    [HttpPost("open")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSessionRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        // BranchId resolution: use current user's branch, or fallback to first active branch for Admin
        var branchId = currentUser.BranchId;
        if (branchId == null || branchId == Guid.Empty)
        {
            var firstBranch = await db.Branches
                .Where(b => b.IsActive)
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            if (firstBranch == null)
                return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل فتح صندوق الكاشير. لا توجد فروع نشطة في النظام." });
            branchId = firstBranch.Id;
        }

        if (req.OpeningBalance < 0)
            return BadRequest(new { message = "لا يمكن أن يكون رصيد العهدة الافتتاحية سالباً" });

        // CONCURRENCY SAFETY: Begin the transaction BEFORE the authoritative open-session
        // eligibility check, then acquire a deterministic lock scoped to the cashier identity
        // to prevent two concurrent requests (or two Railway replicas) from creating two
        // open sessions for the same cashier.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire a deterministic transaction-scoped PostgreSQL lock for the cashier identity.
            // Uses StableGuidToLong(cashierId) which is deterministic across processes and restarts.
            // Do NOT use .NET GetHashCode which is not stable across app domains.
            if (db.Database.IsRelational())
            {
                var cashierLockKey = StableLockKeyHelper.StableGuidToLong(userId);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", cashierLockKey);
            }

            // AUTHORITATIVE RE-CHECK inside the lock: verify no open session exists for this cashier.
            // This check must happen after acquiring the lock to prevent concurrent OpenSession calls
            // from both passing the eligibility check before either creates a session.
            var hasOpenSession = await db.CashierSessions
                .AnyAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

            if (hasOpenSession)
                return BadRequest(new { message = "لديك وردية مفتوحة بالفعل. يجب إقفال الوردية الحالية أولاً قبل فتح وردية جديدة." });

            // Generate sequential SessionNumber CS-yyyyMMdd-NNN using advisory lock
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})", StableLockKeyHelper.CashierSessionNumber);
            }

            var today = DateTime.UtcNow;
            var datePart = today.ToString("yyyyMMdd");
            var prefix = $"CS-{datePart}-";

            var lastSession = await db.CashierSessions
                .IgnoreQueryFilters()
                .Where(s => s.SessionNumber.StartsWith(prefix))
                .OrderByDescending(s => s.SessionNumber)
                .Select(s => s.SessionNumber)
                .FirstOrDefaultAsync();

            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastSession) && lastSession.Length > prefix.Length)
            {
                var seqPart = lastSession[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq))
                    nextSeq = lastSeq + 1;
            }

            var sessionNumber = $"{prefix}{nextSeq:D2}";

            var session = new CashierSession
            {
                SessionNumber = sessionNumber,
                CashierId = userId,
                BranchId = branchId.Value,
                OpeningTime = DateTime.UtcNow,
                OpeningBalance = req.OpeningBalance,
                ExpectedClosingCash = req.OpeningBalance, // starts with just opening cash
                ExpectedClosingCard = 0,
                ExpectedClosingBank = 0,
                Status = SessionStatus.Open,
                Notes = req.Notes?.Trim(),
                // Explicitly resolve and set the cash vault TreasuryId when opening
                // the session so all subsequent cash movements tied to this session are routed
                // to the same treasury. OpeningBalance is a drawer-reconciliation seed only — it
                // does NOT adjust Treasury.Balance. Treasury.Balance reflects actual cash
                // movements (CashFlowTransaction outflows/inflows), not the cashier's opening
                // float. Double-counting would occur if we also incremented Treasury.Balance by
                // the opening balance, since patient cash receipts during the session already
                // increment it via CashFlowTransaction inflows.
                TreasuryId = await ResolveSessionTreasuryIdAsync(branchId.Value)
            };

            db.CashierSessions.Add(session);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for session open
            await audit.LogAsync(AuditAction.Create, "CashierSession", session.Id,
                details: $"Session {sessionNumber} opened");

            return Ok(new
            {
                session.Id,
                session.SessionNumber,
                session.OpeningTime,
                session.OpeningBalance,
                session.TreasuryId,
                Status = session.Status.ToString(),
                message = "تم فتح صندوق الكاشير والوردية اليومية بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpPost("close")]
    public async Task<IActionResult> CloseSession([FromBody] CloseSessionRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        // FIN-01 FIX: Wrap close in a transaction + advisory lock + re-check, mirroring OpenSession.
        // Previously the method loaded the session with Status==Open, mutated in memory, and called
        // SaveChangesAsync once with no transaction/lock. Two concurrent close requests both passed
        // the Open check and both saved — the second won, corrupting reconciliation.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire a deterministic transaction-scoped lock scoped to the cashier identity
            // (same key as OpenSession) so close + open cannot race on the same cashier.
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

            // Use CashFlowTransactions as the reconciled source for session financial movement.
            // This replaces the old Payment-based calculation which did NOT subtract
            // refunds, operational expenses, or other outflows — causing drawer overstatement.
            var sessionTransactions = await db.CashFlowTransactions
                .Where(t => t.CashierSessionId == session.Id && t.IsActive)
                .ToListAsync();

            var cashInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
            var cashOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
            var cardInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
            var cardOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
            var bankInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);
            var bankOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);

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
            session.Status = SessionStatus.Closed; // Locked!
            session.Notes = req.Notes?.Trim();

            // Backwards-compatibility pass: link any unlinked cashflow transactions
            // created during the session window that were not linked at creation time.
            // (New transactions should already be linked at creation time, but older
            // data or race conditions may leave some unlinked.)
            var unlinkedTransactions = await db.CashFlowTransactions
                .Where(t => t.CashierSessionId == null
                         && t.PerformedBy == userId
                         && t.CreatedAt >= session.OpeningTime
                         && t.IsActive)
                .ToListAsync();

            foreach (var t in unlinkedTransactions)
            {
                t.CashierSessionId = session.Id;
                // Phase 0B: Log warning for heuristically-linked transactions.
                // This linking is based on PerformedBy + CreatedAt matching, which is
                // imprecise — transactions created by another user on behalf of this
                // cashier, or system-generated transactions, may be missed or incorrectly linked.
                logger.LogWarning("Phase 0B: Heuristically linking unlinked CashFlowTransaction {TxId} to session {SessionId}. " +
                    "This transaction was not linked at creation time — investigate if this occurs frequently.",
                    t.Id, session.Id);
            }

            if (unlinkedTransactions.Count > 0)
            {
                sessionTransactions = await db.CashFlowTransactions
                    .Where(t => t.CashierSessionId == session.Id && t.IsActive)
                    .ToListAsync();

                var expected = CalculateExpectedAmounts(session.OpeningBalance, sessionTransactions);
                session.ExpectedClosingCash = expected.Cash;
                session.ExpectedClosingCard = expected.Card;
                session.ExpectedClosingBank = expected.Bank;

                expectedTotal = session.ExpectedClosingCash + session.ExpectedClosingCard + session.ExpectedClosingBank;
                actualTotal = req.ActualClosingCash + req.ActualClosingCard + req.ActualClosingBank;
                session.ShortageOrSurplus = actualTotal - expectedTotal;
            }

            // FIN-03: Manager co-sign requirement for large shortages/surpluses.
            // If |ShortageOrSurplus| exceeds the threshold, the close is rejected UNLESS:
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
                    "FIN-03: Cashier session {SessionId} closed with manager override. Variance={Variance}, Threshold={Threshold}, ApprovedBy={UserId} ({Role})",
                    session.Id, variance, threshold, currentUser.UserId, currentUser.Role);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for session close (outside tx — non-fatal if it fails)
            await audit.LogAsync(AuditAction.Update, "CashierSession", session.Id,
                details: $"Session closed, surplus/shortage: {session.ShortageOrSurplus}");

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
                Status = session.Status.ToString(),
                message = "تم إقفال صندوق الاستقبال وترحيل المبالغ وتأمين القيود بنجاح"
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "CloseSession failed for cashier {UserId}", userId);
            throw;
        }
    }

    private static (decimal Cash, decimal Card, decimal Bank) CalculateExpectedAmounts(
        decimal openingBalance,
        IReadOnlyCollection<CashFlowTransaction> transactions)
    {
        var cashInflows = transactions.Where(t => t.Type == TransactionType.Inflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cashOutflows = transactions.Where(t => t.Type == TransactionType.Outflow && IsCashMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cardInflows = transactions.Where(t => t.Type == TransactionType.Inflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var cardOutflows = transactions.Where(t => t.Type == TransactionType.Outflow && IsCardMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var bankInflows = transactions.Where(t => t.Type == TransactionType.Inflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);
        var bankOutflows = transactions.Where(t => t.Type == TransactionType.Outflow && IsBankMethod(t.PaymentMethod)).Sum(t => t.Amount);

        return (
            openingBalance + cashInflows - cashOutflows,
            cardInflows - cardOutflows,
            bankInflows - bankOutflows
        );
    }

    private static string NormalizePaymentMethod(string? method)
    {
        var value = (method ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ");

        return value switch
        {
            "cash" or "نقدي" or "نقدا" => "cash",
            "card" or "credit card" or "debit card" or "بطاقة" => "card",
            "bank" or "bank transfer" or "transfer" or "تحويل بنكي" or "حوالة" or "karimey" or "jawaly" or "check" => "bank",
            _ => value
        };
    }

    private static bool IsCashMethod(string? method) => NormalizePaymentMethod(method) == "cash";

    private static bool IsCardMethod(string? method) => NormalizePaymentMethod(method) == "card";

    private static bool IsBankMethod(string? method) => NormalizePaymentMethod(method) == "bank";

    /// <summary>
    /// Resolves the branch cash Vault treasury for a CashierSession.
    /// Uses the centralized TreasuryResolutionService to find or auto-create
    /// the vault treasury for the branch. Returns the TreasuryId.
    /// </summary>
    private async Task<Guid> ResolveSessionTreasuryIdAsync(Guid branchId)
    {
        var treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, "cash", null);
        return treasury.Id;
    }

    /// <summary>
    /// GET /api/cashier-sessions/daily-summary — Operational KPIs for Reception daily checkout.
    /// FinanceAccess (Admin, Reception, Accountant). Excludes deep report fields (commissions, journal health).
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<IActionResult> GetDailySummary()
    {
        var branchId = currentUser.BranchId;
        if (!currentUser.IsAdmin && (!branchId.HasValue || branchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var scopedBranch = currentUser.IsAdmin ? (Guid?)null : branchId;
        var today = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday());
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var paymentsQuery = db.Payments.Where(p => p.IsActive);
        if (scopedBranch.HasValue)
            paymentsQuery = paymentsQuery.Where(p => p.BranchId == scopedBranch.Value);

        var todayInflow = await paymentsQuery
            .Where(p => p.PaymentDate == today)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;
        var monthInflow = await paymentsQuery
            .Where(p => p.PaymentDate >= monthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var activeContractsQuery = db.Contracts.Where(c => c.Status == ContractStatus.Active);
        if (scopedBranch.HasValue)
            activeContractsQuery = activeContractsQuery.Where(c => c.Patient.BranchId == scopedBranch.Value);
        var activeContracts = await activeContractsQuery.CountAsync();

        var unpaidInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Issued && i.IsActive);
        if (scopedBranch.HasValue)
            unpaidInvoicesQuery = unpaidInvoicesQuery.Where(i => i.Patient.BranchId == scopedBranch.Value);
        var unpaidInvoicesCount = await unpaidInvoicesQuery.CountAsync();

        var draftInvoicesQuery = db.Invoices.Where(i => i.Status == InvoiceStatus.Draft && i.IsActive);
        if (scopedBranch.HasValue)
            draftInvoicesQuery = draftInvoicesQuery.Where(i => i.Patient.BranchId == scopedBranch.Value);
        var draftInvoicesCount = await draftInvoicesQuery.CountAsync();

        var recentPaymentsRaw = await paymentsQuery
            .Include(p => p.Patient)
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.CreatedAt)
            .Take(10)
            .ToListAsync();
        var recentPayments = recentPaymentsRaw.Select(p => new
        {
            p.Id,
            p.Amount,
            PaymentDate = p.PaymentDate.ToString(),
            PatientName = p.Patient != null ? (p.Patient.FirstName + " " + p.Patient.LastName).Trim() : "",
            p.PaymentMethod,
        }).ToList();

        var recentInvoicesQuery = db.Invoices.Include(i => i.Patient).Where(i => i.IsActive);
        if (scopedBranch.HasValue)
            recentInvoicesQuery = recentInvoicesQuery.Where(i => i.Patient.BranchId == scopedBranch.Value);
        var recentInvoices = await recentInvoicesQuery
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .Select(i => new { i.Id, i.InvoiceNumber, TotalAmount = i.TotalAmount, Status = i.Status.ToString() })
            .ToListAsync();

        return Ok(new
        {
            TodayInflow = todayInflow,
            MonthInflow = monthInflow,
            ActiveContracts = activeContracts,
            UnpaidInvoicesCount = unpaidInvoicesCount,
            DraftInvoicesCount = draftInvoicesCount,
            RecentPayments = recentPayments,
            RecentInvoices = recentInvoices,
        });
    }

    [HttpGet("active")]
    // FIN-24: This legacy endpoint returns CashFlowTransaction-based expected amounts (cashflowExpected)
    // while the V3 endpoint (FinanceV3Controller.CashierSessions.GetActiveCashierSessionV3) returns
    // JournalLine-based expected amounts. During the dual-write migration these can drift.
    // TODO: Once the CashFlowTransaction dual-write is removed, delete this endpoint and route
    // Reception through V3 with an appropriate policy (or add Reception to ReportsAccess).
    [Obsolete("Use GET /api/finance-v3/cashier-sessions/active instead where ReportsAccess is allowed. This legacy endpoint remains fully active and returns canonical session data to preserve Reception access under FinanceAccess policy.")]
    public async Task<IActionResult> GetActiveSession()
    {
        var cashierId = currentUser.UserId ?? Guid.Empty;

        var session = await db.CashierSessions
            .Include(s => s.Cashier)
            .Include(s => s.Treasury)
            .FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return Ok(new { hasActiveSession = false });

        // Calculate expected values from JournalLine instead of CashFlowTransaction
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
        var treasuryTypes = await db.Treasuries
            .Where(t => treasuryIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => (TreasuryType?)t.Type);

        // Classify each line by payment method (Treasury.Type) and direction (Debit/Credit)
        decimal cashInflows = 0, cashOutflows = 0;
        decimal bankInflows = 0, bankOutflows = 0;

        foreach (var line in sessionJournalLines)
        {
            var tType = treasuryTypes.GetValueOrDefault(line.AccountId);
            var isCash = tType == TreasuryType.Vault || tType == null; // Vault or unknown → cash
            var isBank = tType == TreasuryType.Bank;                       // bank account

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
        var totalCollections = sessionJournalLines.Where(l => l.Debit > 0).Sum(l => l.Debit);

        var cashflowTransactions = await db.CashFlowTransactions
            .Where(t => t.IsActive
                && (t.CashierSessionId == session.Id
                    || (t.CashierSessionId == null
                        && t.PerformedBy == cashierId
                        && t.CreatedAt >= session.OpeningTime)))
            .ToListAsync();
        var cashflowExpected = CalculateExpectedAmounts(session.OpeningBalance, cashflowTransactions);
        var cashflowCollections = cashflowTransactions
            .Where(t => t.Type == TransactionType.Inflow)
            .Sum(t => t.Amount);

        return Ok(new
        {
            hasActiveSession = true,
            session.Id,
            session.SessionNumber,
            CashierId = session.CashierId,
            CashierName = session.Cashier?.Username ?? "",
            session.BranchId,
            OpenedAt = session.OpeningTime,
            session.ClosingTime,
            session.OpeningBalance,
            ExpectedClosingCash = cashflowExpected.Cash,
            ExpectedClosingCard = cashflowExpected.Card,
            ExpectedClosingBank = cashflowExpected.Bank,
            ActualClosingCash = (decimal?)session.ActualClosingCash,
            ActualClosingCard = (decimal?)session.ActualClosingCard,
            ActualClosingBank = (decimal?)session.ActualClosingBank,
            ShortageOrSurplus = (decimal?)session.ShortageOrSurplus,
            Status = session.Status.ToString(),
            session.Notes,
            session.TreasuryId,
            TotalCollections = cashflowCollections
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.CashierSessions
            .Include(s => s.Cashier)
            .Where(s => s.IsActive)
            .AsQueryable();

        // Non-admin must have a valid branch assignment and can only see their own branch sessions
        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
                return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });
            query = query.Where(s => s.BranchId == currentUser.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SessionStatus>(status, true, out var statusFilter))
        {
            query = query.Where(s => s.Status == statusFilter);
        }

        var total = await query.CountAsync();

        var sessions = await query
            .OrderByDescending(s => s.OpeningTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.SessionNumber,
                CashierName = s.Cashier.Username,
                s.CashierId,
                s.BranchId,
                OpenedAt = s.OpeningTime,
                s.ClosingTime,
                s.OpeningBalance,
                s.ExpectedClosingCash,
                s.ExpectedClosingCard,
                s.ExpectedClosingBank,
                s.ActualClosingCash,
                s.ActualClosingCard,
                s.ActualClosingBank,
                s.ShortageOrSurplus,
                Status = s.Status.ToString(),
                s.Notes,
                s.TreasuryId
            })
            .ToListAsync();

        return Ok(new { data = sessions, total, page, pageSize });
    }

    // ─── H13: Cashier Session Detail Endpoint ───────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSessionDetail(Guid id)
    {
        var session = await db.CashierSessions
            .Include(s => s.Cashier)
            .Include(s => s.Transactions)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (session == null)
            return NotFound(new { message = "الوردية غير موجودة" });

        // Non-admin can only see their own branch sessions
        if (!currentUser.IsAdmin && currentUser.BranchId.HasValue && session.BranchId != currentUser.BranchId.Value)
            return Forbid();

        var transactions = session.Transactions
            .Where(t => t.IsActive)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                Type = t.Type.ToString(),
                Category = t.Category.ToString(),
                t.Amount,
                t.PaymentMethod,
                t.Description,
                t.ReferenceNumber,
                t.IsReversal,
                PerformedBy = t.PerformedBy
            })
            .ToList();

        return Ok(new
        {
            session.Id,
            session.SessionNumber,
            Status = session.Status.ToString(),
            session.OpeningTime,
            session.ClosingTime,
            session.OpeningBalance,
            session.ExpectedClosingCash,
            session.ExpectedClosingCard,
            session.ExpectedClosingBank,
            session.ActualClosingCash,
            session.ActualClosingCard,
            session.ActualClosingBank,
            session.ShortageOrSurplus,
            CashierName = session.Cashier?.Username,
            Transactions = transactions
        });
    }

    [HttpPatch("{id:guid}/reconcile")]
    [Authorize(Policy = "ReportsAccess")] // Admin + Accountant only
    public async Task<IActionResult> ReconcileSession(Guid id, [FromBody] string? notes)
    {
        // FIN-02 FIX: Wrap reconcile in a transaction + lock + re-check. Previously the method
        // only checked Status != Closed before mutating to Reconciled — two concurrent reconcile
        // calls both saw Closed and both set Reconciled.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Lock on the session id so two concurrent reconcile calls serialize.
            if (db.Database.IsRelational())
            {
                var sessionLockKey = StableLockKeyHelper.StableGuidToLong(id);
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", sessionLockKey);
            }

            var session = await db.CashierSessions.FindAsync(id);
            if (session == null || !session.IsActive)
                return NotFound(new { message = "الوردية غير موجودة" });

            // AUTHORITATIVE RE-CHECK inside the lock: a concurrent reconcile that won the race
            // will have set Status=Reconciled.
            if (session.Status == SessionStatus.Reconciled)
                return BadRequest(new { message = "تمت مطابقة هذه الوردية بالفعل" });

            if (session.Status != SessionStatus.Closed)
                return BadRequest(new { message = "يمكن مطابقة الورديات المغلقة فقط" });

            session.Status = SessionStatus.Reconciled;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                session.Notes = string.IsNullOrWhiteSpace(session.Notes)
                    ? $"[مطابقة] {notes}"
                    : $"{session.Notes}\n[مطابقة] {notes}";
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for session reconciliation (outside tx — non-fatal if it fails)
            await audit.LogAsync(AuditAction.Update, "CashierSession", id,
                details: "Session reconciled");

            return Ok(new
            {
                session.Id,
                session.SessionNumber,
                Status = session.Status.ToString(),
                message = "تمت المطابقة والاعتماد المحاسبي للوردية اليومية بنجاح"
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "ReconcileSession failed for session {SessionId}", id);
            throw;
        }
    }
}
