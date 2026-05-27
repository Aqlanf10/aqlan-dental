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

        // BranchId guard: must have a valid branch assignment before opening a cashier session
        var branchId = currentUser.BranchId;
        if (branchId == null || branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل فتح صندوق الكاشير." });

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

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSession()
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        var session = await db.CashierSessions
            .Include(s => s.Cashier)
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return NotFound(new { message = "لا يوجد صندوق كاشير مفتوح حالياً لهذه الجلسة." });

        // Calculate expected closing values from CashFlowTransactions for the session
        var sessionTransactions = await db.CashFlowTransactions
            .Where(t => t.CashierSessionId == session.Id && t.IsActive)
            .ToListAsync();

        var cashInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && string.Equals(t.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var cashOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && string.Equals(t.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var cardInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && string.Equals(t.PaymentMethod, "card", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var cardOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && string.Equals(t.PaymentMethod, "card", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
        var bankInflows = sessionTransactions.Where(t => t.Type == TransactionType.Inflow && (string.Equals(t.PaymentMethod, "bank_transfer", StringComparison.OrdinalIgnoreCase) || string.Equals(t.PaymentMethod, "bank", StringComparison.OrdinalIgnoreCase))).Sum(t => t.Amount);
        var bankOutflows = sessionTransactions.Where(t => t.Type == TransactionType.Outflow && (string.Equals(t.PaymentMethod, "bank_transfer", StringComparison.OrdinalIgnoreCase) || string.Equals(t.PaymentMethod, "bank", StringComparison.OrdinalIgnoreCase))).Sum(t => t.Amount);

        var totalCollections = sessionTransactions.Where(t => t.Type == TransactionType.Inflow).Sum(t => t.Amount);

        return Ok(new
        {
            session.Id,
            session.SessionNumber,
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
            session.ShortageOrSurplus,
            Status = session.Status.ToString(),
            session.Notes,
            session.TreasuryId,
            TotalCollections = totalCollections
        });
    }

    [HttpPost("close")]
    public async Task<IActionResult> CloseSession([FromBody] CloseSessionRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

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

        await db.SaveChangesAsync();

        // H3: Audit logging for session close
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

    private static bool IsCashMethod(string method) =>
        string.Equals(method, "cash", StringComparison.OrdinalIgnoreCase);

    private static bool IsCardMethod(string method) =>
        string.Equals(method, "card", StringComparison.OrdinalIgnoreCase);

    private static bool IsBankMethod(string method) =>
        string.Equals(method, "bank_transfer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "bank", StringComparison.OrdinalIgnoreCase);

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

        // Non-admin can only see their own branch sessions
        if (currentUser.BranchId.HasValue && !currentUser.IsAdmin)
        {
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
        var session = await db.CashierSessions.FindAsync(id);
        if (session == null || !session.IsActive)
            return NotFound(new { message = "الوردية غير موجودة" });

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

        // H3: Audit logging for session reconciliation
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
}
