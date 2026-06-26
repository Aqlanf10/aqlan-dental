using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateAdvancePaymentRequest
{
    public Guid EmployeeId { get; init; }
    public decimal Amount { get; init; }
    public string? Reason { get; init; }
    public int? DeductFromMonth { get; init; }
    public int? DeductFromYear { get; init; }
}

public sealed class ApproveAdvanceRequest
{
    public bool Approve { get; init; }
    public string? RejectionReason { get; init; }
}

[ApiController]
[Route("api/advances")]
[Authorize(Policy = "ReportsAccess")]
public class AdvancePaymentController(AppDbContext db, IAuditService audit, IJournalEntryService journalEntryService, ITreasuryResolutionService treasuryResolution, ICurrentUserService currentUser, ILogger<AdvancePaymentController> logger) : ControllerBase
{
    // FIN-PERM (Group B): the class-level ReportsAccess policy is the coarse gate;
    // the granular finance.expenses permission (employee advances are outflows) is the
    // real per-action gate. Most actions are also AdminOnly; Admin bypasses CanAsync.
    private Task<bool> CanAsync(string action) =>
        PermissionGuard.HasAsync(db, currentUser, "finance.expenses", action);

    private IActionResult Deny() =>
        StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء المالي" });

    /// <summary>
    /// Get advance payment records with filters
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? employeeId,
        [FromQuery] RequestStatus? status,
        [FromQuery] Guid? branchId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await CanAsync("view")) return Deny();
        try
        {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.AdvancePayments
            .Include(a => a.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
            query = query.Where(a => a.EmployeeId == employeeId.Value);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (branchId.HasValue)
            query = query.Where(a => a.Employee.BranchId == branchId.Value);

        var total = await query.CountAsync();

        var records = await query
            .OrderByDescending(a => a.RequestDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.EmployeeId,
                EmployeeName = a.Employee.FullName,
                a.Amount,
                a.Reason,
                a.RequestDate,
                Status = a.Status.ToString(),
                a.ApprovedAt,
                a.RejectionReason,
                a.DeductFromMonth,
                a.DeductFromYear,
                a.IsDeducted,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { data = records, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetAll advances failed");
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل البيانات" });
        }
    }

    /// <summary>
    /// Create advance payment request
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateAdvancePaymentRequest req)
    {
        if (!await CanAsync("create")) return Deny();
        if (req.Amount <= 0)
            return BadRequest(new { message = "مبلغ السلفة يجب أن يكون أكبر من صفر" });

        var employee = await db.Employees.FindAsync(req.EmployeeId);
        if (employee is null)
            return BadRequest(new { message = "الموظف غير موجود" });

        var advance = new AdvancePayment
        {
            EmployeeId = req.EmployeeId,
            Amount = req.Amount,
            Reason = req.Reason?.Trim(),
            RequestDate = DateTime.UtcNow,
            Status = RequestStatus.Pending,
            DeductFromMonth = req.DeductFromMonth,
            DeductFromYear = req.DeductFromYear,
        };

        db.AdvancePayments.Add(advance);
        await db.SaveChangesAsync();

        // H3: Audit logging for advance payment creation
        await audit.LogAsync(AuditAction.Create, "AdvancePayment", advance.Id);

        return Created($"/api/advances/{advance.Id}", new
        {
            advance.Id,
            advance.EmployeeId,
            EmployeeName = employee.FullName,
            advance.Amount,
            advance.Reason,
            advance.RequestDate,
            Status = advance.Status.ToString(),
            advance.DeductFromMonth,
            advance.DeductFromYear,
        });
    }

    /// <summary>
    /// Approve or reject advance payment
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveAdvanceRequest req)
    {
        if (!await CanAsync("approve")) return Deny();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userIdGuid = Guid.TryParse(userId, out var uidParsed) ? uidParsed : Guid.Empty;

        // Rejection path â€” state-only, no transaction needed
        if (!req.Approve)
        {
            var advance = await db.AdvancePayments.FindAsync(id);
            if (advance is null)
                return NotFound(new { message = "طلب السلفة غير موجود" });
            if (advance.Status != RequestStatus.Pending)
                return BadRequest(new { message = "تم معالجة هذا الطلب مسبقاً" });

            advance.Status = RequestStatus.Rejected;
            advance.RejectionReason = req.RejectionReason?.Trim();
            advance.ApprovedBy = userIdGuid == Guid.Empty ? null : userIdGuid;
            advance.ApprovedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await audit.LogAsync(AuditAction.Update, "AdvancePayment", id, details: "Advance rejected");
            return Ok(new { message = "تم رفض السلفة", Status = advance.Status.ToString() });
        }

        // â”€â”€ Approval path â€” fully atomic â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Pessimistic lock for concurrency safety
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""AdvancePayments"" WHERE ""Id"" = {0} FOR UPDATE", id);
            }

            var advance = await db.AdvancePayments.FindAsync(id);
            if (advance is null)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "طلب السلفة غير موجود" });
            }

            // Re-check Pending status INSIDE the transaction (concurrency guard)
            if (advance.Status != RequestStatus.Pending)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "تم معالجة هذا الطلب مسبقاً" });
            }

            var employee = await db.Employees.FindAsync(advance.EmployeeId);
            if (employee is null)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "الموظف غير موجود" });
            }

            var branchId = employee.BranchId ?? Guid.Empty;
            if (branchId == Guid.Empty)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "عذراً، لا يمكن الموافقة على السلفة — الفرع غير محدد للموظف. تواصل مع الإدارة." });
            }

            // Resolve payment method and cashier session
            var paymentMethod = "cash"; // Advances are typically cash
            CashierSession? activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userIdGuid && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل الموافقة على السلف النقدية." });
            }

            // Resolve treasury via TreasuryResolutionService
            Treasury treasury;
            try
            {
                treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, paymentMethod, null, activeSession.Id);
            }
            catch (ArgumentException ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }

            // 1. Update advance status
            advance.Status = RequestStatus.Approved;
            advance.ApprovedBy = userIdGuid == Guid.Empty ? null : userIdGuid;
            advance.ApprovedAt = DateTime.UtcNow;

            // 2. Create CashFlowTransaction (Outflow) for the advance payment
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-ADV-{advance.Id.ToString()[..8]}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.SalaryAdvance,
                Amount = advance.Amount,
                PaymentMethod = paymentMethod,
                TransactionDate = ClinicTimeProvider.ClinicToday(),
                ReferenceId = advance.Id,
                ReferenceNumber = $"ADV-{advance.Id.ToString()[..8]}",
                Description = $"سلفة موظف: {employee.FullName}",
                PerformedBy = userIdGuid,
                BranchId = branchId,
                TreasuryId = treasury.Id,
                CashierSessionId = activeSession.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            // 3. Create JournalEntry: Debit OtherReceivable, Credit Treasury
            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.AdvancePayment,
                financialDocumentId: advance.Id,
                description: $"سلفة موظف: {employee.FullName}",
                entryDate: ClinicTimeProvider.ClinicToday(),
                branchId: branchId,
                performedBy: userIdGuid,
                cashierSessionId: activeSession.Id,
                treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.OtherReceivable, advance.Id, advance.Amount, 0m, (string?)$"سلفة: {employee.FullName}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, advance.Amount, (string?)$"سداد من: {treasury.Name}")
                }, autoSave: false);
            je.IsPosted = true;
            je.PostedAt = DateTime.UtcNow;

            // 4. Atomically decrement Treasury.Balance
            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, paymentMethod, advance.Amount, null, activeSession.Id);

            // 5. Save all changes atomically
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Approve, "AdvancePayment", id);

            return Ok(new
            {
                message = "تم الموافقة على السلفة",
                Status = advance.Status.ToString(),
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Delete advance payment
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!await CanAsync("delete")) return Deny();
        var advance = await db.AdvancePayments.FindAsync(id);
        if (advance is null)
            return NotFound(new { message = "طلب السلفة غير موجود" });

        // Blocker 4: Block deletion of approved advances
        if (advance.Status == RequestStatus.Approved)
            return BadRequest(new { message = "لا يمكن حذف سلفة تمت الموافقة عليها. استخدم عكس السلفة بدلاً من ذلك." });

        advance.IsActive = false;
        advance.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف طلب السلفة بنجاح" });
    }

    /// <summary>
    /// POST /api/advances/{id}/reverse â€” Reverse an approved advance with dual-write CashFlow + JournalEntry reversal.
    /// Admin-only. Pessimistic locking, double-restore prevention, session guard.
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ReverseAdvance(Guid id, [FromBody] ReverseAdvanceRequest req)
    {
        if (!await CanAsync("approve")) return Deny();
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var performedBy = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty;

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Pessimistic lock
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""AdvancePayments"" WHERE ""Id"" = {0} FOR UPDATE", id);
            }

            var advance = await db.AdvancePayments.FindAsync(id);
            if (advance is null)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "طلب السلفة غير موجود" });
            }

            if (advance.Status != RequestStatus.Approved)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لا يمكن عكس سلفة لم يتم الموافقة عليها" });
            }

            // Double-restore prevention
            var existingReversal = await db.CashFlowTransactions
                .AnyAsync(c => c.ReversalOfTransactionId != null
                    && c.Category == FinancialCategory.Reversal
                    && c.IsReversal
                    && c.ReferenceId == advance.Id);
            if (existingReversal)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "تم عكس هذه السلفة مسبقاً" });
            }

            // Blocker 4: REQUIRE original CashFlow with TreasuryId
            var originalCashflow = await db.CashFlowTransactions
                .FirstOrDefaultAsync(c => c.ReferenceId == advance.Id && c.Category == FinancialCategory.SalaryAdvance && !c.IsReversal);
            if (originalCashflow == null || originalCashflow.TreasuryId == null || originalCashflow.TreasuryId == Guid.Empty)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لا يمكن عكس السلفة — سجلات المحاسبة الأصلية غير مكتملة. تواصل مع المحاسب." });
            }

            // Blocker 4: REQUIRE original JournalEntry exists and IsPosted
            var originalJe = await db.JournalEntries
                .FirstOrDefaultAsync(j => j.FinancialDocumentId == advance.Id && !j.IsReversal);
            if (originalJe == null || !originalJe.IsPosted)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لا يمكن عكس السلفة — سجلات المحاسبة الأصلية غير مكتملة. تواصل مع المحاسب." });
            }

            // Session guard
            if (originalCashflow.CashierSessionId != null)
            {
                var session = await db.CashierSessions.FindAsync(originalCashflow.CashierSessionId);
                if (session != null && (session.Status == SessionStatus.Closed || session.Status == SessionStatus.Reconciled))
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { message = "لا يمكن عكس سلفة مرتبطة بوردية مقفلة أو مسواة." });
                }
            }

            // Mark advance as rejected with reversal reason
            advance.Status = RequestStatus.Rejected;
            advance.RejectionReason = string.IsNullOrWhiteSpace(req.Reason) ? "عكس السلفة" : req.Reason;
            advance.IsDeducted = false;

            var employee = await db.Employees.FindAsync(advance.EmployeeId);
            // Blocker 6: Use original CashFlow's BranchId, NOT current employee branch
            var branchId = originalCashflow.BranchId;
            if (branchId == Guid.Empty)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لا يمكن عكس السلفة — الفرع الأصلي غير محدد في السجل المالي. تواصل مع المحاسب." });
            }
            var treasuryId = originalCashflow.TreasuryId;

            // Dual-write: CashFlow reversal
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var nextSeq = await db.CashFlowTransactions.CountAsync(t => t.Category == FinancialCategory.Reversal) + 1;
            var reversalCashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-REV-ADV-{nextSeq:D3}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.Reversal,
                Amount = advance.Amount,
                PaymentMethod = originalCashflow.PaymentMethod ?? "cash",
                TransactionDate = ClinicTimeProvider.ClinicToday(),
                ReferenceId = advance.Id,
                ReferenceNumber = $"REV-ADV-{advance.Id.ToString()[..8]}",
                Description = $"عكس سلفة موظف: {employee?.FullName ?? "غير معروف"}",
                PerformedBy = performedBy,
                BranchId = branchId,
                TreasuryId = treasuryId,
                IsReversal = true,
                ReversalOfTransactionId = originalCashflow.Id,
                CashierSessionId = originalCashflow.CashierSessionId
            };
            db.CashFlowTransactions.Add(reversalCashflow);

            // Dual-write: JournalEntry reversal
            var reversalJe = await journalEntryService.CreateReversalEntryAsync(originalJe.Id, $"عكس سلفة: {employee?.FullName}", performedBy);
            reversalJe.IsPosted = true;
            reversalJe.PostedAt = DateTime.UtcNow;

            // Restore Treasury balance
            await treasuryResolution.IncrementTreasuryBalanceByTreasuryIdAsync(treasuryId.Value, advance.Amount);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Update, "AdvancePayment", advance.Id, "Advance payment reversed");

            return Ok(new { message = "تم عكس السلفة بنجاح واستعادة الرصيد للخزينة" });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}

public sealed class ReverseAdvanceRequest
{
    public string? Reason { get; init; }
}
