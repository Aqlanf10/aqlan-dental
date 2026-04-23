using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(DashboardService service) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await service.GetStatsAsync();
        return Ok(stats);
    }
}
