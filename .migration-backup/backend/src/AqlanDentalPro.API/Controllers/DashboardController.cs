using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "StaffOnly")]
public class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await service.GetStatsAsync(CanViewFinance());
        return Ok(stats);
    }

    [HttpGet("charts")]
    public async Task<IActionResult> GetCharts()
    {
        var charts = await service.GetChartsAsync(CanViewFinance());
        return Ok(charts);
    }

    /// <summary>
    /// GET /api/dashboard/alerts — operational attention counters (overdue lab
    /// work, today's no-shows, long-waiting patients, tomorrow's unconfirmed
    /// appointments, recall candidates). No financial data — safe for all staff.
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts()
    {
        var alerts = await service.GetAlertsAsync();
        return Ok(alerts);
    }

    private bool CanViewFinance() =>
        User.IsInRole("Admin")
        || User.IsInRole("Accountant")
        || User.IsInRole("Reception")
        || User.HasClaim("permission", "finance.view");
}
