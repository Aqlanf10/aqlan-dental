using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Services;
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

    /// <summary>
    /// Finance V3: Required classification for external deposits.
    /// Must be one of: OwnerCapital, OpeningBalance, OtherReceivable, AuthorizedRevenueDocument.
    /// If null or unrecognized, the deposit will be rejected from Finance V3 posting.
    /// </summary>
    public string? DepositSource { get; init; }
}

public sealed class ApproveTransferRequest
{
    /// <summary>
    /// Finance V3: Required classification for external deposits.
    /// Must be one of: OwnerCapital, OpeningBalance, OtherReceivable, AuthorizedRevenueDocument.
    /// If null or unrecognized, the deposit will be rejected from Finance V3 posting.
    /// </summary>
    public string? DepositSource { get; init; }
}

public sealed class RejectTransferRequest
{
    public string Notes { get; init; } = string.Empty;
}

[ApiController]
[Route("api/vault-transfers")]
[Authorize(Policy = "FinanceAccess")]
public class VaultTransfersController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit, IJournalEntryService journalEntryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        // Blocker 4: Non-admin users must have a valid branch to view transfers
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

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

        // Blocker 4: Non-admin users must have a valid branch to create transfers
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.BranchId ?? Guid.Empty;
        var userId = currentUser.UserId ?? Guid.Empty;

        // Verify destination treasury (read-only check; re-verified is not needed inside tx
        // because we only ADD to its balance on Approve, not here).
        var destTreasury = await db.Treasuries
            .FirstOrDefaultAsync(t => t.Id == req.DestinationTreasuryId && t.BranchId == branchId && t.IsActive);
        if (destTreasury == null)
            return BadRequest(new { message = "الخزنة المستهدفة غير موجودة أو غير تابعة للفرع" });

        // FIN-05 FIX: The source treasury existence + balance check MUST happen inside the
        // transaction with a row lock. Previously the check was at line 135 BEFORE
        // BeginTransactionAsync (line 140), and the advisory lock (line 144) was on the
        // transfer-number sequence — NOT on the source treasury row. Two concurrent transfers
        // from the same source both passed the balance check, both entered the tx, both
        // deducted via tracked-entity mutation (no atomic SQL, no FOR UPDATE), and the
        // treasury balance could go negative.
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Lock on the transfer-number sequence (existing behavior — preserves order).
            var lockKey = StableLockKeyHelper.VaultTransferNumber;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // FIN-05: If a source treasury is specified, acquire a row-level lock on it so
            // two concurrent transfers from the same source serialize. Combined with the
            // re-check below, this prevents the negative-balance race.
            Treasury? sourceTreasury = null;
            if (req.SourceTreasuryId.HasValue)
            {
                if (db.Database.IsRelational())
                {
                    // FOR UPDATE on the source treasury row (PostgreSQL). On InMemory (tests),
                    // this is skipped — the advisory lock above provides serialization there.
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"Treasuries\" WHERE \"Id\" = {0} FOR UPDATE",
                        req.SourceTreasuryId.Value);
                }

                // AUTHORITATIVE RE-CHECK inside the lock: reload the source treasury.
                sourceTreasury = await db.Treasuries
                    .FirstOrDefaultAsync(t => t.Id == req.SourceTreasuryId.Value && t.BranchId == branchId && t.IsActive);
                if (sourceTreasury == null)
                    return BadRequest(new { message = "الخزنة المصدر غير موجودة أو غير تابعة للفرع" });

                if (sourceTreasury.Balance < req.Amount)
                    return BadRequest(new { message = $"عذراً، رصيد الخزنة المصدر ({sourceTreasury.Balance:N0} ر.ي) أقل من مبلغ التحويل المطلوب ({req.Amount:N0} ر.ي)" });
            }

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
                Notes = req.Notes?.Trim(),
                DepositSource = req.DepositSource // Finance V3: Store deposit source classification
            };

            // Deduct the source treasury immediately (lock/block funds).
            // Safe now: we hold the row lock + re-checked Balance >= Amount inside the lock.
            if (sourceTreasury != null)
            {
                sourceTreasury.Balance -= req.Amount;
            }

            db.VaultTransfers.Add(transfer);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // H3: Audit logging for vault transfer creation
            await audit.LogAsync(AuditAction.Create, "VaultTransfer", transfer.Id,
                details: $"Created transfer {transfer.TransferNumber}: amount={transfer.Amount:N0}, from={transfer.SourceTreasuryId}, to={transfer.DestinationTreasuryId}, session={transfer.CashierSessionId}");

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
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTransferRequest? approveReq = null)
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

        // Blocker 4: Branch isolation — non-admin users MUST have a valid branch assignment
        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
                return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

            var destBranchId = transfer.DestinationTreasury.BranchId;
            if (destBranchId != currentUser.BranchId.Value)
                return StatusCode(403, new { message = "ليس لديك صلاحية الموافقة على تحويلات فرع آخر" });
        }

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
            TransactionDate = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday()),
            ReferenceId = transfer.Id,
            ReferenceNumber = transfer.TransferNumber,
            Description = $"ترحيل سيولة مادية داخلية: من {sourceName} إلى {destName}",
            PerformedBy = transfer.PerformedBy,
            BranchId = transfer.DestinationTreasury.BranchId,
            CashierSessionId = transfer.CashierSessionId
        };
        db.CashFlowTransactions.Add(cashflow);

        // Phase 0B: Create paired Outflow CashFlowTransaction for the source treasury
        // to complete the audit trail. Money leaving the source must be recorded.
        if (transfer.SourceTreasury != null)
        {
            var sourceCashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-TR-OUT-{transfer.TransferNumber[3..]}",
                Type = TransactionType.Outflow,
                Category = FinancialCategory.InternalTransfer,
                Amount = transfer.Amount,
                PaymentMethod = transfer.SourceTreasury.Type == TreasuryType.Bank ? "bank" : "cash",
                TransactionDate = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday()),
                ReferenceId = transfer.Id,
                ReferenceNumber = transfer.TransferNumber,
                Description = $"تحويل سيولة خارجية: من {sourceName} إلى {destName}",
                PerformedBy = transfer.PerformedBy,
                BranchId = transfer.SourceTreasury.BranchId,
                CashierSessionId = transfer.CashierSessionId
            };
            db.CashFlowTransactions.Add(sourceCashflow);
        }

        // Finance V3: External deposit journal entry with DepositSource classification
        // AND Internal transfer journal entry (Debit Destination / Credit Source)
        // Wrap in explicit transaction for relational DB to guarantee atomicity.
        var useApproveTx = db.Database.IsRelational();
        var approveTx = useApproveTx ? await db.Database.BeginTransactionAsync() : null;
        try
        {
            if (transfer.SourceTreasuryId == null)
            {
                var depositSource = approveReq?.DepositSource;
                if (string.IsNullOrWhiteSpace(depositSource))
                {
                    if (useApproveTx) await approveTx!.RollbackAsync();
                    return BadRequest(new { message = "يجب تحديد مصدر الإيداع الخارجي (OwnerCapital أو OpeningBalance أو OtherReceivable أو AuthorizedRevenueDocument)" });
                }

                var branchId = transfer.DestinationTreasury.BranchId;
                var treasuryId = transfer.DestinationTreasury.Id;
                var performedBy = transfer.PerformedBy;

                var (creditAccountType, creditAccountId, description) = depositSource switch
                {
                    "OwnerCapital" => (JournalAccountType.OwnerEquity, branchId, $"إيداع رأس مال مالك - {transfer.TransferNumber}"),
                    "OpeningBalance" => (JournalAccountType.OwnerEquity, branchId, $"رصيد افتتاحي - {transfer.TransferNumber}"),
                    "OtherReceivable" => (JournalAccountType.OtherReceivable, branchId, $"إيداع ذمم مدينة أخرى - {transfer.TransferNumber}"),
                    "AuthorizedRevenueDocument" => (JournalAccountType.Revenue, transfer.Id, $"إيراد مستندي معتمد - {transfer.TransferNumber}"),
                    _ => ((JournalAccountType)0, Guid.Empty, "")
                };

                if (creditAccountId == Guid.Empty)
                {
                    if (useApproveTx) await approveTx!.RollbackAsync();
                    return BadRequest(new { message = $"مصدر الإيداع غير معروف: {depositSource}. القيم المقبولة: OwnerCapital, OpeningBalance, OtherReceivable, AuthorizedRevenueDocument" });
                }

                var journalLines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
                {
                    (JournalAccountType.Treasury, treasuryId, transfer.Amount, 0m, $"استلام إيداع خارجي - {transfer.TransferNumber}"),
                    (creditAccountType, creditAccountId, 0m, transfer.Amount, description)
                };

                var jeEntry = await journalEntryService.CreateEntryAsync(
                    documentType: FinancialDocumentType.VaultTransfer,
                    financialDocumentId: transfer.Id,
                    description: description,
                    entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    branchId: branchId,
                    performedBy: performedBy,
                    cashierSessionId: transfer.CashierSessionId,
                    treasuryId: treasuryId,
                    lines: journalLines);

                jeEntry.IsPosted = true;
                jeEntry.PostedAt = DateTime.UtcNow;
            }
            else
            {
                // Blocker 5: Internal vault transfer JE — Debit Destination / Credit Source
                // This is NOT revenue; it's a transfer between two treasury accounts.
                var sourceTreasuryId = transfer.SourceTreasuryId.Value;
                var destTreasuryId = transfer.DestinationTreasuryId;
                var branchId = transfer.DestinationTreasury.BranchId;

                var journalLines = new List<(JournalAccountType, Guid, decimal, decimal, string?)>
                {
                    (JournalAccountType.Treasury, destTreasuryId, transfer.Amount, 0m, $"استلام تحويل داخلي - {transfer.TransferNumber}"),
                    (JournalAccountType.Treasury, sourceTreasuryId, 0m, transfer.Amount, $"إرسال تحويل داخلي - {transfer.TransferNumber}")
                };

                var jeEntry = await journalEntryService.CreateEntryAsync(
                    documentType: FinancialDocumentType.VaultTransfer,
                    financialDocumentId: transfer.Id,
                    description: $"تحويل سيولة داخلية: {transfer.TransferNumber}",
                    entryDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    branchId: branchId,
                    performedBy: transfer.PerformedBy,
                    cashierSessionId: transfer.CashierSessionId,
                    treasuryId: destTreasuryId,
                    lines: journalLines);

                // Auto-post
                jeEntry.IsPosted = true;
                jeEntry.PostedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            if (useApproveTx) await approveTx!.CommitAsync();
        }
        catch
        {
            if (useApproveTx) await approveTx!.RollbackAsync();
            throw;
        }

        // H3: Audit logging for vault transfer approval
        await audit.LogAsync(AuditAction.Approve, "VaultTransfer", id,
            details: $"Approved transfer: amount={transfer.Amount:N0}, status={transfer.Status}, approvedBy={currentUser.UserId}");

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

        // Blocker 4: Branch isolation — non-admin users MUST have a valid branch assignment
        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty)
                return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

            var destBranchId = transfer.DestinationTreasury.BranchId;
            if (destBranchId != currentUser.BranchId.Value)
                return StatusCode(403, new { message = "ليس لديك صلاحية رفض تحويلات فرع آخر" });
        }

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

        // H3: Audit logging for vault transfer rejection
        await audit.LogAsync(AuditAction.Update, "VaultTransfer", id, details: "Transfer rejected");

        return Ok(new
        {
            transfer.Id,
            transfer.TransferNumber,
            Status = transfer.Status.ToString(),
            message = "تم رفض طلب التحويل وإرجاع المبالغ المقيدة للخزنة المصدر بنجاح"
        });
    }
}
