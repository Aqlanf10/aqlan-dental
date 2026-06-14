using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/photo-analysis")]
[Authorize(Policy = "OrthoAccess")]
public class PhotoAnalysisController(PhotoAnalysisService service) : ControllerBase
{
    // GET /api/photo-analysis?orthoCaseId={id} — saved analyses for a case
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid orthoCaseId)
    {
        if (orthoCaseId == Guid.Empty)
            return BadRequest(new { message = "رقم حالة التقويم مطلوب" });
        return Ok(await service.ListAsync(orthoCaseId));
    }

    // GET /api/photo-analysis/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await service.GetByIdAsync(id);
        return result is null
            ? NotFound(new { message = "تحليل الصورة غير موجود" })
            : Ok(result);
    }

    // POST /api/photo-analysis
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePhotoAnalysisRequest req)
    {
        var (result, error) = await service.CreateAsync(req);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // DELETE /api/photo-analysis/{id} — soft delete
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await service.SoftDeleteAsync(id);
        return ok ? Ok(new { message = "تم حذف تحليل الصورة" })
                  : NotFound(new { message = "تحليل الصورة غير موجود" });
    }
}
