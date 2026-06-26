using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.API.Authorization;
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
    IAuditService audit) : ControllerBase
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
            .AsQueryable();

        if (labId.HasValue) query = query.Where(p => p.LabId == labId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

        var total = await query.CountAsync();
        var payables = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.LabOrderId,
                LabName = p.Lab != null ? p.Lab.Name : "",
                OrderNumber = p.LabOrder != null ? p.LabOrder.OrderNumber : "",
                PatientName = p.LabOrder != null && p.LabOrder.Patient != null
                    ? p.LabOrder.Patient.FirstName + " " + p.LabOrder.Patient.LastName
                    : "",
                p.Amount,
                p.PaidAmount,
                Balance = p.Amount - p.PaidAmount,
                p.Status,
                DueDate = p.DueDate != null ? p.DueDate.Value.ToString("yyyy-MM-dd") : null,
                p.Notes,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = p.UpdatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = payables, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Forbid();

        var payable = await db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payable is null) return NotFound(new { message = "المديونية غير موجودة" });

        return Ok(new
        {
            payable.Id,
            payable.LabOrderId,
            LabName = payable.Lab?.Name ?? "",
            OrderNumber = payable.LabOrder?.OrderNumber ?? "",
            payable.Amount,
            payable.PaidAmount,
            Balance = payable.Amount - payable.PaidAmount,
            payable.Status,
            DueDate = payable.DueDate?.ToString("yyyy-MM-dd"),
            payable.Notes,
            CreatedAt = payable.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    public sealed class RecordPaymentRequest
    {
        public decimal Amount { get; init; }
        public string? Notes { get; init; }
    }

    [HttpPost("{id:guid}/record-payment")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest req)
    {
        if (!await CanAsync("edit")) return Forbid();

        if (req.Amount <= 0)
            return BadRequest(new { message = "المبلغ يجب أن يكون أكبر من صفر" });

        var userId = currentUser.UserId ?? Guid.Empty;

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
            logger.LogInformation("LabPayable payment recorded securely: {Id} â€” {Amount}", id, req.Amount);

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
