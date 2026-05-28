using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

// ─── DTOs لعمليات الورديات ───────────────────────────────────────────

/// <summary>
/// طلب فتح وردية كاشير جديدة
/// </summary>
public sealed class OpenShiftDto
{
    /// <summary>مبلغ العهدة الافتتاحية في الدرج</summary>
    public decimal OpeningAmount { get; init; }

    /// <summary>ملاحظات اختيارية</summary>
    public string? Notes { get; init; }
}

// ─── ShiftsController — واجهة Finance V3 لإدارة الورديات ─────────────

/// <summary>
/// كنترولر استقبال وتسيير الورديات وفتح الدورة النقدية بالخزينة.
/// يعمل كواجهة Finance V3 (api/finance-v3/shifts) ويستخدم كيان CashierSession الحقيقي.
/// الكنترولر الأصلي CashierSessionsController يظل يعمل على مسار api/cashier-sessions.
/// </summary>
[ApiController]
[Route("api/finance-v3/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<ShiftsController> _logger;

    public ShiftsController(
        AppDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<ShiftsController> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    // ─── فتح وردية جديدة ─────────────────────────────────────────────

    /// <summary>
    /// POST /api/finance-v3/shifts/open
    /// فتح وردية كاشير جديدة مع مبلغ افتتاحي.
    /// يمنع فتح أكثر من وردية واحدة لنفس المستخدم في نفس الوقت.
    /// </summary>
    [HttpPost("open")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> OpenShift([FromBody] OpenShiftDto dto)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;
        var branchId = _currentUser.BranchId;

        // التحقق من وجود فرع صالح
        if (branchId == null || branchId == Guid.Empty)
            return BadRequest(new { Message = "عذراً، يجب تحديد الفرع قبل فتح وردية الكاشير." });

        // التحقق من عدم وجود وردية مفتوحة سابقة لنفس المستخدم
        var activeShift = await _context.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

        if (activeShift != null)
        {
            return BadRequest(new
            {
                Message = "لديك وردية مفتوحة بالفعل برقم " + activeShift.SessionNumber + ". يجب إقفال الوردية الحالية أولاً.",
                ShiftId = activeShift.Id
            });
        }

        // التحقق من صحة المبلغ الافتتاحي
        if (dto.OpeningAmount < 0)
            return BadRequest(new { Message = "لا يمكن أن يكون رصيد العهدة الافتتاحية سالباً." });

        // إنشاء الوردية الجديدة
        var newShift = new CashierSession
        {
            Id = Guid.NewGuid(),
            CashierId = userId,
            BranchId = branchId.Value,
            OpeningTime = DateTime.UtcNow,
            OpeningBalance = dto.OpeningAmount,
            ExpectedClosingCash = dto.OpeningAmount, // يبدأ بالمبلغ الافتتاحي فقط
            ExpectedClosingCard = 0,
            ExpectedClosingBank = 0,
            Status = SessionStatus.Open,
            Notes = dto.Notes?.Trim(),
            SessionNumber = await GenerateSessionNumberAsync()
        };

        _context.CashierSessions.Add(newShift);
        await _context.SaveChangesAsync();

        // تسجيل العملية في سجل المراجعة
        await _audit.LogAsync(AuditAction.Create, "CashierSession", newShift.Id,
            details: $"Shift {newShift.SessionNumber} opened via Finance V3 API with opening balance {dto.OpeningAmount}");

        _logger.LogInformation("Shift {SessionNumber} opened for cashier {UserId} at branch {BranchId}",
            newShift.SessionNumber, userId, branchId.Value);

        return Ok(new
        {
            Message = "تم فتح الوردية بنجاح بنقود افتتاحية قيمتها " + dto.OpeningAmount.ToString("N0") + " ر.ي",
            ShiftId = newShift.Id,
            SessionNumber = newShift.SessionNumber,
            OpeningTime = newShift.OpeningTime,
            OpeningBalance = newShift.OpeningBalance,
            Status = newShift.Status.ToString()
        });
    }

    // ─── جلب الوردية النشطة للمستخدم الحالي ──────────────────────────

    /// <summary>
    /// GET /api/finance-v3/shifts/active
    /// جلب الوردية المفتوحة حالياً للمستخدم الحالي (إن وجدت).
    /// </summary>
    [HttpGet("active")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetActiveShift()
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        var activeShift = await _context.CashierSessions
            .Include(s => s.Cashier)
            .Where(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive)
            .Select(s => new
            {
                s.Id,
                s.SessionNumber,
                s.OpeningTime,
                s.OpeningBalance,
                s.ExpectedClosingCash,
                s.ExpectedClosingCard,
                s.ExpectedClosingBank,
                CashierName = s.Cashier.Username,
                s.BranchId,
                Status = s.Status.ToString()
            })
            .FirstOrDefaultAsync();

        if (activeShift == null)
            return Ok(new { hasActiveShift = false });

        return Ok(new { hasActiveShift = true, shift = activeShift });
    }

    // ─── إقفال الوردية الحالية ────────────────────────────────────────

    /// <summary>
    /// POST /api/finance-v3/shifts/close
    /// إقفال الوردية المفتوحة حالياً مع إدخال المبالغ الفعلية.
    /// </summary>
    [HttpPost("close")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> CloseShift([FromBody] CloseSessionRequest req)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        var session = await _context.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

        if (session == null)
            return BadRequest(new { Message = "لا يوجد وردية مفتوحة حالياً لإقفالها." });

        // حساب المبالغ المتوقعة من المعاملات النقدية المرتبطة بالوردية
        var sessionTransactions = await _context.CashFlowTransactions
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
        session.Status = SessionStatus.Closed;
        session.Notes = req.Notes?.Trim();

        // ربط المعاملات غير المرتبطة بالوردية
        var unlinkedTransactions = await _context.CashFlowTransactions
            .Where(t => t.CashierSessionId == null
                     && t.PerformedBy == userId
                     && t.CreatedAt >= session.OpeningTime
                     && t.IsActive)
            .ToListAsync();

        foreach (var t in unlinkedTransactions)
        {
            t.CashierSessionId = session.Id;
            _logger.LogWarning("Heuristically linking unlinked CashFlowTransaction {TxId} to session {SessionId}",
                t.Id, session.Id);
        }

        await _context.SaveChangesAsync();

        await _audit.LogAsync(AuditAction.Update, "CashierSession", session.Id,
            details: $"Shift closed via Finance V3 API, surplus/shortage: {session.ShortageOrSurplus}");

        return Ok(new
        {
            Message = "تم إقفال الوردية بنجاح وترحيل المبالغ",
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
            Status = session.Status.ToString()
        });
    }

    // ─── أدوات مساعدة ─────────────────────────────────────────────────

    private static bool IsCashMethod(string method) =>
        string.Equals(method, "cash", StringComparison.OrdinalIgnoreCase);

    private static bool IsCardMethod(string method) =>
        string.Equals(method, "card", StringComparison.OrdinalIgnoreCase);

    private static bool IsBankMethod(string method) =>
        string.Equals(method, "bank_transfer", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "bank", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// توليد رقم وردية تسلسلي بصيغة SH-yyyyMMdd-NN
    /// </summary>
    private async Task<string> GenerateSessionNumberAsync()
    {
        var today = DateTime.UtcNow;
        var datePart = today.ToString("yyyyMMdd");
        var prefix = $"SH-{datePart}-";

        var lastSession = await _context.CashierSessions
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

        return $"{prefix}{nextSeq:D2}";
    }
}
