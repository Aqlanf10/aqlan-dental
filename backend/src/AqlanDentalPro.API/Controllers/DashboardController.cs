using AqlanDentalPro.Application.Services;
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
        var stats = await service.GetStatsAsync();
        return Ok(stats);
    }

    [HttpGet("charts")]
    public async Task<IActionResult> GetCharts()
    {
        var charts = await service.GetChartsAsync();
        return Ok(charts);
    }
}
