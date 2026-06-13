using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph")]
[Authorize(Policy = "OrthoAccess")]
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

    // GET /api/ceph/compare?baseId=&targetId= — C-B pre/post comparison
    // (declared before {id:guid} so "compare" never binds as an id)
    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] Guid baseId, [FromQuery] Guid targetId)
    {
        if (baseId == Guid.Empty || targetId == Guid.Empty || baseId == targetId)
            return BadRequest(new { message = "حدد تحليلين مختلفين للمقارنة" });

        var (result, error) = await service.CompareAsync(baseId, targetId);
        if (error == "التحليل غير موجود") return NotFound(new { message = error });
        if (error is not null) return BadRequest(new { message = error });
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
    // Template-based landmark simulation — NOT AI (honest labeling, see
    // CephService.SimulateTemplateAsync). Disabled by default; enable via the
    // Settings key "ceph.simulation_enabled".
    [HttpPost("{id:guid}/simulate")]
    public async Task<IActionResult> SimulateTemplate(Guid id, [FromBody] AiSimulateRequest req)
    {
        if (!await service.IsSimulationEnabledAsync())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "المحاكاة التجريبية معطلة من الإعدادات" });

        var result = await service.SimulateTemplateAsync(id, req);
        return Ok(result);
    }

    // POST /api/ceph/{id}/ai/auto-trace
    // Honest placeholder for real AI auto-tracing (future specialized vision
    // model integration point). Always 501 until a real model is integrated —
    // the system must never fake AI results.
    [HttpPost("{id:guid}/ai/auto-trace")]
    public IActionResult AutoTrace(Guid id)
        => StatusCode(StatusCodes.Status501NotImplemented, new
        {
            status  = "unavailable",
            message = "التتبع الآلي بالذكاء الاصطناعي يتطلب نموذج رؤية متخصص — قيد التطوير. استخدم الوضع اليدوي."
        });

    // POST /api/ceph/{id}/ai/draft-diagnosis
    // C-D: REAL LLM draft-diagnosis assistant (Gemini/Anthropic via
    // IAiDraftProvider) — never fake AI.
    // Returns either a real model response or an honest Arabic error:
    //   403 when disabled (Settings "ai.ceph_draft_enabled"), the provider is
    //       unsupported (e.g. openai), or its env-var API key is missing —
    //       message states exactly which,
    //   429 when the configurable monthly limit (ai.monthly_limit) is reached,
    //   502 when the upstream AI API call failed.
    // The draft is NEVER auto-saved: the response always carries the review
    // disclaimer and the doctor explicitly copies it into FinalDiagnosis.
    // Every attempt is audited in OrthodonticAiLogs.
    [HttpPost("{id:guid}/ai/draft-diagnosis")]
    public async Task<IActionResult> DraftDiagnosis(
        Guid id,
        [FromServices] AqlanDentalPro.Infrastructure.Services.CephAiDraftService aiDraftService,
        [FromServices] ILogger<CephController> logger)
    {
        try
        {
            var result = await aiDraftService.GenerateDraftAsync(id);
            if (result is null)
                return NotFound(new { message = "تحليل السيفالومتري غير موجود" });

            return Ok(new
            {
                draft = result.Draft,
                modelId = result.ModelId,
                disclaimer = result.Disclaimer,
                generatedAt = result.GeneratedAt,
            });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiUnavailableException ex)
        {
            // Honest, specific Arabic reason (flag off vs unsupported provider vs missing API key).
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiLimitReachedException ex)
        {
            // Configurable monthly usage limit (Settings "ai.monthly_limit").
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiUpstreamException ex)
        {
            // Security rule: never expose exception/upstream details to clients.
            logger.LogError(ex, "AI draft-diagnosis upstream failure for analysis {AnalysisId}", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "تعذر الاتصال بخدمة الذكاء الاصطناعي — حاول لاحقًا" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected AI draft-diagnosis failure for analysis {AnalysisId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء توليد مسودة التشخيص" });
        }
    }

    // GET /api/ceph/{id}/report/pdf — C-C Arabic cephalometric PDF report
    [HttpGet("{id:guid}/report/pdf")]
    public async Task<IActionResult> GetReportPdf(
        Guid id,
        [FromServices] CephReportPdfGenerator reportGenerator,
        [FromServices] ILogger<CephController> logger)
    {
        try
        {
            var pdfBytes = await reportGenerator.GenerateAsync(id);
            return File(pdfBytes, "application/pdf", $"ceph-report-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "تحليل السيفالومتري غير موجود" });
        }
        catch (Exception ex)
        {
            // Security rule: never expose exception details in HTTP responses —
            // full details go to the server log only.
            logger.LogError(ex, "Failed to generate ceph report PDF for analysis {AnalysisId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء تقرير التحليل السيفالومتري" });
        }
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
