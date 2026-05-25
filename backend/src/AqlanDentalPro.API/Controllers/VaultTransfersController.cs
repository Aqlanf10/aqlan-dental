using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateTransferRequest
{
    public Guid? SourceTreasuryId { get; init; }
    public Guid DestinationTreasuryId { get; init; }
    public decimal Amount { get; init; }
    public string? Notes { get; init; }
}

public sealed class RejectTransferRequest
{
    public string Notes { get; init; } = string.Empty;
}

[ApiController]
[Route("api/vault-transfers")]
[Authorize(Policy = "FinanceAccess")]
public class VaultTransfersController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var branchId = currentUser.BranchId ?? Guid.Empty;
        var isAdmin = currentUser.IsAdmin;

        var query = db.VaultTransfers
            .Include(t => t.SourceTreasury)
            .Include(t => t.DestinationTreasury)
            .Include(t => t.PerformedByUser)
            .Include(t => t.ApprovedByUser)
            .Where(t => t.IsActive);

        if (!isAdmin)
        {
            query = query.Where(t => t.DestinationTreasury.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TransferStatus>(status, true, out var filterStatus))
        {
            query = query.Where(t => t.Status == filterStatus);
        }

        var total = await query.CountAsync();
        var list = await query
            .OrderByDescending(t => t.TransferDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.TransferNumber,
                SourceTreasuryId = t.SourceTreasuryId,
                SourceTreasuryName = t.SourceTreasury != null ? t.SourceTreasury.Name : "إيداع خارجي",
                DestinationTreasuryId = t.DestinationTreasuryId,
                DestinationTreasuryName = t.DestinationTreasury.Name,
                t.Amount,
                t.TransferDate,
                PerformedBy = t.PerformedByUser.Username,
                ApprovedBy = t.ApprovedByUser != null ? t.ApprovedByUser.Username : null,
                t.ApprovalDate,
                Status = t.Status.ToString(),
                StatusArabic = t.Status == TransferStatus.Pending ? "معلق" :
                               t.Status == TransferStatus.Approved ? "مقبول" : "مرفوض",
                t.Notes
            })
            .ToListAsync();

        return Ok(new { data = list, total, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransferRequest req)
    {
        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ التحويل أكبر من الصفر" });

        var branchId = currentUser.BranchId ?? Guid.Empty;
        var userId = currentUser.UserId ?? Guid.Empty;

        // Verify destination treasury
        var destTreasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.Id == req.DestinationTreasuryId && t.BranchId == branchId && t.IsActive);
        if (destTreasury == null)
            return BadRequest(new { message = "الخزنة المستهدفة غير موجودة أو غير تابعة للفرع" });

        Treasury? sourceTreasury = null;
        if (req.SourceTreasuryId.HasValue)
        {
            sourceTreasury = await db.Treasuries
                .FirstOrDefaultAsync(t => t.Id == req.SourceTreasuryId.Value && t.BranchId == branchId && t.IsActive);
            if (sourceTreasury == null)
                return BadRequest(new { message = "الخزنة المصدر غير موجودة أو غير تابعة للفرع" });

            if (sourceTreasury.Balance < req.Amount)
                return BadRequest(new { message = $"عذراً، رصيد الخزنة المصدر ({sourceTreasury.Balance:N0} ر.ي) أقل من مبلغ التحويل المطلوب ({req.Amount:N0} ر.ي)" });
        }

        // Generate sequential transfer code TR-yyyyMMdd-NNN
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = Math.Abs("VaultTransferNumber".GetHashCode()) % 100000;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            var today = DateTime.UtcNow;
            var datePart = today.ToString("yyyyMMdd");
            var prefix = $"TR-{datePart}-";

            var lastTransfer = await db.VaultTransfers
                .IgnoreQueryFilters()
                .Where(t => t.TransferNumber.StartsWith(prefix))
                .OrderByDescending(t => t.TransferNumber)
                .Select(t => t.TransferNumber)
                .FirstOrDefaultAsync();

            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastTransfer) && lastTransfer.Length > prefix.Length)
            {
                var seqPart = lastTransfer[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq))
                    nextSeq = lastSeq + 1;
            }

            var transferNumber = $"{prefix}{nextSeq:D3}";

            // Check if there is an active cashier session for this cashier
            var activeSession = await db.CashierSessions
                .FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

            var transfer = new VaultTransfer
            {
                TransferNumber = transferNumber,
                SourceTreasuryId = req.SourceTreasuryId,
                DestinationTreasuryId = req.DestinationTreasuryId,
                CashierSessionId = activeSession?.Id,
                Amount = req.Amount,
                TransferDate = DateTime.UtcNow,
                PerformedBy = userId,
                Status = TransferStatus.Pending,
                Notes = req.Notes?.Trim()
            };

            // Deduct the source treasury immediately (lock/block funds)
            if (sourceTreasury != null)
            {
                sourceTreasury.Balance -= req.Amount;
            }

            db.VaultTransfers.Add(transfer);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new
            {
                transfer.Id,
                transfer.TransferNumber,
                transfer.Amount,
                Status = transfer.Status.ToString(),
                message = "تم إنشاء طلب ترحيل السيولة بنجاح وهو قيد المراجعة والاستلام الفعلي"
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Accountant")] // Only Admin or Accountant can reconcile and approve receipt of funds
    public async Task<IActionResult> Approve(Guid id)
    {
        var userId = currentUser.UserId ?? Guid.Empty;

        var transfer = await db.VaultTransfers
            .Include(t => t.SourceTreasury)
            .Include(t => t.DestinationTreasury)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

        if (transfer == null)
            return NotFound(new { message = "طلب التحويل غير موجود" });

        if (transfer.Status != TransferStatus.Pending)
            return BadRequest(new { message = "يمكن قبول طلبات التحويل المعلقة فقط" });

        // Add funds to destination treasury balance
        transfer.DestinationTreasury.Balance += transfer.Amount;

        transfer.Status = TransferStatus.Approved;
        transfer.ApprovedBy = userId;
        transfer.ApprovalDate = DateTime.UtcNow;

        // Auto-create central ledger cashflow transaction (InternalTransfer / Inflow+Outflow logic)
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var sourceName = transfer.SourceTreasury != null ? transfer.SourceTreasury.Name : "إيداع خارجي";
        var destName = transfer.DestinationTreasury.Name;

        var cashflow = new CashFlowTransaction
        {
            TransactionNumber = $"TX-{datePart}-TR-{transfer.TransferNumber[3..]}",
            Type = TransactionType.Inflow, // internally treated as inflow to the target vault
            Category = FinancialCategory.InternalTransfer,
            Amount = transfer.Amount,
            PaymentMethod = transfer.DestinationTreasury.Type == TreasuryType.Bank ? "bank" : "cash",
            TransactionDate = DateOnly.FromDateTime(DateTime.Today),
            ReferenceId = transfer.Id,
            ReferenceNumber = transfer.TransferNumber,
            Description = $"ترحيل سيولة مادية داخلية: من {sourceName} إلى {destName}",
            PerformedBy = transfer.PerformedBy,
            BranchId = transfer.DestinationTreasury.BranchId,
            CashierSessionId = transfer.CashierSessionId
        };
        db.CashFlowTransactions.Add(cashflow);

        await db.SaveChangesAsync();

        return Ok(new
        {
            transfer.Id,
            transfer.TransferNumber,
            Status = transfer.Status.ToString(),
            DestinationBalance = transfer.DestinationTreasury.Balance,
            message = "تم تأكيد الاستلام المادي بنجاح وترحيل المبالغ وتحديث الأرصدة للأستاذ العام"
        });
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin,Accountant")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectTransferRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Notes))
            return BadRequest(new { message = "يجب تحديد سبب رفض استلام المبالغ" });

        var userId = currentUser.UserId ?? Guid.Empty;

        var transfer = await db.VaultTransfers
            .Include(t => t.SourceTreasury)
            .Include(t => t.DestinationTreasury)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

        if (transfer == null)
            return NotFound(new { message = "طلب التحويل غير موجود" });

        if (transfer.Status != TransferStatus.Pending)
            return BadRequest(new { message = "يمكن رفض طلبات التحويل المعلقة فقط" });

        // Restore funds to source treasury balance
        if (transfer.SourceTreasury != null)
        {
            transfer.SourceTreasury.Balance += transfer.Amount;
        }

        transfer.Status = TransferStatus.Rejected;
        transfer.ApprovedBy = userId;
        transfer.ApprovalDate = DateTime.UtcNow;
        transfer.Notes = req.Notes.Trim();

        await db.SaveChangesAsync();

        return Ok(new
        {
            transfer.Id,
            transfer.TransferNumber,
            Status = transfer.Status.ToString(),
            message = "تم رفض طلب التحويل وإرجاع المبالغ المقيدة للخزنة المصدر بنجاح"
        });
    }
}
