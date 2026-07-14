using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph")]
[Authorize(Policy = "OrthoAccess")]
public class CephController(
    CephService service,
    AppDbContext db,
    IPatientAccessService patientAccess,
    ICurrentUserService currentUser) : ControllerBase
{
    private static readonly HashSet<string> PaRequiredLandmarkKeys =
        ["Cg", "ZR", "ZL", "ANS", "JR", "JL", "AgR", "AgL", "Me", "UDM", "LDM", "U6R", "U6L", "L6R", "L6L"];
    private static readonly HashSet<string> CohortAnalysisTypes =
        ["steiner", "tweed", "mcnamara", "ricketts", "downs", "jarabak", "wits", "pa", "full"];

    private static bool IsPaReadyForApproval(CephAnalysisDetailDto detail) =>
        !string.Equals(detail.AnalysisType, "pa", StringComparison.OrdinalIgnoreCase)
        || (detail.PixelsPerMm is > 0
            && PaRequiredLandmarkKeys.IsSubsetOf(detail.Landmarks.Select(x => x.Key))
            && detail.Measurements.Count > 0);
    // GET /api/ceph                          — all analyses
    // GET /api/ceph?orthoCaseId={id}         — filtered by ortho case
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? orthoCaseId)
    {
        if (orthoCaseId.HasValue)
        {
            var accessError = await GetCaseAccessErrorAsync(orthoCaseId.Value);
            if (accessError is not null) return accessError;
        }

        var accessiblePatientIds = await patientAccess.GetAccessiblePatientIdsAsync();
        var result = await service.ListAsync(orthoCaseId, accessiblePatientIds);
        return Ok(result);
    }

    // Aggregate-only cohort analytics. The service uses one latest approved,
    // doctor-reviewed record per accessible patient and suppresses cohorts < 5.
    [HttpGet("cohort")]
    public async Task<IActionResult> Cohort(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? analysisType,
        [FromQuery] string? tag)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest(new { message = "تاريخ البداية يجب أن يسبق تاريخ النهاية" });

        analysisType = analysisType?.Trim().ToLowerInvariant();
        if (analysisType == "all") analysisType = null;
        if (analysisType is not null && !CohortAnalysisTypes.Contains(analysisType))
            return BadRequest(new { message = "نوع تحليل السيفالو غير صالح" });

        tag = tag?.Trim();
        if (string.IsNullOrWhiteSpace(tag)) tag = null;
        if (tag is not null && !CephClinicalTagCatalog.IsKnownKey(tag))
            return BadRequest(new { message = "وسم السيفالو غير صالح" });

        var accessiblePatientIds = await patientAccess.GetAccessiblePatientIdsAsync();
        var result = await service.BuildCohortAsync(
            accessiblePatientIds,
            from,
            to,
            analysisType,
            tag);
        return Ok(result);
    }

    // Aggregate validation evidence from doctor-corrected AI points. This is
    // deliberately labelled as a local QA indicator, never as a regulatory or
    // autonomous-diagnosis claim.
    [HttpGet("ai/accuracy")]
    public async Task<IActionResult> AiAccuracy()
    {
        var accessiblePatientIds = await patientAccess.GetAccessiblePatientIdsAsync();
        return Ok(await service.GetAiAccuracyAsync(accessiblePatientIds));
    }

    // Read-only connector readiness. Never returns the partner key itself.
    // WebCeph only releases the full integration contract after a partner
    // agreement, so transfer execution remains disabled until that contract
    // and a server-side key are configured.
    [HttpGet("webceph-migration/status")]
    public IActionResult WebCephMigrationStatus([FromServices] IConfiguration configuration)
    {
        var configured = !string.IsNullOrWhiteSpace(configuration["WEBCEPH_PARTNER_API_KEY"]);
        return Ok(new
        {
            configured,
            partnerAgreementRequired = true,
            premiumRequired = true,
            requestLimitPerMinute = 30,
            apiTransferable = new[] { "patients", "records", "images" },
            exportRequired = new[] { "landmarks", "analysis-results", "clinical-diagnostic-data" },
            notice = configured
                ? "مفتاح الشريك مضبوط على الخادم. يجب تثبيت عقد نقاط النهاية الرسمي قبل تشغيل نقل بيانات المرضى."
                : "يلزم اتفاق Partner API ومفتاح خادمي من WebCeph لتفعيل نقل المرضى والسجلات والصور."
        });
    }

    // GET /api/ceph/compare?baseId=&targetId= — C-B pre/post comparison
    // (declared before {id:guid} so "compare" never binds as an id)
    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] Guid baseId, [FromQuery] Guid targetId)
    {
        if (baseId == Guid.Empty || targetId == Guid.Empty || baseId == targetId)
            return BadRequest(new { message = "حدد تحليلين مختلفين للمقارنة" });

        var baseAccessError = await GetAnalysisAccessErrorAsync(baseId);
        if (baseAccessError is not null) return baseAccessError;
        var targetAccessError = await GetAnalysisAccessErrorAsync(targetId);
        if (targetAccessError is not null) return targetAccessError;

        var (result, error) = await service.CompareAsync(baseId, targetId);
        if (error == "التحليل غير موجود") return NotFound(new { message = error });
        if (error is not null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // GET /api/ceph/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var result = await service.GetByIdAsync(id);
        return result is null
            ? NotFound(new { message = "تحليل السيفالومتري غير موجود" })
            : Ok(result);
    }

    [HttpGet("{id:guid}/ai/inference-runs")]
    public async Task<IActionResult> GetInferenceRuns(
        Guid id,
        [FromServices] CephAiModelRegistryService registry,
        CancellationToken cancellationToken)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        return Ok(await registry.GetInferenceRunsAsync(id, cancellationToken));
    }

    // POST /api/ceph
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCephAnalysisRequest req)
    {
        var accessError = await GetCaseAccessErrorAsync(req.OrthoCaseId);
        if (accessError is not null) return accessError;

        var result = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // POST /api/ceph/{id}/landmarks  — saves, computes, returns full detail
    [HttpPost("{id:guid}/landmarks")]
    public async Task<IActionResult> SaveLandmarks(Guid id, [FromBody] SaveLandmarksRequest req)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        bool ok;
        try
        {
            ok = await service.SaveLandmarksAsync(id, req);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "ceph.invalid-landmark-lineage", message = ex.Message });
        }
        if (!ok) return NotFound(new { message = "تحليل السيفالومتري غير موجود" });
        var detail = await service.GetByIdAsync(id);
        return Ok(detail);
    }

    // WebCeph's official API does not expose landmark or analysis data. The
    // account-owned Landmark Table export is therefore the supported clinical
    // migration path. The current .xls download is a UTF-8 HTML table fragment.
    [HttpPost("{id:guid}/imports/webceph-landmarks")]
    [RequestSizeLimit(WebCephLandmarkImportParser.MaxFileBytes)]
    public async Task<IActionResult> ImportWebCephLandmarks(
        Guid id,
        IFormFile? file,
        [FromForm] WebCephLandmarkImportOptions options)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        if (file is null || file.Length <= 0)
            return BadRequest(new { message = "اختر ملف Landmark Table الذي صدرته من WebCeph." });
        if (file.Length > WebCephLandmarkImportParser.MaxFileBytes)
            return BadRequest(new { message = "ملف نقاط WebCeph يتجاوز الحد المسموح (1 ميجابايت)." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xls" or ".html" or ".htm"))
            return BadRequest(new { message = "صيغة الملف غير مدعومة. استخدم ملف Landmark Table بصيغة XLS من WebCeph." });

        try
        {
            await using var stream = file.OpenReadStream();
            var summary = await service.ImportWebCephLandmarksAsync(id, stream, options);
            if (summary is null)
                return NotFound(new { message = "تحليل السيفالومتري غير موجود." });

            var detail = await service.GetByIdAsync(id);
            return Ok(new { analysis = detail, summary });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  CEPH-EPIC batch C-B — analysis VERSIONS (named snapshots)
    //  POST   /api/ceph/{id}/versions              → save current state as a snapshot
    //  GET    /api/ceph/{id}/versions              → list snapshots (id, label, date)
    //  GET    /api/ceph/{id}/versions/{versionId}  → full snapshot for compare
    //
    //  PatientAccessFilter is enforced via GetAnalysisAccessErrorAsync (the
    //  ceph analysis belongs to a patient — doctors see only their own).
    //  The AuditLogMiddleware auto-logs every POST/GET-on-sensitive-endpoint.
    // ──────────────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/versions")]
    public async Task<IActionResult> SaveVersion(Guid id, [FromBody] CreateCephVersionRequest req)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        try
        {
            var version = await service.SaveVersionAsync(id, req);
            return version is null
                ? NotFound(new { message = "تحليل السيفالومتري غير موجود" })
                : CreatedAtAction(nameof(GetVersion), new { id, versionId = version.Id }, version);
        }
        catch (ArgumentException ex)
        {
            // Label validation: empty/whitespace label → 400 Arabic.
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<IActionResult> ListVersions(Guid id)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var versions = await service.ListVersionsAsync(id);
        return Ok(versions);
    }

    [HttpGet("{id:guid}/versions/{versionId:guid}")]
    public async Task<IActionResult> GetVersion(Guid id, Guid versionId)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var version = await service.GetVersionAsync(id, versionId);
        return version is null
            ? NotFound(new { message = "النسخة غير موجودة" })
            : Ok(version);
    }

    // POST /api/ceph/{id}/simulate
    // Template-based landmark simulation — NOT AI (honest labeling, see
    // CephService.SimulateTemplateAsync). Disabled by default; enable via the
    // Settings key "ceph.simulation_enabled".
    [HttpPost("{id:guid}/simulate")]
    public async Task<IActionResult> SimulateTemplate(Guid id, [FromBody] AiSimulateRequest req)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        if (!await service.IsSimulationEnabledAsync())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "المحاكاة التجريبية معطلة من الإعدادات" });

        var result = await service.SimulateTemplateAsync(id, req);
        return Ok(result);
    }

    // POST /api/ceph/{id}/ai/auto-trace
    // Real Gemini image-understanding draft. The result is deliberately
    // UNSAVED and must be reviewed/adjusted by an orthodontist.
    [HttpPost("{id:guid}/ai/auto-trace")]
    public async Task<IActionResult> AutoTrace(
        Guid id,
        [FromBody] CephAiTraceRequest request,
        [FromServices] AqlanDentalPro.Infrastructure.Services.CephAiLandmarkDraftService landmarkService,
        [FromServices] ILogger<CephController> logger)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        // Map the optional precision string → enum. Unknown / null = draft.
        var precision = string.Equals(request?.Precision, "high", StringComparison.OrdinalIgnoreCase)
            ? AqlanDentalPro.Application.Interfaces.Services.CephAiPrecision.High
            : AqlanDentalPro.Application.Interfaces.Services.CephAiPrecision.Draft;

        try
        {
            var result = await landmarkService.GenerateAsync(
                id, request.ImageWidth, request.ImageHeight, precision, HttpContext.RequestAborted);
            return result is null
                ? NotFound(new { message = "تحليل السيفالومتري غير موجود" })
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiLimitReachedException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiUpstreamException ex)
        {
            logger.LogError(ex, "AI landmark draft upstream failure for analysis {AnalysisId}", id);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "تعذر توليد مسودة النقاط من خدمة الذكاء الاصطناعي — حاول لاحقاً" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected AI landmark draft failure for analysis {AnalysisId}", id);
            return StatusCode(500, new { message = "حدث خطأ أثناء توليد مسودة نقاط السيفالومتري" });
        }
    }

    // POST /api/ceph/{id}/ai/refine-landmark
    // Re-evaluate the position of a single AI-placed landmark. The orthodontist
    // right-clicks a point on the canvas and asks the model to reconsider. The
    // result is UNSAVED — the user reviews and saves it manually.
    [HttpPost("{id:guid}/ai/refine-landmark")]
    public async Task<IActionResult> RefineLandmark(
        Guid id,
        [FromBody] CephAiRefineLandmarkRequest request,
        [FromServices] AqlanDentalPro.Infrastructure.Services.CephAiLandmarkDraftService landmarkService,
        [FromServices] ILogger<CephController> logger)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        if (string.IsNullOrWhiteSpace(request?.LandmarkKey))
            return BadRequest(new { message = "يجب تحديد النقطة المراد تحسينها" });
        if (request.ImageWidth <= 0 || request.ImageHeight <= 0)
            return BadRequest(new { message = "أبعاد صورة الأشعة غير صالحة" });

        try
        {
            var result = await landmarkService.RefineLandmarkAsync(
                id, request.LandmarkKey, request.ImageWidth, request.ImageHeight,
                request.CurrentX, request.CurrentY, HttpContext.RequestAborted);
            return result is null
                ? NotFound(new { message = "تحليل السيفالومتري غير موجود" })
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiLimitReachedException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (AqlanDentalPro.Application.Exceptions.CephAiUpstreamException ex)
        {
            logger.LogError(ex, "AI landmark refine upstream failure for analysis {AnalysisId} key {Key}",
                id, request.LandmarkKey);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "تعذر تحسين موضع النقطة من خدمة الذكاء الاصطناعي — حاول لاحقاً" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected AI landmark refine failure for analysis {AnalysisId} key {Key}",
                id, request.LandmarkKey);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحسين موضع النقطة" });
        }
    }

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
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

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

    // POST /api/ceph/{id}/approve — clinical approval gate (CEPH-EPIC).
    // Only the doctor ASSIGNED to the analysis's patient OR an Admin may approve.
    // GetAnalysisAccessErrorAsync already rejects a doctor who is not assigned
    // (and 404s a missing analysis); the extra IsDoctor/IsAdmin guard rejects
    // non-doctor staff (e.g. reception) who otherwise have full patient access.
    // Sets IsApproved=true so the final PDF report can be issued. Idempotent.
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCephAnalysisRequest? req)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        if (!currentUser.IsAdmin && !patientAccess.IsDoctor)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "لا يُسمح إلا للطبيب المعالج أو المدير باعتماد التحليل" });

        var analysis = await db.CephAnalyses
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (analysis is null)
            return NotFound(new { message = "تحليل السيفالومتري غير موجود" });

        var unreviewedExternalCount = await CountUnreviewedExternalLandmarksAsync(id);
        if (unreviewedExternalCount > 0)
            return BadRequest(new
            {
                message = $"لا يمكن اعتماد التحليل قبل مراجعة جميع نقاط الذكاء الاصطناعي أو WebCeph. المتبقي: {unreviewedExternalCount} نقطة."
            });

        if (analysis.IsApproved)
        {
            var detailAlready = await service.GetByIdAsync(id);
            return Ok(new { message = "التحليل معتمد مسبقاً", analysis = detailAlready });
        }

        var approvalDetail = await service.GetByIdAsync(id);
        if (approvalDetail is null || !IsPaReadyForApproval(approvalDetail))
            return BadRequest(new { message = "لا يمكن اعتماد تحليل PA قبل حفظ المعايرة وإكمال النقاط الـ15 وحساب القياسات" });

        analysis.IsApproved = true;
        analysis.ApprovedByUserId = currentUser.UserId;
        analysis.ApprovedAt = DateTime.UtcNow;
        analysis.ApprovalNotes = string.IsNullOrWhiteSpace(req?.Notes) ? null : req!.Notes!.Trim();
        await db.SaveChangesAsync();

        var detail = await service.GetByIdAsync(id);
        return Ok(new { message = "تم اعتماد التحليل بنجاح", analysis = detail });
    }

    // GET /api/ceph/{id}/report/pdf — C-C Arabic cephalometric PDF report
    [HttpGet("{id:guid}/report/pdf")]
    public async Task<IActionResult> GetReportPdf(
        Guid id,
        [FromServices] CephReportPdfGenerator reportGenerator,
        [FromServices] ILogger<CephController> logger)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        // Clinical approval gate: the final report cannot be issued until an
        // authorized doctor/admin approves the analysis (CEPH-EPIC).
        var reportDetail = await service.GetByIdAsync(id);
        if (reportDetail?.IsApproved != true)
            return BadRequest(new { message = "لا يمكن إصدار التقرير النهائي قبل اعتماد الطبيب للتحليل" });
        var unreviewedExternalCount = await CountUnreviewedExternalLandmarksAsync(id);
        if (unreviewedExternalCount > 0)
            return BadRequest(new
            {
                message = $"لا يمكن إصدار التقرير قبل مراجعة نقاط الذكاء الاصطناعي أو WebCeph المتبقية ({unreviewedExternalCount})."
            });
        if (!IsPaReadyForApproval(reportDetail))
            return BadRequest(new { message = "لا يمكن إصدار تقرير PA قبل حفظ المعايرة وإكمال النقاط الـ15 وحساب القياسات" });

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
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var ok = await service.SaveDiagnosisAsync(id, req);
        return ok
            ? Ok(new { message = "تم حفظ التشخيص بنجاح" })
            : NotFound(new { message = "تحليل السيفالومتري غير موجود" });
    }

    // POST /api/ceph/{id}/assessment/problem-list
    [HttpPost("{id:guid}/assessment/problem-list")]
    public async Task<IActionResult> ImportAssessmentProblems(
        Guid id,
        [FromBody] ImportCephAssessmentProblemsRequest req)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        if (!currentUser.IsAdmin && !patientAccess.IsDoctor)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "لا يُسمح إلا للطبيب المعالج أو المدير بنقل التقييم إلى قائمة المشكلات" });

        var result = await service.ImportAssessmentProblemsAsync(id, req);
        return result.Status switch
        {
            "not_found" => NotFound(new { message = "تحليل السيفالومتري غير موجود" }),
            "analysis_not_approved" => BadRequest(new { message = "اعتمد التحليل أولاً قبل نقل التقييم" }),
            "diagnosis_not_approved" => BadRequest(new { message = "يجب أن يوافق الطبيب على التشخيص قبل نقل التقييم" }),
            _ => Ok(result),
        };
    }

    [HttpGet("{id:guid}/vto-scenarios")]
    public async Task<IActionResult> GetVtoScenarios(Guid id)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;
        return Ok(new
        {
            data = await service.ListVtoScenariosAsync(id),
            disclaimer = CephService.VtoDisclaimerAr,
        });
    }

    [HttpPost("{id:guid}/vto-scenarios")]
    public async Task<IActionResult> SaveVtoScenario(
        Guid id,
        [FromBody] SaveCephVtoScenarioRequest req)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;
        if (!currentUser.IsAdmin && !patientAccess.IsDoctor)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "لا يُسمح إلا للطبيب المعالج أو المدير بحفظ سيناريو VTO" });

        var result = await service.SaveVtoScenarioAsync(id, req);
        return result.Status switch
        {
            "not_found" => NotFound(new { message = "تحليل السيفالومتري غير موجود" }),
            "analysis_not_approved" => BadRequest(new { message = "اعتمد التحليل قبل حفظ سيناريو VTO" }),
            "calibration_missing" => BadRequest(new { message = "معايرة الصورة المحفوظة مطلوبة لحفظ سيناريو VTO" }),
            "incisors_missing" => BadRequest(new { message = "معالم القواطع الأربعة المحفوظة مطلوبة لحفظ سيناريو VTO" }),
            "scenario_not_found" => NotFound(new { message = "مجموعة سيناريو VTO غير موجودة" }),
            "version_conflict" => Conflict(new { message = "حُفظ إصدار آخر بالتزامن؛ أعد المحاولة لإنشاء الإصدار التالي" }),
            _ => Ok(result.Scenario),
        };
    }

    // DELETE /api/ceph/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var accessError = await GetAnalysisAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var analysis = await service.GetByIdAsync(id);
        if (analysis is null)
            return NotFound(new { message = "تحليل السيفالومتري غير موجود" });

        await service.SoftDeleteAsync(id);
        return Ok(new { message = "تم حذف التحليل بنجاح" });
    }

    private async Task<IActionResult?> GetCaseAccessErrorAsync(Guid orthoCaseId)
    {
        var patientId = await db.OrthoCases
            .AsNoTracking()
            .Where(x => x.Id == orthoCaseId && x.IsActive)
            .Select(x => (Guid?)x.PatientId)
            .FirstOrDefaultAsync();

        if (!patientId.HasValue)
            return NotFound(new { message = "حالة التقويم غير موجودة" });

        return await patientAccess.CanAccessPatientAsync(patientId.Value)
            ? null
            : Forbid();
    }

    private Task<int> CountUnreviewedExternalLandmarksAsync(Guid analysisId) =>
        db.CephLandmarks.CountAsync(item =>
            item.AnalysisId == analysisId
            && item.IsActive
            && !item.IsReviewed
            && (item.PlacementSource == "ai" || item.PlacementSource == "webceph-import"));

    private async Task<IActionResult?> GetAnalysisAccessErrorAsync(Guid analysisId)
    {
        var patientId = await db.CephAnalyses
            .AsNoTracking()
            .Where(x => x.Id == analysisId && x.IsActive)
            .Select(x => (Guid?)x.OrthoCase.PatientId)
            .FirstOrDefaultAsync();

        if (!patientId.HasValue)
            return NotFound(new { message = "تحليل السيفالومتري غير موجود" });

        return await patientAccess.CanAccessPatientAsync(patientId.Value)
            ? null
            : Forbid();
    }
}
