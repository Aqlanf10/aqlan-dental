using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ortho-cases")]
[Authorize]
public class OrthoCasesController(OrthoService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? patientId = null)
    {
        var result = await service.GetListAsync(page, pageSize, doctorId, status, search, patientId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await service.GetByIdAsync(id);
        return result == null ? NotFound(new { message = "الحالة التقويمية غير موجودة" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrthoCaseRequest req)
    {
        var result = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}/visits")]
    public async Task<IActionResult> GetVisits(Guid id)
    {
        var result = await service.GetVisitsAsync(id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/visits")]
    public async Task<IActionResult> AddVisit(Guid id, [FromBody] CreateOrthoVisitRequest req)
    {
        var result = await service.AddVisitAsync(id, req);
        return Ok(result);
    }

    [HttpGet("{id:guid}/stages")]
    public async Task<IActionResult> GetStages(Guid id)
    {
        var result = await service.GetStagesAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> UpdateStage(Guid id, Guid stageId, [FromBody] UpdateStageRequest req)
    {
        var result = await service.UpdateStageAsync(stageId, req.Status);
        return result == null ? NotFound() : Ok(result);
    }
}

public class UpdateStageRequest
{
    public string Status { get; set; } = string.Empty;
}
