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
public class TreasuriesController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var branchId = currentUser.BranchId ?? Guid.Empty;
        var isAdmin = currentUser.IsAdmin;

        var query = db.Treasuries
            .Where(t => t.IsActive);

        if (!isAdmin)
        {
            query = query.Where(t => t.BranchId == branchId);
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

        return Ok(list);
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

        var branchId = currentUser.BranchId ?? Guid.Empty;

        var treasury = new Treasury
        {
            Name = req.Name.Trim(),
            Type = type,
            Balance = req.OpeningBalance,
            BranchId = branchId,
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
                TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                ReferenceId = treasury.Id,
                ReferenceNumber = "OP-BAL",
                Description = $"رصيد افتتاحي لبداية تشغيل {treasury.Name}",
                PerformedBy = currentUser.UserId ?? Guid.Empty,
                BranchId = branchId
            };
            db.CashFlowTransactions.Add(cashflow);
        }

        await db.SaveChangesAsync();

        return Ok(new
        {
            treasury.Id,
            treasury.Name,
            Type = treasury.Type.ToString(),
            treasury.Balance,
            message = "تم إنشاء الخزنة/الحساب المالي بنجاح"
        });
    }
}
