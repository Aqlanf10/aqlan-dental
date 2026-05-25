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

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = "ReportsAccess")] // Admin + Accountant only
public class OperationalExpensesController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
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
        var branchId = currentUser.BranchId ?? Guid.Empty;

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

        // Generate sequential EXP number using advisory lock
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = Math.Abs("ExpenseNumber".GetHashCode()) % 100000;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"EXP-{datePart}-";

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
                BranchId = branchId
            };

            db.OperationalExpenses.Add(expense);

            // Auto-create central ledger cashflow transaction (Outflow)
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-OUT-{nextSeq:D3}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.OperationalExpense,
                Amount = req.Amount,
                PaymentMethod = req.PaymentMethod,
                TransactionDate = date,
                ReferenceId = expense.Id,
                ReferenceNumber = expenseNumber,
                Description = $"قيد مصروف تشغيلي: {expense.Title} ({GetCategoryArabic(category)})",
                PerformedBy = userId,
                BranchId = branchId
            };

            db.CashFlowTransactions.Add(cashflow);

            // If linked to lab order, update lab order status to "paid" or custom notes
            if (req.LabOrderId.HasValue)
            {
                var labOrder = await db.LabOrders.FindAsync(req.LabOrderId.Value);
                if (labOrder != null)
                {
                    labOrder.Status = "paid";
                }
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

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
                message = "تم تسجيل المصروف والترحيل المالي بنجاح"
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
        [FromQuery] string? toDate = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.OperationalExpenses
            .Include(e => e.Supplier)
            .Include(e => e.LabOrder)
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

        if (!string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var from))
        {
            query = query.Where(e => e.ExpenseDate >= from);
        }

        if (!string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var to))
        {
            query = query.Where(e => e.ExpenseDate <= to);
        }

        var total = await query.CountAsync();

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
                e.CreatedAt
            })
            .ToListAsync();

        return Ok(new { data = expenses, total, page, pageSize });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var expense = await db.OperationalExpenses.FindAsync(id);
        if (expense == null || !expense.IsActive)
            return NotFound(new { message = "المصروف غير موجود" });

        var userId = currentUser.UserId;

        expense.IsActive = false;
        expense.DeletedAt = DateTime.UtcNow;
        expense.DeletedBy = userId;

        // Deactive the linked cashflow ledger outflow transaction
        var cashflow = await db.CashFlowTransactions
            .FirstOrDefaultAsync(t => t.ReferenceId == expense.Id && t.Category == FinancialCategory.OperationalExpense && t.IsActive);
        if (cashflow != null)
        {
            cashflow.IsActive = false;
            cashflow.DeletedAt = DateTime.UtcNow;
            cashflow.DeletedBy = userId;
        }

        // If linked to lab order, restore lab order status back to "received"
        if (expense.LabOrderId.HasValue)
        {
            var labOrder = await db.LabOrders.FindAsync(expense.LabOrderId.Value);
            if (labOrder != null)
            {
                labOrder.Status = "received";
            }
        }

        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف قيد المصروف وإلغاء الترحيل المالي بنجاح" });
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
