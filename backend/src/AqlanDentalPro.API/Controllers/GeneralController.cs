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

    // ── Periodontal Records ──────────────────────────────────────────────────

    [HttpPost("general/perio")]
    public async Task<IActionResult> CreatePerioRecord([FromBody] CreatePerioRecordRequest req)
    {
        var result = await service.CreatePerioRecordAsync(req);
        return Ok(result);
    }

    [HttpGet("general/perio/{patientId:guid}")]
    public async Task<IActionResult> GetPerioRecords(Guid patientId)
    {
        var result = await service.GetPerioRecordsAsync(patientId);
        return Ok(result);
    }

    // ── Treatment Plan Items ─────────────────────────────────────────────────

    [HttpPost("general/treatment-plans")]
    public async Task<IActionResult> CreateTreatmentPlanItem([FromBody] CreateTreatmentPlanItemRequest req)
    {
        var result = await service.CreateTreatmentPlanItemAsync(req);
        return Ok(result);
    }

    [HttpGet("general/treatment-plans/{patientId:guid}")]
    public async Task<IActionResult> GetTreatmentPlanItems(Guid patientId)
    {
        var result = await service.GetTreatmentPlanItemsAsync(patientId);
        return Ok(result);
    }

    [HttpPatch("general/treatment-plans/{id:guid}/status")]
    public async Task<IActionResult> UpdateTreatmentPlanItemStatus(Guid id, [FromBody] UpdateTreatmentPlanStatusRequest req)
    {
        var result = await service.UpdateTreatmentPlanItemStatusAsync(id, req.Status);
        return result == null ? NotFound(new { message = "عنصر خطة العلاج غير موجود" }) : Ok(result);
    }
}
