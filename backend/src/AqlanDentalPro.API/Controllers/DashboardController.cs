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

    private bool CanViewFinance() =>
        User.IsInRole("Admin")
        || User.IsInRole("Accountant")
        || User.IsInRole("Reception")
        || User.HasClaim("permission", "finance.view");
}
