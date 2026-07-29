using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/photo-analysis")]
[Authorize(Policy = "OrthoAccess")]
public class PhotoAnalysisController(
    PhotoAnalysisService service,
    AppDbContext db,
    IPatientAccessService patientAccess) : ControllerBase
{
    // GET /api/photo-analysis?orthoCaseId={id} — saved analyses for a case
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid orthoCaseId)
    {
        if (orthoCaseId == Guid.Empty)
            return BadRequest(new { message = "رقم حالة التقويم مطلوب" });
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        return Ok(await service.ListAsync(orthoCaseId));
    }

    // GET /api/photo-analysis/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var result = await service.GetByIdAsync(id);
        return result is null
            ? NotFound(new { message = "تحليل الصورة غير موجود" })
            : Ok(result);
    }

    // POST /api/photo-analysis
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePhotoAnalysisRequest req)
    {
        var accessError = await GetCaseAccessErrorAsync(req.OrthoCaseId);
        if (accessError is not null) return accessError;

        var (result, error) = await service.CreateAsync(req);
        if (error is not null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // DELETE /api/photo-analysis/{id} — soft delete
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var ok = await service.SoftDeleteAsync(id);
        return ok ? Ok(new { message = "تم حذف تحليل الصورة" })
                  : NotFound(new { message = "تحليل الصورة غير موجود" });
    }

    // GET /api/photo-analysis/{id}/report/pdf — Arabic PDF report
    [HttpGet("{id:guid}/report/pdf")]
    public async Task<IActionResult> ReportPdf(
        Guid id,
        [FromServices] AqlanDentalPro.API.Services.PhotoAnalysisReportPdfGenerator pdf)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        try
        {
            var bytes = await pdf.GenerateAsync(id);
            return File(bytes, "application/pdf", $"photo-analysis-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "تحليل الصورة غير موجود" });
        }
    }

    // ── Patient-access guards (same pattern as CephController) ──────────────
    private async Task<IActionResult?> GetCaseAccessErrorAsync(Guid orthoCaseId)
    {
        var patientId = await db.OrthoCases
            .AsNoTracking()
            .Where(x => x.Id == orthoCaseId && x.IsActive)
            .Select(x => (Guid?)x.PatientId)
            .FirstOrDefaultAsync();

        if (!patientId.HasValue)
            return NotFound(new { message = "حالة التقويم غير موجودة" });

        return await patientAccess.CanAccessPatientAsync(patientId.Value) ? null : Forbid();
    }

    private async Task<IActionResult?> GetAnalysisAccessErrorAsync(Guid id)
    {
        var patientId = await db.PhotoAnalyses
            .AsNoTracking()
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => (Guid?)x.OrthoCase.PatientId)
            .FirstOrDefaultAsync();

        if (!patientId.HasValue)
            return NotFound(new { message = "تحليل الصورة غير موجود" });

        return await patientAccess.CanAccessPatientAsync(patientId.Value) ? null : Forbid();
    }
}
