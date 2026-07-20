using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    public sealed record CreateAccountingPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, string? Notes);

    [HttpGet("accounting-periods")]
    public async Task<IActionResult> GetAccountingPeriods()
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var periods = await db.AccountingPeriods
            .Where(p => !branchId.HasValue || p.BranchId == branchId.Value)
            .OrderByDescending(p => p.EndDate)
            .Select(p => new { p.Id, p.Name, p.StartDate, p.EndDate, p.Status, p.ClosedAt, p.Notes, p.BranchId })
            .ToListAsync();
        return Ok(new { data = periods });
    }

    [HttpPost("accounting-periods")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateAccountingPeriod([FromBody] CreateAccountingPeriodRequest req)
    {
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty || string.IsNullOrWhiteSpace(req.Name) || req.StartDate == default || req.EndDate < req.StartDate)
            return BadRequest(new { message = "أدخل اسماً وفترة مالية صحيحة." });
        var overlaps = await db.AccountingPeriods.AnyAsync(p => p.BranchId == branchId && p.StartDate <= req.EndDate && p.EndDate >= req.StartDate);
        if (overlaps) return Conflict(new { message = "الفترة الجديدة تتداخل مع فترة مالية موجودة." });
        var period = new AccountingPeriod { BranchId = branchId, Name = req.Name.Trim(), StartDate = req.StartDate, EndDate = req.EndDate, Notes = req.Notes?.Trim() };
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Create, "AccountingPeriod", period.Id, details: $"{period.Name}: {period.StartDate:yyyy-MM-dd}..{period.EndDate:yyyy-MM-dd}");
        return CreatedAtAction(nameof(GetAccountingPeriods), new { id = period.Id }, period);
    }

    [HttpPost("accounting-periods/{id:guid}/close")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CloseAccountingPeriod(Guid id)
    {
        var period = await db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == id);
        if (period is null) return NotFound(new { message = "الفترة غير موجودة." });
        if (!currentUser.IsAdmin && period.BranchId != currentUser.BranchId) return Deny();
        if (period.Status == "Closed") return BadRequest(new { message = "الفترة مقفلة بالفعل." });
        var hasUnposted = await db.JournalEntries.AnyAsync(e => e.BranchId == period.BranchId && e.EntryDate >= period.StartDate && e.EntryDate <= period.EndDate && !e.IsPosted);
        if (hasUnposted) return BadRequest(new { message = "لا يمكن الإقفال: توجد قيود غير مرحلة داخل الفترة." });
        period.Status = "Closed"; period.ClosedAt = DateTime.UtcNow; period.ClosedBy = currentUser.UserId;
        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "AccountingPeriod", period.Id, details: "Period closed");
        return Ok(new { message = "تم إقفال الفترة. أي قيد جديد بتاريخ داخلها سيُرفض." });
    }
}
