using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Services;
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
public class OperationalExpensesController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit, IJournalEntryService journalEntryService, ITreasuryResolutionService treasuryResolution) : ControllerBase
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
        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل تسجيل مصروف نقدي." });
        }

        // Resolve treasury for the branch by payment method — required for JournalEntry posting
        Treasury treasury;
        try
        {
            treasury = await treasuryResolution.ResolveTreasuryAsync(branchId.Value, req.PaymentMethod, activeSession?.Id);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
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
                var lockKey = StableLockKeyHelper.ExpenseNumber;
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
                // Dual-write: CashFlowTransaction (transitional) + JournalEntry (canonical)
                var cashflow = CreateCashFlowTransaction(expense, nextSeq, datePart, userId, branchId.Value, activeSession?.Id, treasury.Id);
                db.CashFlowTransactions.Add(cashflow);
                expense.IsPostedToLedger = true;
                expense.CashFlowTransactionId = cashflow.Id;

                // JournalEntry: Debit Expense / Credit Treasury
                var je = await journalEntryService.CreateEntryAsync(
                    documentType: FinancialDocumentType.Expense,
                    financialDocumentId: expense.Id,
                    description: $"قيد مصروف تشغيلي: {expense.Title} ({GetCategoryArabic(expense.Category)})",
                    entryDate: expense.ExpenseDate,
                    branchId: branchId.Value,
                    performedBy: userId,
                    cashierSessionId: activeSession?.Id,
                    treasuryId: treasury.Id,
                    lines: new[]
                    {
                        (JournalAccountType.Expense, expense.Id, expense.Amount, 0m, (string?)$"مصروف: {expense.Title}"),
                        (JournalAccountType.Treasury, treasury.Id, 0m, expense.Amount, (string?)$"سداد من: {treasury.Name}")
                    });
                je.IsPosted = true;
                je.PostedAt = DateTime.UtcNow;

                // Blocker 1: Atomically decrement Treasury.Balance for the outflow
                await treasuryResolution.DecrementTreasuryBalanceAsync(branchId.Value, expense.PaymentMethod, expense.Amount, activeSession?.Id);
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
        var userId = currentUser.UserId ?? Guid.Empty;

        // Blocker 2: Cash expense approval MUST require an open cashier session
        //   Pre-check session BEFORE starting the transaction to fail fast.
        CashierSession? activeSession = null;
        // We need the expense to know the payment method; load it first without tracking
        var expenseSnapshot = await db.OperationalExpenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
        if (expenseSnapshot == null)
            return NotFound(new { message = "المصروف غير موجود" });

        if (expenseSnapshot.ApprovalStatus != ApprovalStatus.Pending)
            return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });

        var branchId = expenseSnapshot.BranchId;

        // Guard: reject if branch is invalid
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، الفرع غير محدد لهذا المصروف. تواصل مع الإدارة." });

        if (string.Equals(expenseSnapshot.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل اعتماد المصروف النقدي." });
        }

        Treasury treasury;
        try
        {
            treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, expenseSnapshot.PaymentMethod, activeSession?.Id);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // CONCURRENCY SAFETY: Begin transaction FIRST, then lock the source row and re-check eligibility.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire transaction-scoped pessimistic lock on the expense row
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE",
                    id);
            }

            // Reload the expense inside the lock and re-check eligibility
            var expense = await db.OperationalExpenses.FindAsync(id);
            if (expense == null || !expense.IsActive)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "المصروف غير موجود" });
            }

            if (expense.ApprovalStatus != ApprovalStatus.Pending)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "هذا المصروف لا يحتاج إلى اعتماد أو تمت معالجته مسبقاً" });
            }

            expense.ApprovalStatus = ApprovalStatus.Approved;
            expense.ApprovedById = userId;
            expense.ApprovedAt = DateTime.UtcNow;
            expense.ApprovalNotes = req.Notes?.Trim();

            // Dual-write: CashFlowTransaction (transitional) + JournalEntry (canonical)
            var datePart = expense.ExpenseDate.ToString("yyyyMMdd");
            var seqSuffix = expense.ExpenseNumber.Split('-').LastOrDefault() ?? "001";
            if (!int.TryParse(seqSuffix, out var seq)) seq = 1;

            var cashflow = CreateCashFlowTransaction(expense, seq, datePart, userId, branchId, activeSession?.Id, treasury.Id);
            db.CashFlowTransactions.Add(cashflow);
            expense.IsPostedToLedger = true;
            expense.CashFlowTransactionId = cashflow.Id;

            // JournalEntry: Debit Expense / Credit Treasury
            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.Expense,
                financialDocumentId: expense.Id,
                description: $"قيد مصروف تشغيلي (معتمد): {expense.Title} ({GetCategoryArabic(expense.Category)})",
                entryDate: expense.ExpenseDate,
                branchId: branchId,
                performedBy: userId,
                cashierSessionId: activeSession?.Id,
                treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Expense, expense.Id, expense.Amount, 0m, (string?)$"مصروف معتمد: {expense.Title}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, expense.Amount, (string?)$"سداد من: {treasury.Name}")
                });
            je.IsPosted = true;
            je.PostedAt = DateTime.UtcNow;

            // Blocker 1: Atomically decrement Treasury.Balance for the approved outflow
            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, expense.PaymentMethod, expense.Amount, activeSession?.Id);

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

        // Soft-delete the expense since it's rejected (never posted, so no reversal needed)
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

        // Only admin can delete posted expenses
        if (expense.ApprovalStatus == ApprovalStatus.Approved && expense.IsPostedToLedger)
        {
            if (!currentUser.IsAdmin)
                return Forbid();
        }

        var userId = currentUser.UserId ?? Guid.Empty;

        // If the expense was posted to ledger, we must create reversal entries instead of soft-deleting
        if (expense.IsPostedToLedger)
        {
            return await ReversePostedExpenseAsync(expense, userId);
        }

        // Unposted expense: safe to soft-delete (no financial records to reverse)
        expense.IsActive = false;
        expense.DeletedAt = DateTime.UtcNow;
        expense.DeletedBy = userId;

        // If linked to lab order, restore lab order status back to "received"
        if (expense.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(expense.LabOrderId.Value);
            if (labOrder != null)
                labOrder.Status = "received";
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف قيد المصروف بنجاح" });
    }

    // --------------- Helpers ---------------

    /// <summary>
    /// Reverses a posted expense by creating reversal CashFlowTransaction + JournalEntry.
    /// NEVER soft-deletes posted financial history — all corrections go through reversal entries.
    /// </summary>
    private async Task<IActionResult> ReversePostedExpenseAsync(OperationalExpense expenseSnapshot, Guid userId)
    {
        // CONCURRENCY SAFETY: Begin transaction FIRST, then lock the source row.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire transaction-scoped pessimistic lock on the expense row
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""OperationalExpenses"" WHERE ""Id"" = {0} FOR UPDATE",
                    expenseSnapshot.Id);
            }

            // Reload the expense inside the lock
            var expense = await db.OperationalExpenses.FindAsync(expenseSnapshot.Id);
            if (expense == null || !expense.IsActive)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "المصروف غير موجود" });
            }

            // Guard: do not corrupt a closed or reconciled session
            CashFlowTransaction? originalCashflow = null;
            if (expense.CashFlowTransactionId.HasValue)
            {
                originalCashflow = await db.CashFlowTransactions.FindAsync(expense.CashFlowTransactionId.Value);
            }
            else
            {
                originalCashflow = await db.CashFlowTransactions
                    .FirstOrDefaultAsync(t => t.ReferenceId == expense.Id && t.Category == FinancialCategory.OperationalExpense && t.IsActive);
            }

            if (originalCashflow != null && originalCashflow.CashierSessionId.HasValue)
            {
                var linkedSession = await db.CashierSessions.FindAsync(originalCashflow.CashierSessionId.Value);
                if (linkedSession != null && linkedSession.Status != SessionStatus.Open)
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { message = "لا يمكن حذف مصروف مرتبط بوردية مقفلة أو مطابقة. تواصل مع المحاسب." });
                }
            }

            // Check if already reversed — prevents double-restore (re-checked inside lock)
            if (originalCashflow?.ReversedByTransactionId != null)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "هذا المصروف تم عكسه مسبقاً." });
            }

            // Blocker 1: For posted expense reversal, we MUST restore balance to the exact original treasury.
            if (originalCashflow == null || originalCashflow.TreasuryId == null || originalCashflow.TreasuryId == Guid.Empty)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "عذراً، لا يمكن عكس القيد — سجل التدفق النقدي الأصلي غير مرتبط بخزينة. تواصل مع المحاسب." });
            }

            var originalTreasuryId = originalCashflow.TreasuryId.Value;

            // Verify the original treasury exists and is active before writing any reversal records
            var originalTreasury = await db.Treasuries.FindAsync(originalTreasuryId);
            if (originalTreasury == null || !originalTreasury.IsActive)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "عذراً، الخزينة الأصلية غير موجودة أو غير مفعلة. لا يمكن عكس القيد المالي — تواصل مع المحاسب." });
            }
            // Create reversal CashFlowTransaction (Inflow, Category=Reversal)
            var reversalCashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-OUT-REV-{Guid.NewGuid().ToString()[..8]}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.Reversal,
                Amount = expense.Amount,
                PaymentMethod = expense.PaymentMethod,
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                ReferenceId = expense.Id,
                ReferenceNumber = expense.ExpenseNumber,
                Description = $"عكس قيد مصروف تشغيلي: {expense.Title}",
                PerformedBy = userId,
                BranchId = expense.BranchId,
                IsReversal = true,
                ReversalOfTransactionId = originalCashflow.Id,
                CashierSessionId = originalCashflow.CashierSessionId,
                TreasuryId = originalTreasuryId
            };
            db.CashFlowTransactions.Add(reversalCashflow);

            // Link original to reversal — prevents double-restore on retry
            originalCashflow.ReversedByTransactionId = reversalCashflow.Id;

            // Create reversal JournalEntry via service
            var originalJe = await db.JournalEntries
                .FirstOrDefaultAsync(e => e.FinancialDocumentId == expense.Id
                    && e.FinancialDocumentType == FinancialDocumentType.Expense
                    && !e.IsReversal);

            if (originalJe != null)
            {
                var reversalJe = await journalEntryService.CreateReversalEntryAsync(
                    originalEntryId: originalJe.Id,
                    reason: $"حذف مصروف: {expense.Title}",
                    performedBy: userId);
                reversalJe.IsPosted = true;
                reversalJe.PostedAt = DateTime.UtcNow;
            }

            // Blocker 1: Atomically increment Treasury.Balance on the EXACT original treasury.
            // Uses the original CashFlowTransaction.TreasuryId — never re-resolves by payment method.
            // Double-restore is prevented because originalCashflow.ReversedByTransactionId is set above,
            // and the check at the top of this method rejects if it's already non-null.
            await treasuryResolution.IncrementTreasuryBalanceByTreasuryIdAsync(originalTreasuryId, expense.Amount);

            // Mark the expense as deactivated (but DO NOT soft-delete posted financial history)
            expense.IsActive = false;
            expense.DeletedAt = DateTime.UtcNow;
            expense.DeletedBy = userId;

            // If linked to lab order, restore lab order status back to "received"
            if (expense.LabOrderId.HasValue)
            {
                var labOrder = await db.LabOrders.FindAsync(expense.LabOrderId.Value);
                if (labOrder != null)
                    labOrder.Status = "received";
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Update, "OperationalExpense", expense.Id, details: "Posted expense reversed via cashflow + journal entry reversal");

            return Ok(new { message = "تم عكس قيد المصروف وترحيل القيد العكسي بنجاح" });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static CashFlowTransaction CreateCashFlowTransaction(
        OperationalExpense expense, int seq, string datePart, Guid userId, Guid branchId,
        Guid? cashierSessionId, Guid treasuryId)
    {
        return new CashFlowTransaction
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
            CashierSessionId = cashierSessionId,
            TreasuryId = treasuryId
        };
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
