using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateSupplierBillRequest
{
    public Guid SupplierId { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public string? BillDate { get; init; }      // yyyy-MM-dd
    public string? DueDate { get; init; }        // yyyy-MM-dd (optional credit term)
    public Guid? PurchaseOrderId { get; init; }
    public Guid? LabOrderId { get; init; }
    public string? AttachmentUrl { get; init; }
    public string? Notes { get; init; }
}

public sealed class PayBillInstallmentRequest
{
    public decimal Amount { get; init; }
    public string PaymentMethod { get; init; } = "cash";
    public string? PaymentDate { get; init; }    // yyyy-MM-dd
    public string? ReferenceNumber { get; init; }
    public string? Notes { get; init; }
}

[ApiController]
[Route("api/supplier-bills")]
[Authorize(Policy = "ReportsAccess")] // Admin + Accountant only
public class SupplierBillsController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit, IJournalEntryService journalEntryService, ITreasuryResolutionService treasuryResolution) : ControllerBase
{
    /// <summary>POST /api/supplier-bills — Register a new supplier bill (A/P entry).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierBillRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(new { message = "وصف الفاتورة مطلوب" });

        if (req.TotalAmount <= 0)
            return BadRequest(new { message = "يجب أن يكون إجمالي الفاتورة أكبر من الصفر" });

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == req.SupplierId && s.IsActive);
        if (supplier == null)
            return BadRequest(new { message = "المورد المحدد غير موجود" });

        var billDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(req.BillDate) && DateOnly.TryParse(req.BillDate, out var parsedBill))
            billDate = parsedBill;

        DateOnly? dueDate = null;
        if (!string.IsNullOrWhiteSpace(req.DueDate) && DateOnly.TryParse(req.DueDate, out var parsedDue))
            dueDate = parsedDue;

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = currentUser.BranchId ?? Guid.Empty;

        if (branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل تسجيل فاتورة المورد." });

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Generate BILL number (e.g., BILL-20260525-001)
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"BILL-{datePart}-";

            if (db.Database.IsRelational())
            {
                var lockKey = StableLockKeyHelper.BillNumber;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);
            }

            var lastBill = await db.SupplierBills
                .IgnoreQueryFilters()
                .Where(b => b.BillNumber.StartsWith(prefix))
                .OrderByDescending(b => b.BillNumber)
                .Select(b => b.BillNumber)
                .FirstOrDefaultAsync();

            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastBill) && lastBill.Length > prefix.Length)
            {
                var seqPart = lastBill[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1;
            }

            var billNumber = $"{prefix}{nextSeq:D3}";

            var bill = new SupplierBill
            {
                BillNumber = billNumber,
                SupplierId = req.SupplierId,
                Description = req.Description.Trim(),
                TotalAmount = req.TotalAmount,
                PaidAmount = 0,
                Status = BillStatus.Unpaid,
                BillDate = billDate,
                DueDate = dueDate,
                PurchaseOrderId = req.PurchaseOrderId,
                LabOrderId = req.LabOrderId,
                AttachmentUrl = req.AttachmentUrl,
                Notes = req.Notes?.Trim(),
                BranchId = branchId,
                CreatedBy = userId
            };

            db.SupplierBills.Add(bill);

            // Finance Phase 1: Update supplier balance (increase what we owe)
            supplier.Balance += req.TotalAmount;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for supplier bill creation
            await audit.LogAsync(AuditAction.Create, "SupplierBill", bill.Id);

            return Created($"/api/supplier-bills/{bill.Id}", new
            {
                bill.Id,
                bill.BillNumber,
                SupplierName = supplier.Name,
                bill.Description,
                bill.TotalAmount,
                bill.PaidAmount,
                RemainingAmount = bill.TotalAmount,
                Status = bill.Status.ToString(),
                BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
                DueDate = bill.DueDate?.ToString("yyyy-MM-dd"),
                message = "تم تسجيل فاتورة المورد بنجاح"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>GET /api/supplier-bills — List all supplier bills with filtering.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] bool? overdue = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.SupplierBills
            .Include(b => b.Supplier)
            .Include(b => b.Payments)
            .Where(b => b.IsActive)
            .AsQueryable();

        if (currentUser.BranchId.HasValue && !currentUser.IsAdmin)
            query = query.Where(b => b.BranchId == currentUser.BranchId.Value);

        if (supplierId.HasValue)
            query = query.Where(b => b.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BillStatus>(status, true, out var statusFilter))
            query = query.Where(b => b.Status == statusFilter);

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (overdue == true)
            query = query.Where(b => b.DueDate.HasValue && b.DueDate.Value < today && b.Status != BillStatus.FullyPaid);

        var total = await query.CountAsync();
        var overdueCount = await db.SupplierBills
            .Where(b => b.IsActive && b.DueDate.HasValue && b.DueDate.Value < today && b.Status != BillStatus.FullyPaid)
            .CountAsync();
        var totalUnpaid = await db.SupplierBills
            .Where(b => b.IsActive && b.Status != BillStatus.FullyPaid && b.Status != BillStatus.Cancelled)
            .SumAsync(b => b.TotalAmount - b.PaidAmount);

        var bills = await query
            .OrderByDescending(b => b.BillDate)
            .ThenByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                b.Id,
                b.BillNumber,
                SupplierName = b.Supplier != null ? b.Supplier.Name : "",
                b.SupplierId,
                b.Description,
                b.TotalAmount,
                b.PaidAmount,
                RemainingAmount = b.TotalAmount - b.PaidAmount,
                Status = b.Status.ToString(),
                StatusArabic = GetStatusArabic(b.Status),
                BillDate = b.BillDate.ToString("yyyy-MM-dd"),
                DueDate = b.DueDate.HasValue ? b.DueDate.Value.ToString("yyyy-MM-dd") : null,
                IsOverdue = b.DueDate.HasValue && b.DueDate.Value < today && b.Status != BillStatus.FullyPaid,
                PaymentsCount = b.Payments.Count(p => p.IsActive),
                b.AttachmentUrl,
                b.Notes,
                b.CreatedAt
            })
            .ToListAsync();

        return Ok(new { data = bills, total, page, pageSize, overdueCount, totalUnpaid });
    }

    /// <summary>GET /api/supplier-bills/{id} — Get bill details with payment history.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var bill = await db.SupplierBills
            .Include(b => b.Supplier)
            .Include(b => b.Payments.Where(p => p.IsActive))
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

        if (bill == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });

        return Ok(new
        {
            bill.Id,
            bill.BillNumber,
            SupplierName = bill.Supplier?.Name ?? "",
            SupplierPhone = bill.Supplier?.Phone,
            bill.SupplierId,
            bill.Description,
            bill.TotalAmount,
            bill.PaidAmount,
            RemainingAmount = bill.TotalAmount - bill.PaidAmount,
            Status = bill.Status.ToString(),
            StatusArabic = GetStatusArabic(bill.Status),
            BillDate = bill.BillDate.ToString("yyyy-MM-dd"),
            DueDate = bill.DueDate?.ToString("yyyy-MM-dd"),
            bill.AttachmentUrl,
            bill.Notes,
            Payments = bill.Payments.Select(p => new
            {
                p.Id,
                p.Amount,
                p.PaymentMethod,
                PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd"),
                p.ReferenceNumber,
                p.Notes,
                p.CreatedAt
            })
        });
    }

    /// <summary>GET /api/supplier-bills/supplier/{supplierId}/statement — Supplier ledger statement.</summary>
    [HttpGet("supplier/{supplierId:guid}/statement")]
    public async Task<IActionResult> GetSupplierStatement(Guid supplierId)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId && s.IsActive);
        if (supplier == null) return NotFound(new { message = "المورد غير موجود" });

        var bills = await db.SupplierBills
            .Include(b => b.Payments.Where(p => p.IsActive))
            .Where(b => b.SupplierId == supplierId && b.IsActive)
            .OrderBy(b => b.BillDate)
            .ToListAsync();

        var totalBilled = bills.Sum(b => b.TotalAmount);
        var totalPaid = bills.Sum(b => b.PaidAmount);
        var totalRemaining = totalBilled - totalPaid;

        var entries = bills.SelectMany(b =>
        {
            var list = new List<object>
            {
                new
                {
                    Date = b.BillDate.ToString("yyyy-MM-dd"),
                    Type = "فاتورة",
                    Reference = b.BillNumber,
                    Description = b.Description,
                    Debit = b.TotalAmount,
                    Credit = 0m,
                    Status = GetStatusArabic(b.Status)
                }
            };
            list.AddRange(b.Payments.Select(p => (object)new
            {
                Date = p.PaymentDate.ToString("yyyy-MM-dd"),
                Type = "دفعة",
                Reference = p.ReferenceNumber ?? $"PMT-{p.CreatedAt:yyyyMMdd}",
                Description = $"دفعة جزئية على فاتورة {b.BillNumber}",
                Debit = 0m,
                Credit = p.Amount,
                Status = ""
            }));
            return list;
        }).OrderBy(e => e.GetType().GetProperty("Date")!.GetValue(e)).ToList();

        return Ok(new
        {
            SupplierId = supplierId,
            SupplierName = supplier.Name,
            SupplierPhone = supplier.Phone,
            SupplierEmail = supplier.Email,
            totalBilled,
            totalPaid,
            totalRemaining,
            BillsCount = bills.Count,
            Entries = entries
        });
    }

    /// <summary>POST /api/supplier-bills/{id}/pay — Record an installment payment for a bill.
    /// Dual-writes CashFlowTransaction + JournalEntry atomically.</summary>
    [HttpPost("{id:guid}/pay")]
    public async Task<IActionResult> Pay(Guid id, [FromBody] PayBillInstallmentRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ الدفعة أكبر من الصفر" });

        var billSnapshot = await db.SupplierBills.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
        if (billSnapshot == null)
            return NotFound(new { message = "الفاتورة غير موجودة" });

        if (billSnapshot.Status == BillStatus.FullyPaid)
            return BadRequest(new { message = "هذه الفاتورة مدفوعة بالكامل بالفعل" });

        if (billSnapshot.Status == BillStatus.Cancelled)
            return BadRequest(new { message = "هذه الفاتورة ملغاة" });

        var paymentDate = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(req.PaymentDate) && DateOnly.TryParse(req.PaymentDate, out var parsedDate))
            paymentDate = parsedDate;

        var userId = currentUser.UserId ?? Guid.Empty;
        var branchId = billSnapshot.BranchId;

        // Guard: reject if branch is invalid
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، الفرع غير محدد لفاتورة المورد. تواصل مع الإدارة." });

        // Resolve treasury by payment method for JournalEntry posting
        // Phase 0B: Cash bill payments require an open cashier session
        CashierSession? activeSession = null;
        if (string.Equals(req.PaymentMethod, "cash", StringComparison.OrdinalIgnoreCase))
        {
            activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
            if (activeSession == null)
                return BadRequest(new { message = "عذراً، يجب فتح صندوق الكاشير (الوردية اليومية) أولاً قبل سداد فواتير المورد النقدية." });
        }

        Treasury treasury;
        try
        {
            treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, req.PaymentMethod, activeSession?.Id);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // CONCURRENCY SAFETY: Begin transaction FIRST, then lock the bill row and re-check remaining.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire transaction-scoped pessimistic lock on the bill row
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""SupplierBills"" WHERE ""Id"" = {0} FOR UPDATE",
                    id);
            }

            // Reload the bill inside the lock
            var bill = await db.SupplierBills.Include(b => b.Supplier)
                .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
            if (bill == null)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "الفاتورة غير موجودة" });
            }

            if (bill.Status == BillStatus.FullyPaid)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "هذه الفاتورة مدفوعة بالكامل بالفعل" });
            }

            if (bill.Status == BillStatus.Cancelled)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "هذه الفاتورة ملغاة" });
            }

            // Re-calculate remaining inside the lock to prevent overpayment
            var remaining = bill.TotalAmount - bill.PaidAmount;
            if (req.Amount > remaining)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = $"مبلغ الدفعة ({req.Amount:N0}) يتجاوز المبلغ المتبقي ({remaining:N0} ريال)" });
            }
            // Dual-write: CashFlowTransaction (transitional)
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-BILL-{DateTime.UtcNow:HHmmss}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.SupplierPayment,
                Amount = req.Amount,
                PaymentMethod = req.PaymentMethod,
                TransactionDate = paymentDate,
                ReferenceId = bill.Id,
                ReferenceNumber = bill.BillNumber,
                Description = $"دفعة على فاتورة مورد {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                PerformedBy = userId,
                BranchId = branchId,
                CashierSessionId = activeSession?.Id,
                TreasuryId = treasury.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            var payment = new SupplierBillPayment
            {
                SupplierBillId = bill.Id,
                Amount = req.Amount,
                PaymentMethod = req.PaymentMethod,
                PaymentDate = paymentDate,
                ReferenceNumber = req.ReferenceNumber,
                Notes = req.Notes?.Trim(),
                PaidBy = userId,
                CashFlowTransactionId = cashflow.Id
            };
            db.SupplierBillPayments.Add(payment);

            // Dual-write: JournalEntry (canonical) — Debit AccountsPayable / Credit Treasury
            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.SupplierPayment,
                financialDocumentId: payment.Id,
                description: $"سداد فاتورة مورد: {bill.BillNumber} — {bill.Supplier?.Name ?? ""}",
                entryDate: paymentDate,
                branchId: branchId,
                performedBy: userId,
                cashierSessionId: activeSession?.Id,
                treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Payable, bill.SupplierId, req.Amount, 0m, (string?)$"سداد مستحقات: {bill.Supplier?.Name}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, req.Amount, (string?)$"سداد من: {treasury.Name}")
                });
            je.IsPosted = true;
            je.PostedAt = DateTime.UtcNow;

            // Blocker 1: Atomically decrement Treasury.Balance for the supplier outflow
            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, req.PaymentMethod, req.Amount, activeSession?.Id);

            // Update bill paid amount and status
            bill.PaidAmount += req.Amount;
            bill.Status = bill.PaidAmount >= bill.TotalAmount ? BillStatus.FullyPaid : BillStatus.PartiallyPaid;

            // Finance Phase 1: Update supplier balance (reduce what we owe)
            if (bill.Supplier != null)
            {
                bill.Supplier.Balance -= req.Amount;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for supplier bill payment
            await audit.LogAsync(AuditAction.Create, "SupplierBillPayment", payment.Id,
                details: $"Bill {id} partial payment");

            return Ok(new
            {
                message = bill.Status == BillStatus.FullyPaid
                    ? "تم سداد الفاتورة بالكامل! تم ترحيل القيد للأستاذ العام."
                    : $"تم تسجيل الدفعة بنجاح. المبلغ المتبقي: {bill.TotalAmount - bill.PaidAmount:N0} ريال",
                bill.Id,
                bill.BillNumber,
                bill.PaidAmount,
                RemainingAmount = bill.TotalAmount - bill.PaidAmount,
                Status = bill.Status.ToString(),
                StatusArabic = GetStatusArabic(bill.Status)
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>DELETE /api/supplier-bills/{id} — Cancel a bill (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminAccess")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var bill = await db.SupplierBills.FindAsync(id);
        if (bill == null || !bill.IsActive)
            return NotFound(new { message = "الفاتورة غير موجودة" });

        if (bill.PaidAmount > 0)
            return BadRequest(new { message = "لا يمكن إلغاء فاتورة تم سداد جزء منها. يرجى مراجعة المحاسب." });

        var userId = currentUser.UserId;
        bill.Status = BillStatus.Cancelled;
        bill.IsActive = false;
        bill.DeletedAt = DateTime.UtcNow;
        bill.DeletedBy = userId;

        await db.SaveChangesAsync();
        return Ok(new { message = "تم إلغاء الفاتورة بنجاح" });
    }

    private static string GetStatusArabic(BillStatus status) => status switch
    {
        BillStatus.Unpaid => "غير مدفوع",
        BillStatus.PartiallyPaid => "مدفوع جزئياً",
        BillStatus.FullyPaid => "مدفوع بالكامل",
        BillStatus.Cancelled => "ملغي",
        _ => status.ToString()
    };
}
