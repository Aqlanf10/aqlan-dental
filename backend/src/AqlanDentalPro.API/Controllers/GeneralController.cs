using AqlanDentalPro.Application.DTOs.General;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
[Authorize(Policy = "GeneralAccess")]
public class GeneralController(GeneralService service) : ControllerBase
{
    [HttpGet("dental-chart/{patientId:guid}")]
    public async Task<IActionResult> GetChart(Guid patientId)
    {
        var result = await service.GetOrCreateChartAsync(patientId);
        return Ok(result);
    }

    [HttpPut("dental-chart/{patientId:guid}/teeth")]
    public async Task<IActionResult> UpdateTooth(Guid patientId, [FromBody] UpdateToothRequest req)
    {
        var result = await service.UpdateToothAsync(patientId, req);
        return Ok(result);
    }

    [HttpGet("general-treatments/{patientId:guid}")]
    public async Task<IActionResult> GetTreatments(Guid patientId)
    {
        var result = await service.GetTreatmentsAsync(patientId);
        return Ok(result);
    }

    [HttpPost("general-treatments")]
    public async Task<IActionResult> CreateTreatment([FromBody] CreateGeneralTreatmentRequest req)
    {
        var result = await service.CreateTreatmentAsync(req);
        return Ok(result);
    }

    [HttpGet("general/recent-treatments")]
    public async Task<IActionResult> GetRecentTreatments([FromQuery] int limit = 20)
    {
        var result = await service.GetRecentTreatmentsAsync(limit);
        return Ok(result);
    }
}
