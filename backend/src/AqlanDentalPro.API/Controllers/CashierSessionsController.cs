using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
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
public class CashierSessionsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("open")]
    public async Task<IActionResult> OpenSession([FromBody] OpenSessionRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId ?? Guid.Empty;

        // Check if there is already an open session for this user
        var hasOpenSession = await db.CashierSessions
            .AnyAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

        if (hasOpenSession)
            return BadRequest(new { message = "لديك وردية مفتوحة بالفعل. يجب إقفال الوردية الحالية أولاً قبل فتح وردية جديدة." });

        if (req.OpeningBalance < 0)
            return BadRequest(new { message = "لا يمكن أن يكون رصيد العهدة الافتتاحية سالباً" });

        // Generate sequential SessionNumber CS-yyyyMMdd-NNN using advisory lock
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = Math.Abs("CashierSessionNumber".GetHashCode()) % 100000;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

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
                BranchId = branchId,
                OpeningTime = DateTime.UtcNow,
                OpeningBalance = req.OpeningBalance,
                ExpectedClosingCash = req.OpeningBalance, // starts with just opening cash
                ExpectedClosingCard = 0,
                ExpectedClosingBank = 0,
                Status = SessionStatus.Open,
                Notes = req.Notes?.Trim()
            };

            db.CashierSessions.Add(session);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                session.Id,
                session.SessionNumber,
                session.OpeningTime,
                session.OpeningBalance,
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
            .Where(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive)
            .Select(s => new
            {
                s.Id,
                s.SessionNumber,
                s.OpeningTime,
                s.OpeningBalance,
                Status = s.Status.ToString()
            })
            .FirstOrDefaultAsync();

        if (session == null)
            return NotFound(new { message = "لا يوجد صندوق كاشير مفتوح حالياً لهذه الجلسة." });

        return Ok(session);
    }

    [HttpPost("close")]
    public async Task<IActionResult> CloseSession([FromBody] CloseSessionRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        var session = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return BadRequest(new { message = "لا يوجد صندوق مفتوح حالياً لإقفاله." });

        // Calculate expected receipts during this session
        // Every Payment recorded between session.OpeningTime and Now by this cashier.
        var payments = await db.Payments
            .Where(p => p.ReceivedBy == userId 
                     && p.CreatedAt >= session.OpeningTime 
                     && p.IsActive)
            .ToListAsync();

        var totalCashPayments = payments.Where(p => p.PaymentMethod == "cash").Sum(p => p.Amount);
        var totalCardPayments = payments.Where(p => p.PaymentMethod == "card").Sum(p => p.Amount);
        var totalBankPayments = payments.Where(p => p.PaymentMethod == "bank_transfer" || p.PaymentMethod == "bank").Sum(p => p.Amount);

        session.ExpectedClosingCash = session.OpeningBalance + totalCashPayments;
        session.ExpectedClosingCard = totalCardPayments;
        session.ExpectedClosingBank = totalBankPayments;

        session.ActualClosingCash = req.ActualClosingCash;
        session.ActualClosingCard = req.ActualClosingCard;
        session.ActualClosingBank = req.ActualClosingBank;

        var expectedTotal = session.ExpectedClosingCash + session.ExpectedClosingCard + session.ExpectedClosingBank;
        var actualTotal = req.ActualClosingCash + req.ActualClosingCard + req.ActualClosingBank;
        session.ShortageOrSurplus = actualTotal - expectedTotal;

        session.ClosingTime = DateTime.UtcNow;
        session.Status = SessionStatus.Closed; // Locked!
        session.Notes = req.Notes?.Trim();

        // Automatically link all payments created during this session to the session record
        foreach (var p in payments)
        {
            // Update Payments directly to link them to this cashier session
            // We need to fetch from database to track them, but since we already queried them, they are in the tracker.
            // Let's verify if Payment has CashierSessionId property. It was added as FK index, let's update it in DbContext or via direct SQL if needed.
            // Wait! Did we add CashierSessionId on Payment entity? Yes, CashFlowTransaction has it, and let's check if we should link it or if it is already tracked.
            // In CashFlowTransaction we definitely have CashierSessionId. Let's link them in CashFlowTransaction!
            var cashflow = await db.CashFlowTransactions
                .Where(t => t.ReferenceId == p.Id && t.Category == FinancialCategory.PatientPayment && t.IsActive)
                .ToListAsync();
            foreach (var cf in cashflow)
            {
                cf.CashierSessionId = session.Id;
            }
        }

        await db.SaveChangesAsync();

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
                s.OpeningTime,
                s.ClosingTime,
                s.OpeningBalance,
                ExpectedTotal = s.ExpectedClosingCash + s.ExpectedClosingCard + s.ExpectedClosingBank,
                ActualTotal = (s.ActualClosingCash ?? 0) + (s.ActualClosingCard ?? 0) + (s.ActualClosingBank ?? 0),
                s.ShortageOrSurplus,
                Status = s.Status.ToString(),
                s.Notes
            })
            .ToListAsync();

        return Ok(new { data = sessions, total, page, pageSize });
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

        return Ok(new
        {
            session.Id,
            session.SessionNumber,
            Status = session.Status.ToString(),
            message = "تمت المطابقة والاعتماد المحاسبي للوردية اليومية بنجاح"
        });
    }
}
