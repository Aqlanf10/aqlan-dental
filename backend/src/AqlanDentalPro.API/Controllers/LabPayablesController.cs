using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/lab-payables")]
[Authorize(Policy = "StaffOnly")]
public class LabPayablesController(
    AppDbContext db,
    ICurrentUserService currentUser,
    ILogger<LabPayablesController> logger,
    ITreasuryResolutionService treasuryResolution,
    IJournalEntryService journalEntryService,
    IAuditService audit,
    ISupplierRefundService? supplierRefundService = null) : ControllerBase
{
    private Task<bool> CanAsync(string action) => PermissionGuard.HasAsync(db, currentUser, "lab_payables", action);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? labId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await CanAsync("view")) return Forbid();

        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .Include(p => p.SupplierBill)
            .AsQueryable();

        if (labId.HasValue) query = query.Where(p => p.LabId == labId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

        // CORE-LAB-008: this screen used to read LabPayables ONLY. A LabPayable row is
        // created solely from a lab ORDER, so a debt entered as an opening balance —
        // what the clinic owed a lab before it started using the system — produces a
        // SupplierBill with LabOrderId == null and NO payable, and was therefore
        // invisible here while showing correctly on the supplier account. The debt was
        // not missing; the screen was reading the narrower of the two tables.
        //
        // Those orphan bills are appended below as read-only rows. They are NOT folded
        // into the payable projection because RecordPayment takes a LabPayable id: an
        // opening balance has none, so it is flagged HasPayable = false and must be
        // settled from the suppliers module. Fabricating a payable row here would
        // create a second, unreconciled record of the same debt.
        var payableRows = await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id,
                // Widened to Guid? so this projection matches the orphan-bill one below
                // exactly — Concat of two anonymous types requires identical shapes.
                LabOrderId = (Guid?)p.LabOrderId,
                LabName = p.Lab != null ? p.Lab.Name : "",
                OrderNumber = p.LabOrder != null ? p.LabOrder.OrderNumber : "",
                PatientName = p.LabOrder != null && p.LabOrder.Patient != null
                    ? p.LabOrder.Patient.FirstName + " " + p.LabOrder.Patient.LastName
                    : "",
                Amount = p.SupplierBill != null ? p.SupplierBill.TotalAmount : p.Amount,
                PaidAmount = p.SupplierBill != null ? p.SupplierBill.PaidAmount : p.PaidAmount,
                Balance = p.SupplierBill != null
                    ? p.SupplierBill.TotalAmount - p.SupplierBill.PaidAmount
                    : p.Amount - p.PaidAmount,
                Status = p.SupplierBill != null
                    ? p.SupplierBill.Status == BillStatus.FullyPaid ? "paid"
                        : p.SupplierBill.Status == BillStatus.PartiallyPaid ? "partial" : "pending"
                    : p.Status,
                Currency = p.SupplierBill != null ? p.SupplierBill.Currency : "YER",
                ExchangeRateToYer = p.SupplierBill != null ? p.SupplierBill.ExchangeRateToYer : 1m,
                SupplierBillId = p.SupplierBillId,
                DueDate = p.DueDate != null ? p.DueDate.Value.ToString("yyyy-MM-dd") : null,
                Notes = p.Notes,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd"),
                HasPayable = true,
                IsOpeningBalance = false,
                SortKey = p.CreatedAt
            })
            .ToListAsync();

        // Lab supplier bills with no payable of their own: opening balances and any bill
        // raised directly against a dental lab from the suppliers module.
        var orphanQuery = db.SupplierBills
            .Where(b => b.IsActive
                     && db.Suppliers.Any(sup => sup.Id == b.SupplierId && sup.Type == SupplierType.DentalLab)
                     && !db.LabPayables.Any(lp => lp.SupplierBillId == b.Id));

        // A lab filter must not silently hide these: match through the lab that points at
        // this supplier. An opening balance carries no LabId of its own.
        if (labId.HasValue)
            orphanQuery = orphanQuery.Where(b =>
                db.Labs.Any(l => l.Id == labId.Value && l.SupplierId == b.SupplierId));

        if (!string.IsNullOrWhiteSpace(status))
        {
            var wanted = status.Trim().ToLowerInvariant();
            orphanQuery = orphanQuery.Where(b =>
                (wanted == "paid" && b.Status == BillStatus.FullyPaid)
                || (wanted == "partial" && b.Status == BillStatus.PartiallyPaid)
                || (wanted == "pending" && b.Status == BillStatus.Unpaid));
        }

        var orphanRows = await orphanQuery
            .Select(b => new
            {
                Id = b.Id,
                LabOrderId = (Guid?)null,
                LabName = db.Labs.Where(l => l.SupplierId == b.SupplierId).Select(l => l.Name).FirstOrDefault()
                          ?? db.Suppliers.Where(sup => sup.Id == b.SupplierId).Select(sup => sup.Name).FirstOrDefault()
                          ?? "",
                OrderNumber = "",
                PatientName = "",
                Amount = b.TotalAmount,
                PaidAmount = b.PaidAmount,
                Balance = b.TotalAmount - b.PaidAmount,
                Status = b.Status == BillStatus.FullyPaid ? "paid"
                       : b.Status == BillStatus.PartiallyPaid ? "partial" : "pending",
                Currency = b.Currency,
                ExchangeRateToYer = b.ExchangeRateToYer,
                SupplierBillId = (Guid?)b.Id,
                DueDate = b.DueDate != null ? b.DueDate.Value.ToString("yyyy-MM-dd") : null,
                Notes = (string?)b.Description,
                CreatedAt = b.BillDate.ToString("yyyy-MM-dd"),
                UpdatedAt = b.UpdatedAt.ToString("yyyy-MM-dd"),
                HasPayable = false,
                IsOpeningBalance = b.IsOpeningBalance,
                SortKey = b.UpdatedAt
            })
            .ToListAsync();

        // Merged in memory because the two sources are shaped differently and a debt list
        // for one clinic's labs is small. Paging AFTER the merge keeps the page numbers
        // honest — paging each source separately would drop rows off the end of a page.
        var merged = payableRows.Concat(orphanRows)
            .OrderByDescending(row => row.SortKey)
            .ToList();

        var total = merged.Count;
        var data = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // CORE-LAB-013: the screen's status cards used to be counted from the current page, next
        // to a grand total counted server-wide — so with more than one page of payables the four
        // numbers did not add up, and they changed as the user paged. Counted here over the whole
        // merged set, they describe the same population the total does.
        var statusCounts = new
        {
            Pending = merged.Count(r => r.Status == "pending"),
            Partial = merged.Count(r => r.Status == "partial"),
            Paid    = merged.Count(r => r.Status == "paid"),
        };

        // Balances are per currency for the same reason every other lab total is — a YER debt
        // and a SAR debt are not addable.
        var balanceByCurrency = merged
            .GroupBy(r => r.Currency)
            .Select(g => new { Currency = g.Key, Amount = g.Sum(r => r.Balance) })
            .Where(x => x.Amount != 0m)
            .OrderByDescending(x => x.Amount)
            .ToList();

        return Ok(new { data, total, page, pageSize, statusCounts, balanceByCurrency });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        var payable = await db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .Include(p => p.SupplierBill)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payable is null) return NotFound(new { message = "المديونية غير موجودة" });

        return Ok(new
        {
            payable.Id,
            payable.LabOrderId,
            LabName = payable.Lab?.Name ?? "",
            OrderNumber = payable.LabOrder?.OrderNumber ?? "",
            Amount = payable.SupplierBill?.TotalAmount ?? payable.Amount,
            PaidAmount = payable.SupplierBill?.PaidAmount ?? payable.PaidAmount,
            Balance = payable.SupplierBill is not null
                ? payable.SupplierBill.TotalAmount - payable.SupplierBill.PaidAmount
                : payable.Amount - payable.PaidAmount,
            Status = payable.SupplierBill is not null
                ? payable.SupplierBill.Status == BillStatus.FullyPaid ? "paid"
                    : payable.SupplierBill.Status == BillStatus.PartiallyPaid ? "partial" : "pending"
                : payable.Status,
            Currency = payable.SupplierBill?.Currency ?? "YER",
            ExchangeRateToYer = payable.SupplierBill?.ExchangeRateToYer ?? 1m,
            payable.SupplierBillId,
            DueDate = payable.DueDate?.ToString("yyyy-MM-dd"),
            payable.Notes,
            CreatedAt = payable.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    public sealed class RecordPaymentRequest
    {
        public decimal Amount { get; init; }
        public string PaymentMethod { get; init; } = "cash";
        public Guid? TreasuryId { get; init; }
        public decimal? ExchangeRateToYer { get; init; }
        public string? ReferenceNumber { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPost("{id:guid}/record-payment")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        if (req.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        var userId = currentUser.UserId ?? Guid.Empty;

        var canonical = await db.LabPayables
            .AsNoTracking()
            .Where(item => item.Id == id && item.IsActive)
            .Select(item => new { item.SupplierBillId })
            .FirstOrDefaultAsync();
        if (canonical is null)
            return NotFound(new { message = "المديونية غير موجودة" });

        if (canonical.SupplierBillId.HasValue && supplierRefundService is not null)
        {
            SupplierPaymentPostingResult posting;
            try
            {
                posting = await supplierRefundService.PaySupplierBillAsync(
                    canonical.SupplierBillId.Value,
                    new PaySupplierBillRequest
                    {
                        Amount = req.Amount,
                        PaymentMethod = req.PaymentMethod,
                        TreasuryId = req.TreasuryId,
                        ExchangeRateToYer = req.ExchangeRateToYer,
                        ExchangeRateSource = req.ExchangeRateToYer.HasValue ? "manual" : null,
                        ReferenceNumber = req.ReferenceNumber,
                        Notes = req.Notes
                    },
                    userId);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            var bill = await db.SupplierBills.AsNoTracking()
                .FirstAsync(item => item.Id == canonical.SupplierBillId.Value);
            var legacy = await db.LabPayables.FirstAsync(item => item.Id == id);
            legacy.PaidAmount = bill.PaidAmount;
            legacy.Status = bill.Status == BillStatus.FullyPaid ? "paid" : "partial";
            if (!string.IsNullOrWhiteSpace(req.Notes))
                legacy.Notes = string.IsNullOrWhiteSpace(legacy.Notes) ? req.Notes : $"{legacy.Notes}\n{req.Notes}";
            await db.SaveChangesAsync();

            await audit.LogAsync(AuditAction.Update, "LabPayable", id,
                details: $"Canonical supplier bill payment: bill={bill.Id}; amount={req.Amount}; journal={posting.JournalEntryId}");
            return Ok(new
            {
                legacy.Id,
                legacy.PaidAmount,
                legacy.Status,
                Balance = bill.TotalAmount - bill.PaidAmount,
                supplierBillId = bill.Id,
                paymentId = posting.PaymentId,
                journalEntryId = posting.JournalEntryId,
                journalEntryNumber = posting.JournalEntryNumber,
                message = "تم سداد مستحق المعمل من نفس فاتورة المورد وترحيل القيد وسند الصرف."
            });
        }

        // Require active open cashier session
        var activeSession = await db.CashierSessions
            .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);
        if (activeSession == null)
            return BadRequest(new { message = "يجب فتح وردية الكاشير أولاً قبل تسجيل دفعة معملية." });

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Acquire row lock to prevent race conditions
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    @"SELECT 1 FROM ""LabPayables"" WHERE ""Id"" = {0} FOR UPDATE",
                    id);
            }

            var payable = await db.LabPayables
                .Include(p => p.Lab)
                .Include(p => p.LabOrder)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (payable == null)
            {
                await tx.RollbackAsync();
                return NotFound(new { message = "المديونية غير موجودة" });
            }

            var remaining = payable.Amount - payable.PaidAmount;
            if (remaining <= 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "هذه المستحقات مدفوعة بالكامل بالفعل" });
            }

            if (req.Amount > remaining)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = $"مبلغ الدفعة ({req.Amount:N0}) يتجاوز المبلغ المتبقي ({remaining:N0} ريال)" });
            }

            // Resolve branch ID
            var branchId = payable.LabOrder?.BranchId 
                           ?? currentUser.BranchId 
                           ?? Guid.Empty;
            if (branchId == Guid.Empty)
            {
                var firstBranch = await db.Branches
                    .Where(b => b.IsActive)
                    .OrderBy(b => b.CreatedAt)
                    .FirstOrDefaultAsync();
                branchId = firstBranch?.Id ?? Guid.Empty;
            }

            if (branchId == Guid.Empty)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = "لم يتم تحديد فرع صالح لتسجيل المعاملة المالية." });
            }

            // Resolve treasury
            Treasury treasury;
            try
            {
                treasury = await treasuryResolution.ResolveTreasuryAsync(branchId, "cash", null, activeSession.Id);
            }
            catch (ArgumentException ex)
            {
                await tx.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }

            // Update Payable
            payable.PaidAmount += req.Amount;
            payable.Status = payable.PaidAmount >= payable.Amount ? "paid" : "partial";
            if (req.Notes != null)
            {
                payable.Notes = string.IsNullOrWhiteSpace(payable.Notes) 
                    ? req.Notes 
                    : $"{payable.Notes}\n{req.Notes}";
            }
            payable.UpdatedAt = DateTime.UtcNow;

            // Create CashFlowTransaction
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var timePart = DateTime.UtcNow.ToString("HHmmss");
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-LAB-{timePart}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.SupplierPayment,
                Amount = req.Amount,
                PaymentMethod = "cash",
                TransactionDate = ClinicTimeProvider.ClinicToday(),
                ReferenceId = payable.Id,
                ReferenceNumber = payable.LabOrder?.OrderNumber,
                Description = $"سداد مستحقات معمل: {payable.Lab?.Name ?? "معمل"} — طلب رقم {payable.LabOrder?.OrderNumber ?? "غير محدد"}",
                PerformedBy = userId,
                BranchId = branchId,
                CashierSessionId = activeSession.Id,
                TreasuryId = treasury.Id
            };
            db.CashFlowTransactions.Add(cashflow);

            // Create JournalEntry
            var je = await journalEntryService.CreateEntryAsync(
                documentType: FinancialDocumentType.SupplierPayment,
                financialDocumentId: payable.Id,
                description: $"سداد مستحقات معمل: {payable.Lab?.Name ?? "معمل"} — طلب رقم {payable.LabOrder?.OrderNumber ?? "غير محدد"}",
                entryDate: ClinicTimeProvider.ClinicToday(),
                branchId: branchId,
                performedBy: userId,
                cashierSessionId: activeSession.Id,
                treasuryId: treasury.Id,
                lines: new[]
                {
                    (JournalAccountType.Payable, payable.LabId, req.Amount, 0m, (string?)$"سداد مستحقات: {payable.Lab?.Name}"),
                    (JournalAccountType.Treasury, treasury.Id, 0m, req.Amount, (string?)$"سداد من: {treasury.Name}")
                }, autoSave: false);
            je.IsPosted = true;
            je.PostedAt = DateTime.UtcNow;

            // Decrement Treasury Balance
            await treasuryResolution.DecrementTreasuryBalanceAsync(branchId, "cash", req.Amount, null, activeSession.Id);

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Update, "LabPayable", payable.Id,
                details: $"Payment recorded: amount={req.Amount:N0}, treasury={treasury?.Name}, session={activeSession?.Id}");
            logger.LogInformation("LabPayable payment recorded securely: {Id} — {Amount}", id, req.Amount);

            return Ok(new
            {
                payable.Id,
                payable.PaidAmount,
                payable.Status,
                Balance = payable.Amount - payable.PaidAmount,
                cashierSessionId = activeSession.Id,
                cashFlowTransactionId = cashflow.Id,
                journalEntryId = je.Id,
                message = "تم تسجيل الدفعة معملياً ومالياً بنجاح"
            });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            logger.LogError(ex, "Error recording LabPayable payment: {Id}", id);
            throw;
        }
    }
}
