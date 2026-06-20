using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "StaffOnly")]
public class LabReportsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    private Task<bool> CanViewReportsAsync() => PermissionGuard.HasAsync(db, currentUser, "lab_reports", "view");

    /// <summary>Lab costs report — total costs per lab.</summary>
    [HttpGet("lab-costs")]
    public async Task<IActionResult> GetLabCosts([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (!await CanViewReportsAsync()) return Forbid();

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
        if (!await CanViewReportsAsync()) return Forbid();

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

    /// <summary>
    /// Lab Sprint 6 — Lab performance KPIs.
    /// Returns per-lab metrics: avg execution days, remake %, overdue %, total orders.
    /// </summary>
    [HttpGet("lab-performance")]
    public async Task<IActionResult> GetLabPerformance([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var query = db.LabOrders
            .Include(o => o.Lab)
            .Where(o => o.LabId != null)
            .AsQueryable();

        if (from.HasValue) query = query.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(o => o.CreatedAt <= to.Value);

        var today = ClinicTimeProvider.ClinicToday();

        // Get raw data grouped by lab for in-memory KPI calculations
        var labGroups = await query
            .GroupBy(o => new { o.LabId, LabName = o.Lab != null ? o.Lab.Name : "غير محدد" })
            .Select(g => new
            {
                LabId = g.Key.LabId,
                LabName = g.Key.LabName,
                TotalOrders = g.Count(),
                DeliveredOrders = g.Count(o => o.Status == "delivered"),
                RemakeOrders = g.Count(o => o.Status == "remake" || o.RemakeCount > 0),
                OverdueOrders = g.Count(o =>
                    o.ExpectedDate != null &&
                    o.ExpectedDate < today &&
                    o.Status != "delivered" &&
                    o.Status != "cancelled"),
                CancelledOrders = g.Count(o => o.Status == "cancelled"),
                TotalCost = g.Sum(o => o.TotalCost ?? o.Cost ?? 0),
                // For avg execution days: count only orders with both SentDate and ReceivedDate
                OrdersWithExecutionDays = g
                    .Where(o => o.SentDate != null && o.ReceivedDate != null)
                    .Select(o => (int?)(o.ReceivedDate!.Value.DayNumber - o.SentDate!.Value.DayNumber))
                    .ToList(),
            })
            .ToListAsync();

        var report = labGroups.Select(g =>
        {
            var avgDays = g.OrdersWithExecutionDays.Count > 0
                ? Math.Round(g.OrdersWithExecutionDays.Average() ?? 0, 1)
                : 0;
            var remakeRate = g.TotalOrders > 0
                ? Math.Round((double)g.RemakeOrders / g.TotalOrders * 100, 1)
                : 0;
            var overdueRate = g.TotalOrders > 0
                ? Math.Round((double)g.OverdueOrders / g.TotalOrders * 100, 1)
                : 0;
            var onTimeRate = g.DeliveredOrders > 0
                ? Math.Round((double)(g.DeliveredOrders - g.OverdueOrders) / g.DeliveredOrders * 100, 1)
                : 0;

            return new
            {
                g.LabId,
                g.LabName,
                g.TotalOrders,
                g.DeliveredOrders,
                g.RemakeOrders,
                g.OverdueOrders,
                g.CancelledOrders,
                g.TotalCost,
                AvgExecutionDays = avgDays,
                RemakeRate = remakeRate,
                OverdueRate = overdueRate,
                OnTimeRate = Math.Max(0, onTimeRate),
            };
        }).OrderByDescending(r => r.TotalOrders).ToList();

        // Overall summary
        var totalOrders = report.Sum(r => r.TotalOrders);
        var overallAvgDays = report.Count > 0
            ? Math.Round(report.Average(r => r.AvgExecutionDays), 1)
            : 0;
        var overallRemakeRate = totalOrders > 0
            ? Math.Round((double)report.Sum(r => r.RemakeOrders) / totalOrders * 100, 1)
            : 0;
        var overallOverdueRate = totalOrders > 0
            ? Math.Round((double)report.Sum(r => r.OverdueOrders) / totalOrders * 100, 1)
            : 0;

        var summary = new
        {
            TotalLabs = report.Count,
            TotalOrders = totalOrders,
            TotalDelivered = report.Sum(r => r.DeliveredOrders),
            TotalOverdue = report.Sum(r => r.OverdueOrders),
            TotalRemakes = report.Sum(r => r.RemakeOrders),
            TotalCost = report.Sum(r => r.TotalCost),
            OverallAvgExecutionDays = overallAvgDays,
            OverallRemakeRate = overallRemakeRate,
            OverallOverdueRate = overallOverdueRate,
        };

        return Ok(new { data = report, summary });
    }

    /// <summary>
    /// Lab Sprint 6 — Lab dashboard summary.
    /// Returns overall KPIs and recent activity for the lab dashboard page.
    /// </summary>
    [HttpGet("lab-dashboard")]
    public async Task<IActionResult> GetLabDashboard()
    {
        if (!await CanViewReportsAsync()) return Forbid();

        var today = ClinicTimeProvider.ClinicToday();
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // Overall counts
        var totalOrders = await db.LabOrders.CountAsync();
        var pendingOrders = await db.LabOrders.CountAsync(o =>
            o.Status == "sent" || o.Status == "manufacturing" || o.Status == "remake");
        var readyOrders = await db.LabOrders.CountAsync(o => o.Status == "ready");
        var receivedOrders = await db.LabOrders.CountAsync(o => o.Status == "received");
        var overdueOrders = await db.LabOrders.CountAsync(o =>
            o.ExpectedDate != null &&
            o.ExpectedDate < today &&
            o.Status != "delivered" &&
            o.Status != "cancelled");
        var deliveredThisMonth = await db.LabOrders.CountAsync(o =>
            o.Status == "delivered" && o.DeliveredDate != null && o.DeliveredDate >= today.AddDays(-30));
        var returnedOrders = await db.LabOrders.CountAsync(o => o.Status == "returned");
        var remakeOrders = await db.LabOrders.CountAsync(o => o.Status == "remake");

        // Financial summary
        var totalLabCosts = await db.LabOrders
            .Where(o => o.LabId != null)
            .SumAsync(o => o.TotalCost ?? o.Cost ?? 0);
        var totalDebt = await db.LabPayables
            .Where(p => p.Status != "paid")
            .SumAsync(p => p.Amount - p.PaidAmount);

        // Status distribution for chart
        var statusDistribution = await db.LabOrders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // Orders by lab (top 5)
        var topLabs = await db.LabOrders
            .Include(o => o.Lab)
            .Where(o => o.LabId != null)
            .GroupBy(o => new { o.LabId, LabName = o.Lab != null ? o.Lab.Name : "غير محدد" })
            .Select(g => new
            {
                LabId = g.Key.LabId,
                LabName = g.Key.LabName,
                OrderCount = g.Count(),
                TotalCost = g.Sum(o => o.TotalCost ?? o.Cost ?? 0),
            })
            .OrderByDescending(l => l.OrderCount)
            .Take(5)
            .ToListAsync();

        // Recent overdue orders
        var recentOverdue = await db.LabOrders
            .Include(o => o.Patient)
            .Include(o => o.Lab)
            .Where(o => o.ExpectedDate != null && o.ExpectedDate < today
                && o.Status != "delivered" && o.Status != "cancelled")
            .OrderBy(o => o.ExpectedDate)
            .Take(10)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                PatientName = o.Patient.FirstName + " " + o.Patient.LastName,
                LabName = o.Lab != null ? o.Lab.Name : o.LabName,
                o.ApplianceType,
                ExpectedDate = o.ExpectedDate != null ? o.ExpectedDate.Value.ToString("yyyy-MM-dd") : null,
                DaysOverdue = o.ExpectedDate != null ? (int)(today.DayNumber - o.ExpectedDate.Value.DayNumber) : 0,
                o.Status,
                o.Priority,
            })
            .ToListAsync();

        // Monthly trend (last 6 months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var monthlyTrend = await db.LabOrders
            .Where(o => o.CreatedAt >= sixMonthsAgo)
            .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalOrders = g.Count(),
                DeliveredOrders = g.Count(o => o.Status == "delivered"),
                TotalCost = g.Sum(o => o.TotalCost ?? o.Cost ?? 0),
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync();

        var dashboard = new
        {
            KPIs = new
            {
                TotalOrders = totalOrders,
                PendingOrders = pendingOrders,
                ReadyOrders = readyOrders,
                ReceivedOrders = receivedOrders,
                OverdueOrders = overdueOrders,
                DeliveredThisMonth = deliveredThisMonth,
                ReturnedOrders = returnedOrders,
                RemakeOrders = remakeOrders,
                TotalLabCosts = totalLabCosts,
                TotalDebt = totalDebt,
            },
            StatusDistribution = statusDistribution,
            TopLabs = topLabs,
            RecentOverdue = recentOverdue,
            MonthlyTrend = monthlyTrend,
        };

        return Ok(new { data = dashboard });
    }
}
