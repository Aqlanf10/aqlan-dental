using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph")]
[Authorize]
public class CephController(CephService service) : ControllerBase
{
    // GET /api/ceph                          — all analyses
    // GET /api/ceph?orthoCaseId={id}         — filtered by ortho case
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? orthoCaseId)
    {
        var result = await service.ListAsync(orthoCaseId);
        return Ok(result);
    }

    // GET /api/ceph/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await service.GetByIdAsync(id);
        return result is null
            ? NotFound(new { message = "تحليل السيفالومتري غير موجود" })
            : Ok(result);
    }

    // POST /api/ceph
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCephAnalysisRequest req)
    {
        var result = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // POST /api/ceph/{id}/landmarks  — saves, computes, returns full detail
    [HttpPost("{id:guid}/landmarks")]
    public async Task<IActionResult> SaveLandmarks(Guid id, [FromBody] SaveLandmarksRequest req)
    {
        var ok = await service.SaveLandmarksAsync(id, req);
        if (!ok) return NotFound(new { message = "تحليل السيفالومتري غير موجود" });
        var detail = await service.GetByIdAsync(id);
        return Ok(detail);
    }

    // POST /api/ceph/{id}/simulate
    [HttpPost("{id:guid}/simulate")]
    public async Task<IActionResult> SimulateAi(Guid id, [FromBody] AiSimulateRequest req)
    {
        var result = await service.SimulateAiAsync(id, req);
        return Ok(result);
    }

    // PUT /api/ceph/{id}/diagnosis
    [HttpPut("{id:guid}/diagnosis")]
    public async Task<IActionResult> SaveDiagnosis(Guid id, [FromBody] SaveDiagnosisRequest req)
    {
        var ok = await service.SaveDiagnosisAsync(id, req);
        return ok
            ? Ok(new { message = "تم حفظ التشخيص بنجاح" })
            : NotFound(new { message = "تحليل السيفالومتري غير موجود" });
    }

    // DELETE /api/ceph/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var analysis = await service.GetByIdAsync(id);
        if (analysis is null)
            return NotFound(new { message = "تحليل السيفالومتري غير موجود" });

        await service.SoftDeleteAsync(id);
        return Ok(new { message = "تم حذف التحليل بنجاح" });
    }
}
