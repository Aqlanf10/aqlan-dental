using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "StaffOnly")]
public class LabReportsController(AppDbContext db) : ControllerBase
{
    /// <summary>Lab costs report — total costs per lab.</summary>
    [HttpGet("lab-costs")]
    public async Task<IActionResult> GetLabCosts([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var query = db.LabOrders
            .Include(o => o.Lab)
            .Where(o => o.LabId != null)
            .AsQueryable();

        if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(o => o.CreatedAt <= to.Value);

        var report = await query
            .GroupBy(o => new { o.LabId, LabName = o.Lab != null ? o.Lab.Name : "غير محدد" })
            .Select(g => new
            {
                LabId = g.Key.LabId,
                LabName = g.Key.LabName,
                TotalOrders = g.Count(),
                TotalCost = g.Sum(o => o.TotalCost ?? o.Cost ?? 0),
                PendingOrders = g.Count(o => o.Status == "sent" || o.Status == "manufacturing"),
                ReturnedOrders = g.Count(o => o.Status == "returned"),
                RemakeOrders = g.Count(o => o.Status == "remake"),
            })
            .OrderByDescending(r => r.TotalCost)
            .ToListAsync();

        return Ok(new { data = report });
    }

    /// <summary>Lab debts report — unpaid/partial payables.</summary>
    [HttpGet("lab-debts")]
    public async Task<IActionResult> GetLabDebts([FromQuery] Guid? labId)
    {
        var query = db.LabPayables
            .Include(p => p.Lab)
            .Include(p => p.LabOrder)
            .Where(p => p.Status != "paid")
            .AsQueryable();

        if (labId.HasValue) query = query.Where(p => p.LabId == labId.Value);

        var debts = await query
            .Select(p => new
            {
                p.Id,
                p.LabId,
                LabName = p.Lab.Name,
                OrderNumber = p.LabOrder.OrderNumber,
                p.Amount,
                p.PaidAmount,
                Balance = p.Amount - p.PaidAmount,
                p.Status,
                DueDate = p.DueDate != null ? p.DueDate.Value.ToString("yyyy-MM-dd") : null,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .OrderByDescending(p => p.Balance)
            .ToListAsync();

        var summary = new
        {
            TotalDebt = debts.Sum(d => d.Balance),
            TotalPayables = debts.Count,
            PendingCount = debts.Count(d => d.Status == "pending"),
            PartialCount = debts.Count(d => d.Status == "partial"),
        };

        return Ok(new { data = debts, summary });
    }
}
