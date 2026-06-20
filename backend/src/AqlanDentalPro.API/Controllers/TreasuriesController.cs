using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateTreasuryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "Vault"; // Vault or Bank
    public decimal OpeningBalance { get; init; }
}

[ApiController]
[Route("api/treasuries")]
[Authorize(Policy = "FinanceAccess")]
public class TreasuriesController(AppDbContext db, ICurrentUserService currentUser, IAuditService audit, ILogger<TreasuriesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var isAdmin = currentUser.IsAdmin;

        var query = db.Treasuries
            .Where(t => t.IsActive);

        if (!isAdmin)
        {
            query = query.Where(t => t.BranchId == currentUser.BranchId!.Value);
        }

        var list = await query
            .OrderBy(t => t.Type)
            .ThenBy(t => t.Name)
            .Select(t => new
            {
                t.Id,
                t.Name,
                Type = t.Type.ToString(),
                TypeArabic = t.Type == TreasuryType.Bank ? "حساب بنكي" : "خزنة مادية",
                t.Balance,
                t.BranchId
            })
            .ToListAsync();

        return Ok(new { data = list });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")] // Only Administrator can seed/create custom accounts/vaults
    public async Task<IActionResult> Create([FromBody] CreateTreasuryRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم الخزنة/الحساب مطلوب" });

        if (!Enum.TryParse<TreasuryType>(req.Type, true, out var type))
            return BadRequest(new { message = "نوع الخزنة غير صالح. المتاح: Vault أو Bank" });

        if (req.OpeningBalance < 0)
            return BadRequest(new { message = "رصيد البداية لا يمكن أن يكون سالباً" });

        var branchId = currentUser.BranchId;
        if (branchId == null || branchId == Guid.Empty)
            return BadRequest(new { message = "عذراً، يجب تحديد الفرع قبل إنشاء خزنة/حساب مالي." });

        var treasury = new Treasury
        {
            Name = req.Name.Trim(),
            Type = type,
            Balance = req.OpeningBalance,
            BranchId = branchId.Value,
            IsActive = true
        };

        db.Treasuries.Add(treasury);

        // If there is an opening balance, record it as a cashflow transaction in the ledger (Inflow)
        if (req.OpeningBalance > 0)
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-IN-OP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.InternalTransfer,
                Amount = req.OpeningBalance,
                PaymentMethod = type == TreasuryType.Bank ? "bank" : "cash",
                TransactionDate = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday()),
                ReferenceId = treasury.Id,
                ReferenceNumber = "OP-BAL",
                Description = $"رصيد افتتاحي لبداية تشغيل {treasury.Name}",
                PerformedBy = currentUser.UserId ?? Guid.Empty,
                BranchId = branchId.Value
            };
            db.CashFlowTransactions.Add(cashflow);
        }

        await db.SaveChangesAsync();

        // H3: Audit logging for treasury creation
        await audit.LogAsync(AuditAction.Create, "Treasury", treasury.Id);

        return Ok(new
        {
            treasury.Id,
            treasury.Name,
            Type = treasury.Type.ToString(),
            treasury.Balance,
            message = "تم إنشاء الخزنة/الحساب المالي بنجاح"
        });
    }

    // ─── C1: Treasury Recalculate Endpoint ──────────────────────────────────
    [HttpPost("{id:guid}/recalculate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RecalculateBalance(Guid id)
    {
        var treasury = await db.Treasuries.FindAsync(id);
        if (treasury == null || !treasury.IsActive)
            return NotFound(new { message = "الخزنة غير موجودة" });

        var oldBalance = treasury.Balance;

        // Determine which payment methods affect this treasury type
        bool isVaultType = treasury.Type == TreasuryType.Vault;
        var applicableMethods = isVaultType
            ? new[] { "cash" }
            : new[] { "card", "bank_transfer", "bank" };

        // Sum all active inflows for this treasury's branch and applicable payment methods
        var inflows = await db.CashFlowTransactions
            .Where(t => t.IsActive
                && t.Type == TransactionType.Inflow
                && t.BranchId == treasury.BranchId
                && applicableMethods.Contains(t.PaymentMethod.ToLower()))
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        // Sum all active outflows for this treasury's branch and applicable payment methods
        var outflows = await db.CashFlowTransactions
            .Where(t => t.IsActive
                && t.Type == TransactionType.Outflow
                && t.BranchId == treasury.BranchId
                && applicableMethods.Contains(t.PaymentMethod.ToLower()))
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var newBalance = inflows - outflows;
        var drift = newBalance - oldBalance;

        // Log warning if drift detected
        if (drift != 0)
        {
            logger.LogWarning("C1 Treasury drift detected for {TreasuryId} ({Name}): Old={OldBalance}, New={NewBalance}, Drift={Drift}",
                treasury.Id, treasury.Name, oldBalance, newBalance, drift);
        }

        // Update treasury balance to recalculated value
        treasury.Balance = newBalance;
        treasury.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "تعارض في تحديث رصيد الخزينة. يرجى المحاولة مرة أخرى." });
        }

        // H3: Audit logging for treasury recalculation
        await audit.LogAsync(AuditAction.Update, "Treasury", id,
            details: $"Recalculated: old={oldBalance}, new={newBalance}, drift={drift}");

        return Ok(new
        {
            treasury.Id,
            treasury.Name,
            OldBalance = oldBalance,
            NewBalance = newBalance,
            Drift = drift,
            DriftDetected = drift != 0,
            message = drift != 0
                ? $"تم إعادة حساب الرصيد. تم اكتشاف انحراف بمبلغ {drift:N0} ر.ي"
                : "تم إعادة حساب الرصيد. لا يوجد انحراف"
        });
    }

    // ─── C1: Treasury Transactions Endpoint ─────────────────────────────────
    [HttpGet("{id:guid}/transactions")]
    public async Task<IActionResult> GetTreasuryTransactions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var treasury = await db.Treasuries.FindAsync(id);
        if (treasury == null || !treasury.IsActive)
            return NotFound(new { message = "الخزنة غير موجودة" });

        // Filter by payment method matching treasury type
        bool isVaultType = treasury.Type == TreasuryType.Vault;
        var applicableMethods = isVaultType
            ? new[] { "cash" }
            : new[] { "card", "bank_transfer", "bank" };

        var query = db.CashFlowTransactions
            .Where(t => t.IsActive
                && t.BranchId == treasury.BranchId
                && applicableMethods.Contains(t.PaymentMethod.ToLower()));

        var total = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.TransactionNumber,
                Type = t.Type.ToString(),
                Category = t.Category.ToString(),
                t.Amount,
                t.PaymentMethod,
                t.TransactionDate,
                t.ReferenceNumber,
                t.Description,
                t.IsReversal,
                t.ReversalOfTransactionId
            })
            .ToListAsync();

        return Ok(new { data = transactions, total, page, pageSize });
    }
}
