using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateExpenseRequest
{
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty; // Rent, Utilities, etc.
    public decimal Amount { get; init; }
    public string? ExpenseDate { get; init; } // yyyy-MM-dd
    public string PaymentMethod { get; init; } = "cash"; // cash, card, bank_transfer
    public Guid? SupplierId { get; init; }
    public Guid? LabOrderId { get; init; }
    public string? Notes { get; init; }
    public string? ReceiptAttachmentUrl { get; init; }
}

public sealed class ApproveExpenseRequest
{
    public string? Notes { get; init; }
}

public sealed class RejectExpenseRequest
{
    public string Reason { get; init; } = string.Empty;
}

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = "ReportsAccess")] // Admin + Accountant only
public class OperationalExpensesController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit) : ControllerBase
{
    /// <summary>
    /// Approval threshold in YER: expenses above this amount require managerial approval.
    /// Can be made configurable via Settings table in future.
    /// </summary>
    private const decimal ApprovalThreshold = 50_000m;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title))
            return BadRequest(new { message = "عنوان المصروف مطلوب" });

        if (!Enum.TryParse<ExpenseCategory>(req.Category, true, out var category))
            return BadRequest(new { message = "صنف المصروف غير صالح" });

        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ المصروف أكبر من الصفر" });

        var date = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(req.ExpenseDate) && DateOnly.TryParse(req.ExpenseDate, out var parsedDate))
            date = parsedDate;

        var userId = currentUser.UserId ?? Guid.Empty;

        // BranchId guard: must have a valid branch assignment before registering an expense
        var branchId = currentUser.BranchId;
        if (branchId == null || branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل تسجيل المصروف." });

        // Cash expenses require an open cashier session so they are linked to the drawer.
        // Non-cash expenses (card, bank_transfer) do NOT require an open session — only
        // physical cash leaves the drawer and must be tracked against the open shift.
        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل مصروف نقدي." });
        }

        // Verify supplier if provided
        if (req.SupplierId.HasValue)
        {
            var supplierExists = await db.Suppliers.AnyAsync(s => s.Id == req.SupplierId.Value && s.IsActive);
            if (!supplierExists)
                return BadRequest(new { message = "المورد المحدد غير موجود" });
        }

        // Verify lab order if provided
        if (req.LabOrderId.HasValue)
        {
            var labOrderExists = await db.LabOrders.AnyAsync(l => l.Id == req.LabOrderId.Value && l.IsActive);
            if (!labOrderExists)
                return BadRequest(new { message = "أمر المختبر المحدد غير موجود" });
        }

        // Determine approval status based on amount threshold
        var needsApproval = req.Amount > ApprovalThreshold;
        var approvalStatus = needsApproval ? ApprovalStatus.Pending : ApprovalStatus.NotRequired;

        // Generate sequential EXP number using advisory lock (relational only)
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"EXP-{datePart}-";

            if (db.Database.IsRelational())
            {
                var lockKey = Math.Abs("ExpenseNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var lastExpense = await db.OperationalExpenses
                .IgnoreQueryFilters()
                .Where(e => e.ExpenseNumber.StartsWith(prefix))
                .OrderByDescending(e => e.ExpenseNumber)
                .Select(e => e.ExpenseNumber)
                .FirstOrDefaultAsync();

            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastExpense) && lastExpense.Length > prefix.Length)
            {
                var seqPart = lastExpense[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq))
                    nextSeq = lastSeq + 1;
            }

            var expenseNumber = $"{prefix}{nextSeq:D3}";

            var expense = new OperationalExpense
            {
                ExpenseNumber = expenseNumber,
                Title = req.Title.Trim(),
                Category = category,
                Amount = req.Amount,
                ExpenseDate = date,
                PaymentMethod = req.PaymentMethod,
                SupplierId = req.SupplierId,
                LabOrderId = req.LabOrderId,
                Notes = req.Notes?.Trim(),
                ReceiptAttachmentUrl = req.ReceiptAttachmentUrl,
                PaidBy = userId,
                BranchId = branchId.Value,
                ApprovalStatus = approvalStatus,
                IsPostedToLedger = false
            };

            db.OperationalExpenses.Add(expense);

            // Auto-post to ledger ONLY if no approval is needed
            if (!needsApproval)
            {
                // Phase 0A: link cashflow to the active cashier session for cash expenses
                // so that the drawer reconciliation correctly subtracts cash outflows.
                var cashflow = await PostToLedgerAsync(db, expense, nextSeq, datePart, userId, branchId.Value, activeSession?.Id);
                expense.IsPostedToLedger = true;
                expense.CashFlowTransactionId = cashflow.Id;
            }

            // If linked to lab order, update lab order status to "paid"
            if (req.LabOrderId.HasValue)
            {
                var labOrder = await db.LabOrders.FindAsync(req.LabOrderId.Value);
                if (labOrder != null)
                    labOrder.Status = "paid";
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for expense creation
            await audit.LogAsync(AuditAction.Create, "OperationalExpense", expense.Id);

            return Created($"/api/expenses/{expense.Id}", new
            {
                expense.Id,
                expense.ExpenseNumber,
                expense.Title,
                Category = expense.Category.ToString(),
                CategoryArabic = GetCategoryArabic(expense.Category),
                expense.Amount,
                ExpenseDate = expense.ExpenseDate.ToString("yyyy-MM-dd"),
                expense.PaymentMethod,
                expense.Notes,
                ApprovalStatus = expense.ApprovalStatus.ToString(),
                expense.IsPostedToLedger,
                message = needsApproval
                    ? $"تم تسجيل المصروف بنجاح. المبلغ ({req.Amount:N0} ريال) يتجاوز حد الاعتماد — في انتظار موافقة الإدارة قبل الترحيل."
                    : "تم تسجيل المصروف والترحيل المالي بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] string? approvalStatus = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.OperationalExpenses
            .Include(e => e.Supplier)
            .Include(e => e.LabOrder)
            .Include(e => e.ApprovedBy)
            .Where(e => e.IsActive)
            .AsQueryable();

        // Branch boundary: Non-admin users are restricted to their own branch
        if (currentUser.BranchId.HasValue && !currentUser.IsAdmin)
        {
            query = query.Where(e => e.BranchId == currentUser.BranchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ExpenseCategory>(category, true, out var catFilter))
        {
            query = query.Where(e => e.Category == catFilter);
        }

        if (!string.IsNullOrWhiteSpace(approvalStatus) && Enum.TryParse<ApprovalStatus>(approvalStatus, true, out var statusFilter))
        {
            query = query.Where(e => e.ApprovalStatus == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var from))
        {
            query = query.Where(e => e.ExpenseDate >= from);
        }

        if (!string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var to))
        {
            query = query.Where(e => e.ExpenseDate <= to);
        }

        var total = await query.CountAsync();
        var pendingCount = await db.OperationalExpenses
            .Where(e => e.IsActive && e.ApprovalStatus == ApprovalStatus.Pending)
            .CountAsync();

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.ExpenseNumber,
                e.Title,
                Category = e.Category.ToString(),
                CategoryArabic = GetCategoryArabic(e.Category),
                e.Amount,
                ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                e.PaymentMethod,
                SupplierName = e.Supplier != null ? e.Supplier.Name : null,
                LabOrderNumber = e.LabOrder != null ? e.LabOrder.OrderNumber : null,
                e.Notes,
                e.ReceiptAttachmentUrl,
                ApprovalStatus = e.ApprovalStatus.ToString(),
                e.IsPostedToLedger,
                e.ApprovalNotes,
                ApprovedByName = e.ApprovedBy != null ? e.ApprovedBy.Username : null,
                ApprovedAt = e.ApprovedAt.HasValue ? e.ApprovedAt.Value.ToString("yyyy-MM-dd HH:mm") : null,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(new { data = expenses, total, page, pageSize, pendingCount });
    }

    /// <summary>GET /api/expenses/pending — Returns all expenses awaiting approval.</summary>
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var expenses = await db.OperationalExpenses
            .Include(e => e.Supplier)
            .Where(e => e.IsActive && e.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id,
                e.ExpenseNumber,
                e.Title,
                Category = e.Category.ToString(),
                CategoryArabic = GetCategoryArabic(e.Category),
                e.Amount,
                ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                e.PaymentMethod,
                SupplierName = e.Supplier != null ? e.Supplier.Name : null,
                e.Notes,
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(expenses);
    }

    /// <summary>POST /api/expenses/{id}/approve — Approve a pending expense and post to GL.</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveExpenseRequest req)
    {
        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive)
            return NotFound(new { message = "المصروف غير موجود" });

        if (expense.ApprovalStatus != ApprovalStatus.Pending)
            return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = expense.BranchId;

        // Phase 0A: When approving a cash expense, find the active session to link
        CashierSession? activeSession = null;
        if (string.Equals(expense.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            // Note: We do NOT block approval if no session is open — the expense was already
            // created. The approved expense will still post to the ledger, but without a
            // session link it won't affect the current drawer reconciliation. This is safe
            // because approved expenses may be processed after session closing.
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            expense.ApprovalStatus = ApprovalStatus.Approved;
            expense.ApprovedById = userId;
            expense.ApprovedAt = DateTime.UtcNow;
            expense.ApprovalNotes = req.Notes?.Trim();

            // Post to GL now that it's approved — Phase 0A: link to session if cash
            var datePart = expense.ExpenseDate.ToString("yyyyMMdd");
            var seqSuffix = expense.ExpenseNumber.Split('-').LastOrDefault() ?? "001";
            if (!int.TryParse(seqSuffix, out var seq)) seq = 1;

            var cashflow = await PostToLedgerAsync(db, expense, seq, datePart, userId, branchId, activeSession?.Id);
            expense.IsPostedToLedger = true;
            expense.CashFlowTransactionId = cashflow.Id;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for expense approval
            await audit.LogAsync(AuditAction.Approve, "OperationalExpense", id);

            return Ok(new
            {
                message = "تم اعتماد المصروف وترحيله للأستاذ العام بنجاح",
                expense.Id,
                expense.ExpenseNumber,
                ApprovalStatus = expense.ApprovalStatus.ToString()
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>POST /api/expenses/{id}/reject — Reject a pending expense (does NOT post to GL).</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectExpenseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Reason))
            return BadRequest(new { message = "سبب الرفض مطلوب" });

        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive)
            return NotFound(new { message = "المصروف غير موجود" });

        if (expense.ApprovalStatus != ApprovalStatus.Pending)
            return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });

        var userId = currentUser.UserId ?? Guid.Empty;

        expense.ApprovalStatus = ApprovalStatus.Rejected;
        expense.ApprovedById = userId;
        expense.ApprovedAt = DateTime.UtcNow;
        expense.ApprovalNotes = req.Reason.Trim();

        // Soft-delete the expense since it's rejected
        expense.IsActive = false;
        expense.DeletedAt = DateTime.UtcNow;
        expense.DeletedBy = userId;

        await db.SaveChangesAsync();

        // H3: Audit logging for expense rejection
        await audit.LogAsync(AuditAction.Update, "OperationalExpense", id, details: "Expense rejected");

        return Ok(new
        {
            message = "تم رفض المصروف وإلغاؤه بنجاح",
            expense.Id,
            expense.ExpenseNumber,
            ApprovalStatus = expense.ApprovalStatus.ToString()
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive)
            return NotFound(new { message = "المصروف غير موجود" });

        if (expense.ApprovalStatus == ApprovalStatus.Approved && expense.IsPostedToLedger)
        {
            // Only admin can delete posted expenses
            if (!currentUser.IsAdmin)
                return Forbid();
        }

        var userId = currentUser.UserId;

        expense.IsActive = false;
        expense.DeletedAt = DateTime.UtcNow;
        expense.DeletedBy = userId;

        // Deactivate the linked cashflow ledger outflow transaction (if posted)
        if (expense.CashFlowTransactionId.HasValue)
        {
            var cashflow = await db.CashFlowTransactions.FindAsync(expense.CashFlowTransactionId.Value);
            if (cashflow != null)
            {
                // Phase 0A: Guard — do not corrupt a closed or reconciled session by removing
                // a transaction that was part of its reconciliation calculation.
                if (cashflow.CashierSessionId.HasValue)
                {
                    var linkedSession = await db.CashierSessions.FindAsync(cashflow.CashierSessionId.Value);
                    if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
                    {
                        return BadRequest(new { message = "لا يمكن حذف مصروف مرتبط بوردية مقفلة أو مطابقة. تواصل مع المحاسب." });
                    }
                }
                cashflow.IsActive = false;
                cashflow.DeletedAt = DateTime.UtcNow;
                cashflow.DeletedBy = userId;
            }
        }
        else
        {
            // Fallback: search by reference for legacy expenses not linked via CashFlowTransactionId
            var cashflow = await db.CashFlowTransactions
                .FirstOrDefaultAsync(t => t.ReferenceId == expense.Id && t.Category == FinancialCategory.OperationalExpense && t.IsActive);
            if (cashflow != null)
            {
                // Phase 0A: Guard — same closed session protection
                if (cashflow.CashierSessionId.HasValue)
                {
                    var linkedSession = await db.CashierSessions.FindAsync(cashflow.CashierSessionId.Value);
                    if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
                    {
                        return BadRequest(new { message = "لا يمكن حذف مصروف مرتبط بوردية مقفلة أو مطابقة. تواصل مع المحاسب." });
                    }
                }
                cashflow.IsActive = false;
                cashflow.DeletedAt = DateTime.UtcNow;
                cashflow.DeletedBy = userId;
            }
        }

        // If linked to lab order, restore lab order status back to "received"
        if (expense.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(expense.LabOrderId.Value);
            if (labOrder != null)
                labOrder.Status = "received";
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف قيد المصروف وإلغاء الترحيل المالي بنجاح" });
    }

    // --------------- Helpers ---------------

    private static async Task<CashFlowTransaction> PostToLedgerAsync(
        AppDbContext db, OperationalExpense expense, int seq, string datePart, Guid userId, Guid branchId,
        Guid? cashierSessionId = null)
    {
        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{datePart}-OUT-{seq:D3}",
            Type = TransactionType.Outflow,
            Category = FinancialCategory.OperationalExpense,
            Amount = expense.Amount,
            PaymentMethod = expense.PaymentMethod,
            TransactionDate = expense.ExpenseDate,
            ReferenceId = expense.Id,
            ReferenceNumber = expense.ExpenseNumber,
            Description = $"قيد مصروف تشغيلي: {expense.Title} ({GetCategoryArabic(expense.Category)})",
            PerformedBy = userId,
            BranchId = branchId,
            // Phase 0A: Link to cashier session for cash expenses so drawer reconciliation
            // correctly subtracts cash outflows from expected closing amounts.
            CashierSessionId = cashierSessionId
        };
        db.CashFlowTransactions.Add(cashflow);
        return cashflow;
    }

    private static string GetCategoryArabic(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Rent => "إيجارات وفروع",
        ExpenseCategory.Utilities => "خدمات ومنافع (كهرباء/مياه/إنترنت)",
        ExpenseCategory.LabFees => "تكاليف مختبرات الأسنان",
        ExpenseCategory.Marketing => "إعلانات وتسويق",
        ExpenseCategory.ClinicSupplies => "مواد ومستلزمات عيادات",
        ExpenseCategory.Maintenance => "صيانة أدوات ومعدات",
        ExpenseCategory.Salaries => "رواتب موظفين",
        ExpenseCategory.Commissions => "عمولات أطباء",
        ExpenseCategory.Taxes => "ضرائب ورسوم حكومية",
        ExpenseCategory.Miscellaneous => "نثريات ومصاريف متنوعة",
        _ => category.ToString()
    };
}
