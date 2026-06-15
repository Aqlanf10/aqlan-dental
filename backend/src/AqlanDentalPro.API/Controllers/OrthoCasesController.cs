using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

public sealed class UpsertClinicalExamRequest
{
    public string? ExamDate { get; init; }
    public string? FacialSymmetry { get; init; }
    public string? Profile { get; init; }
    public bool? LipsCompetence { get; init; }
    public string? SmileLine { get; init; }
    public string? VerticalProportion { get; init; }
    public string? MolarRelation { get; init; }
    public string? CanineRelation { get; init; }
    public decimal? Overjet { get; init; }
    public decimal? Overbite { get; init; }
    public bool Crossbite { get; init; }
    public bool OpenBite { get; init; }
    public string? UpperCrowding { get; init; }
    public string? LowerCrowding { get; init; }
    public decimal? UpperSpacing { get; init; }
    public string? MidlineUpper { get; init; }
    public string? MidlineLower { get; init; }
    public bool? CoCrDiscrepancy { get; init; }
    public string? TmjFindings { get; init; }
    public string? Habits { get; init; }
    public string? Notes { get; init; }
    public Guid? DoctorId { get; init; }

    // ── Phase 3 — structured clinical examination (all optional / additive) ──

    // Occlusal: right/left split + missing measures
    public string? MolarRelationRight { get; init; }
    public string? MolarRelationLeft { get; init; }
    public string? CanineRelationRight { get; init; }
    public string? CanineRelationLeft { get; init; }
    public string? IncisorRelation { get; init; }
    public decimal? OverbitePercent { get; init; }
    public bool? DeepBite { get; init; }
    public string? CrossbiteType { get; init; }
    public bool? ScissorBite { get; init; }
    public decimal? MidlineUpperShiftMm { get; init; }
    public decimal? MidlineLowerShiftMm { get; init; }
    public decimal? UpperCrowdingMm { get; init; }
    public decimal? LowerCrowdingMm { get; init; }
    public decimal? LowerSpacingMm { get; init; }
    public string? CurveOfSpee { get; init; }
    public string? ArchFormUpper { get; init; }
    public string? ArchFormLower { get; init; }
    public string? BoltonDiscrepancyNote { get; init; }

    // Extraoral additions
    public string? LipCompetenceGrade { get; init; }
    public string? NasolabialAngle { get; init; }
    public string? ChinPosition { get; init; }
    public string? FunctionalShift { get; init; }
    public bool? GummySmile { get; init; }

    // Structured habit flags
    public bool? ThumbSucking { get; init; }
    public bool? MouthBreathing { get; init; }
    public bool? TongueThrust { get; init; }
    public bool? LipBiting { get; init; }
    public bool? NailBiting { get; init; }
    public bool? Bruxism { get; init; }

    // Intraoral health
    public string? OralHygiene { get; init; }
    public string? GingivalCondition { get; init; }
    public string? PeriodontalConcerns { get; init; }
    public string? MissingTeethFdi { get; init; }
    public string? RetainedDeciduousFdi { get; init; }
    public string? ImpactedTeethFdi { get; init; }
    public string? SupernumeraryNote { get; init; }
    public string? EctopicEruptionNote { get; init; }
    public string? FrenumNote { get; init; }
    public string? TongueNote { get; init; }
    public string? CariesNote { get; init; }
}

[ApiController]
[Route("api/ortho-cases")]
[Authorize(Policy = "OrthoAccess")]
public class OrthoCasesController(
    OrthoService service,
    AppDbContext db,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess) : ControllerBase
{
    private const int DefaultOrthoFollowUpIntervalDays = 21;
    private static readonly HashSet<string> ImagePreparationStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "PreparedForReport",
        "SelectedForPresentation",
        "ApprovedForPresentation",
    };

    private static readonly HashSet<string> ImageAspectRatios = new(StringComparer.OrdinalIgnoreCase)
    {
        "Original", "4:5", "3:4", "16:9", "2:1", "4:3", "1:1",
    };

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

    // GET /api/ortho-cases/{id}/lab-orders — read-only lab orders linked to this case
    // (Sprint 1: surfaces existing LabOrder.OrthoCaseId; the Lab module remains the
    // source of truth — no duplication). Enforces per-patient access.
    [HttpGet("{id:guid}/lab-orders")]
    public async Task<IActionResult> GetLabOrders(Guid id)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var orders = await db.LabOrders
            .AsNoTracking()
            .Where(o => o.OrthoCaseId == id && o.IsActive)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new
            {
                id = o.Id,
                orderNumber = o.OrderNumber,
                applianceType = o.ApplianceType,
                status = o.Status,
                priority = o.Priority,
                labName = o.LabName,
                totalCost = o.TotalCost,
                sentDate = o.SentDate,
                expectedDate = o.ExpectedDate,
                receivedDate = o.ReceivedDate,
                deliveredDate = o.DeliveredDate,
            })
            .ToListAsync();

        return Ok(orders);
    }

    // Same per-patient guard pattern as CephController.
    private async Task<IActionResult?> GetCaseAccessErrorAsync(Guid orthoCaseId)
    {
        var patientId = await db.OrthoCases
            .AsNoTracking()
            .Where(c => c.Id == orthoCaseId && c.IsActive)
            .Select(c => (Guid?)c.PatientId)
            .FirstOrDefaultAsync();

        if (!patientId.HasValue)
            return NotFound(new { message = "الحالة التقويمية غير موجودة" });

        return await patientAccess.CanAccessPatientAsync(patientId.Value) ? null : Forbid();
    }

    private static object MapImagePreparation(
        OrthoClinicalPhoto photo,
        OrthoImagePreparation? preparation) => new
    {
        photoId = photo.Id,
        originalPhotoUrl = photo.PhotoUrl,
        preparedImageUrl = preparation?.PreparedImageUrl,
        cropX = preparation?.CropX ?? 0m,
        cropY = preparation?.CropY ?? 0m,
        cropWidth = preparation?.CropWidth ?? 1m,
        cropHeight = preparation?.CropHeight ?? 1m,
        zoom = preparation?.Zoom ?? 1m,
        rotationDegrees = preparation?.RotationDegrees ?? 0,
        brightness = preparation?.Brightness ?? 0,
        contrast = preparation?.Contrast ?? 0,
        flipHorizontal = preparation?.FlipHorizontal ?? false,
        flipVertical = preparation?.FlipVertical ?? false,
        aspectRatio = preparation?.AspectRatio ?? "Original",
        preset = preparation?.Preset,
        status = preparation?.Status ?? "OriginalUploaded",
        preparedAt = preparation?.PreparedAt,
        approvedAt = preparation?.ApprovedAt,
    };

    // GET /api/ortho-cases/{id}/case-summary/report/pdf — unified Arabic case summary PDF.
    // Aggregates existing data only (no new computation). Same OrthoAccess policy +
    // per-patient access guard as the rest of the controller.
    [HttpGet("{id:guid}/case-summary/report/pdf")]
    public async Task<IActionResult> GetCaseSummaryPdf(
        Guid id,
        [FromServices] AqlanDentalPro.API.Services.OrthoCaseSummaryReportPdfGenerator generator,
        [FromServices] ILogger<OrthoCasesController> logger)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        try
        {
            var pdf = await generator.GenerateAsync(id);
            return File(pdf, "application/pdf", $"case-summary-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "الحالة التقويمية غير موجودة" });
        }
        catch (Exception ex)
        {
            // Never expose exception details in the HTTP response — log only.
            logger.LogError(ex, "Failed to generate case summary PDF for case {CaseId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء ملخّص الحالة" });
        }
    }

    [HttpGet("{id:guid}/case-presentation/definition")]
    public async Task<IActionResult> GetCasePresentationDefinition(
        Guid id,
        [FromServices] AqlanDentalPro.API.Services.OrthoCasePresentationService generator)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        try
        {
            return Ok(await generator.GetDefinitionAsync(id));
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "حالة التقويم غير موجودة" });
        }
    }

    [HttpPost("{id:guid}/case-presentation/pptx")]
    public async Task<IActionResult> GenerateCasePresentation(
        Guid id,
        [FromBody] AqlanDentalPro.API.Services.GenerateOrthoCasePresentationRequest? request,
        [FromServices] AqlanDentalPro.API.Services.OrthoCasePresentationService generator,
        [FromServices] ILogger<OrthoCasesController> logger)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        try
        {
            var presentation = await generator.GenerateAsync(
                id,
                request ?? new AqlanDentalPro.API.Services.GenerateOrthoCasePresentationRequest(),
                HttpContext.RequestAborted);
            return File(
                presentation,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                $"ortho-case-{id}.pptx");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "حالة التقويم غير موجودة" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate orthodontic case presentation for case {CaseId}", id);
            return StatusCode(500, new { message = "تعذر إنشاء عرض الحالة حالياً" });
        }
    }

    [HttpGet("{id:guid}/overview")]
    public async Task<IActionResult> GetOverview(Guid id)
    {
        var orthoCase = await db.OrthoCases
            .AsNoTracking()
            .Include(c => c.TreatmentPlans)
            .Include(c => c.Stages)
            .Include(c => c.Visits)
            .Include(c => c.OrthoClinicalPhotos)
            .Include(c => c.CephAnalyses)
            .Include(c => c.PhotoAnalyses)
            .Include(c => c.RetentionRecord)
            .Include(c => c.RecordsChecklist)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var hasClinicalExam = await db.OrthoClinicalExams.AnyAsync(e => e.OrthoCaseId == id);
        var problemsCount = await db.ProblemListItems.CountAsync(p => p.OrthoCaseId == id);
        var diagnosisSummary = await db.OrthoDiagnoses
            .Where(d => d.OrthoCaseId == id)
            .Select(d => new
            {
                d.ApprovedAt,
                d.CephSourceAnalysisId,
                d.CephSyncedAt,
                d.ProfileSourceAnalysisId,
                d.FrontalSourceAnalysisId,
                d.PhotoAnalysisSyncedAt,
            })
            .FirstOrDefaultAsync();
        var hasDiagnosis = diagnosisSummary is not null;
        var isDiagnosisApproved = diagnosisSummary?.ApprovedAt is not null;
        var latestCeph = orthoCase.CephAnalyses
            .OrderByDescending(a => a.AnalysisDate)
            .ThenByDescending(a => a.UpdatedAt)
            .FirstOrDefault();
        var latestProfile = orthoCase.PhotoAnalyses
            .Where(a => a.ViewType == "profile")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        var latestFrontal = orthoCase.PhotoAnalyses
            .Where(a => a.ViewType == "frontal")
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        var latestPlan = orthoCase.TreatmentPlans.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var latestVisit = orthoCase.Visits.OrderByDescending(v => v.VisitDate).FirstOrDefault();

        // Contract selection: prefer contract explicitly linked to this ortho case.
        // Fallback: unlinked legacy ortho contracts (RelatedCaseId == null).
        // Never select a contract linked to a different ortho case.
        var contract = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == orthoCase.PatientId &&
                c.RelatedCaseId == id)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (contract is null)
        {
            // Legacy fallback: orthodontics contracts with no case link (pre-dating RelatedCaseId)
            var unlinkedOrthoContracts = await db.Contracts
                .Include(c => c.Payments)
                .Where(c => c.PatientId == orthoCase.PatientId &&
                    c.RelatedCaseId == null &&
                    (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // If multiple ambiguous unlinked contracts exist, do not silently pick the wrong one
            contract = unlinkedOrthoContracts.Count == 1 ? unlinkedOrthoContracts[0] : null;
        }

        decimal? contractTotal = null;
        decimal? contractPaid = null;
        decimal? contractRemaining = null;
        if (contract is not null)
        {
            contractTotal = contract.TotalAmount - contract.DiscountAmount;
            // FIX: Only count active payments — exclude soft-deleted/refunded payments
            contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            contractRemaining = Math.Max(0, contractTotal.Value - contractPaid.Value);
        }

        // Records checklist completion
        var checklist = orthoCase.RecordsChecklist;
        int checklistCompleted = 0;
        int checklistTotal = 14;
        if (checklist is not null)
        {
            checklistCompleted = new[]
            {
                checklist.ExtraoralFrontal, checklist.ExtraoralProfile, checklist.ExtraoralSmile,
                checklist.IntraoralFrontal, checklist.IntraoralRight, checklist.IntraoralLeft,
                checklist.UpperOcclusal, checklist.LowerOcclusal,
                checklist.Opg, checklist.LateralCeph, checklist.Cbct,
                checklist.StudyModels, checklist.Consent, checklist.Contract,
            }.Count(b => b);
        }
        // Auto-derive checklist items from existing data using same per-item logic as GET /checklist
        if (checklist is null)
        {
            var photos = orthoCase.OrthoClinicalPhotos.ToList();
            var hasCeph = orthoCase.CephAnalyses.Count > 0;
            var hasModel = await db.ModelAnalyses.AnyAsync(m => m.OrthoCaseId == id);
            // Use precise per-item matching — same as GET /checklist derivation:
            // (Category, Subtype) tags first, legacy caption keywords as fallback
            checklistCompleted = new[]
            {
                OrthoPhotoRecords.DeriveExtraoralFrontal(photos),
                OrthoPhotoRecords.DeriveExtraoralProfile(photos),
                OrthoPhotoRecords.DeriveExtraoralSmile(photos),
                OrthoPhotoRecords.DeriveIntraoralFrontal(photos),
                OrthoPhotoRecords.DeriveIntraoralRight(photos),
                OrthoPhotoRecords.DeriveIntraoralLeft(photos),
                OrthoPhotoRecords.DeriveUpperOcclusal(photos),
                OrthoPhotoRecords.DeriveLowerOcclusal(photos),
                OrthoPhotoRecords.DeriveOpg(photos),
                hasCeph || OrthoPhotoRecords.DeriveLateralCeph(photos),
                OrthoPhotoRecords.DeriveCbct(photos),
                hasModel,
                false, // Consent — cannot auto-derive
                contract is not null,
            }.Count(b => b);
        }

        return Ok(new
        {
            HasClinicalExam = hasClinicalExam,
            ProblemsCount = problemsCount,
            HasDiagnosis = hasDiagnosis,
            IsDiagnosisApproved = isDiagnosisApproved,
            HasTreatmentPlan = latestPlan is not null,
            IsTreatmentPlanApproved = latestPlan?.IsApproved ?? false,
            TreatmentPlansCount = orthoCase.TreatmentPlans.Count,
            CompletedStages = orthoCase.Stages.Count(s => s.Status == "completed"),
            TotalStages = orthoCase.Stages.Count,
            VisitsCount = orthoCase.Visits.Count,
            PhotosCount = orthoCase.OrthoClinicalPhotos.Count,
            CephAnalysesCount = orthoCase.CephAnalyses.Count,
            PhotoAnalysesCount = orthoCase.PhotoAnalyses.Count,
            LatestCephAnalysisId = latestCeph?.Id,
            LatestCephAnalysisDate = latestCeph?.AnalysisDate.ToString("yyyy-MM-dd"),
            CephDiagnosisSyncedAt = diagnosisSummary?.CephSyncedAt,
            IsCephDiagnosisOutdated = latestCeph is not null &&
                diagnosisSummary?.CephSourceAnalysisId != latestCeph.Id,
            LatestProfileAnalysisId = latestProfile?.Id,
            LatestProfileAnalysisDate = latestProfile?.CreatedAt.ToString("yyyy-MM-dd"),
            LatestFrontalAnalysisId = latestFrontal?.Id,
            LatestFrontalAnalysisDate = latestFrontal?.CreatedAt.ToString("yyyy-MM-dd"),
            PhotoAnalysisSyncedAt = diagnosisSummary?.PhotoAnalysisSyncedAt,
            IsPhotoAnalysisSyncOutdated =
                (latestProfile is not null && diagnosisSummary?.ProfileSourceAnalysisId != latestProfile.Id) ||
                (latestFrontal is not null && diagnosisSummary?.FrontalSourceAnalysisId != latestFrontal.Id),
            HasRetention = orthoCase.RetentionRecord is not null,
            ChecklistCompleted = checklistCompleted,
            ChecklistTotal = checklistTotal,
            ContractId = contract?.Id,
            ContractTotal = contractTotal,
            ContractPaid = contractPaid,
            ContractRemaining = contractRemaining,
            LatestVisitDate = latestVisit?.VisitDate.ToString("yyyy-MM-dd"),
            NextAppointmentDate = latestVisit?.NextAppointmentDate?.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrthoCaseRequest req)
    {
        var result = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    // ─── Visits ──────────────────────────────────────────────────────────────────

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

    [HttpPost("{id:guid}/visits/{visitId:guid}/next-appointment")]
    public async Task<IActionResult> CreateNextAppointment(
        Guid id,
        Guid visitId,
        [FromBody] CreateOrthoFollowUpAppointmentRequest req)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var orthoCase = await db.OrthoCases
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
        if (orthoCase is null)
            return NotFound(new { message = "حالة التقويم غير موجودة" });

        var visit = await db.OrthoVisits
            .FirstOrDefaultAsync(v => v.Id == visitId && v.OrthoCaseId == id && v.IsActive);
        if (visit is null)
            return NotFound(new { message = "زيارة التقويم غير موجودة" });

        var appointmentDate = visit.NextAppointmentDate
            ?? visit.VisitDate.AddDays(DefaultOrthoFollowUpIntervalDays);
        if (!string.IsNullOrWhiteSpace(req.AppointmentDate)
            && !DateOnly.TryParse(req.AppointmentDate, out appointmentDate))
            return BadRequest(new { message = "صيغة تاريخ الموعد غير صالحة" });

        var startTime = new TimeOnly(9, 0);
        if (!string.IsNullOrWhiteSpace(req.StartTime)
            && !TimeOnly.TryParse(req.StartTime, out startTime))
            return BadRequest(new { message = "صيغة وقت الموعد غير صالحة" });

        var durationMinutes = req.DurationMinutes ?? 30;
        if (durationMinutes is < 5 or > 480)
            return BadRequest(new { message = "مدة الموعد يجب أن تكون بين 5 و480 دقيقة" });

        var doctorId = req.DoctorId ?? visit.DoctorId ?? orthoCase.DoctorId;
        if (!doctorId.HasValue)
            return BadRequest(new { message = "يجب تحديد طبيب لموعد المتابعة" });

        var doctorExists = await db.Doctors
            .IgnoreQueryFilters()
            .AnyAsync(d => d.Id == doctorId.Value && d.IsActive);
        if (!doctorExists)
            return BadRequest(new { message = "الطبيب المحدد غير موجود أو غير فعال" });

        var endTime = startTime.AddMinutes(durationMinutes);
        var alreadyScheduled = await db.Appointments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.OrthoCaseId == id
                && a.PatientId == orthoCase.PatientId
                && a.AppointmentDate == appointmentDate
                && a.IsActive
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.NoShow);
        if (alreadyScheduled)
            return Conflict(new { message = "يوجد موعد متابعة تقويم مسجل في هذا التاريخ" });

        var hasConflict = await db.Appointments
            .IgnoreQueryFilters()
            .AnyAsync(a => a.DoctorId == doctorId.Value
                && a.AppointmentDate == appointmentDate
                && a.StartTime < endTime
                && a.EndTime > startTime
                && a.IsActive
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.NoShow);
        if (hasConflict)
            return Conflict(new { message = "يوجد تعارض في مواعيد الطبيب في هذا الوقت" });

        var appointment = new Appointment
        {
            PatientId = orthoCase.PatientId,
            DoctorId = doctorId.Value,
            BranchId = orthoCase.BranchId ?? currentUser.BranchId,
            OrthoCaseId = id,
            AppointmentDate = appointmentDate,
            StartTime = startTime,
            EndTime = endTime,
            DurationMinutes = durationMinutes,
            AppointmentType = string.IsNullOrWhiteSpace(req.AppointmentType)
                ? visit.NextAppointmentType ?? "OrthoFollowUp"
                : req.AppointmentType.Trim(),
            Specialty = Specialty.Orthodontics,
            ServiceId = req.ServiceId,
            Notes = req.Notes,
            CreatedBy = currentUser.UserId
        };

        visit.NextAppointmentDate = appointmentDate;
        visit.NextAppointmentType = appointment.AppointmentType;
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return Ok(new
        {
            appointment.Id,
            appointment.OrthoCaseId,
            appointment.PatientId,
            appointment.DoctorId,
            AppointmentDate = appointment.AppointmentDate.ToString("yyyy-MM-dd"),
            StartTime = appointment.StartTime.ToString("HH:mm"),
            appointment.DurationMinutes,
            appointment.AppointmentType,
            message = "تم إنشاء موعد متابعة التقويم"
        });
    }

    // ─── Stages ──────────────────────────────────────────────────────────────────

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

    // ─── Clinical Exam ───────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/clinical-exam")]
    public async Task<IActionResult> GetClinicalExam(Guid id)
    {
        var exam = await db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
        if (exam is null) return Ok(null);
        return Ok(new {
            exam.Id,
            ExamDate            = exam.ExamDate.ToString("yyyy-MM-dd"),
            exam.FacialSymmetry,
            exam.Profile,
            exam.LipsCompetence,
            exam.SmileLine,
            exam.VerticalProportion,
            exam.MolarRelation,
            exam.CanineRelation,
            exam.Overjet,
            exam.Overbite,
            exam.Crossbite,
            exam.OpenBite,
            exam.UpperCrowding,
            exam.LowerCrowding,
            exam.UpperSpacing,
            exam.MidlineUpper,
            exam.MidlineLower,
            exam.CoCrDiscrepancy,
            exam.TmjFindings,
            exam.Habits,
            exam.Notes,
            exam.DoctorId,
            // Phase 3 — structured clinical examination
            exam.MolarRelationRight,
            exam.MolarRelationLeft,
            exam.CanineRelationRight,
            exam.CanineRelationLeft,
            exam.IncisorRelation,
            exam.OverbitePercent,
            exam.DeepBite,
            exam.CrossbiteType,
            exam.ScissorBite,
            exam.MidlineUpperShiftMm,
            exam.MidlineLowerShiftMm,
            exam.UpperCrowdingMm,
            exam.LowerCrowdingMm,
            exam.LowerSpacingMm,
            exam.CurveOfSpee,
            exam.ArchFormUpper,
            exam.ArchFormLower,
            exam.BoltonDiscrepancyNote,
            exam.LipCompetenceGrade,
            exam.NasolabialAngle,
            exam.ChinPosition,
            exam.FunctionalShift,
            exam.GummySmile,
            exam.ThumbSucking,
            exam.MouthBreathing,
            exam.TongueThrust,
            exam.LipBiting,
            exam.NailBiting,
            exam.Bruxism,
            exam.OralHygiene,
            exam.GingivalCondition,
            exam.PeriodontalConcerns,
            exam.MissingTeethFdi,
            exam.RetainedDeciduousFdi,
            exam.ImpactedTeethFdi,
            exam.SupernumeraryNote,
            exam.EctopicEruptionNote,
            exam.FrenumNote,
            exam.TongueNote,
            exam.CariesNote,
        });
    }

    [HttpPut("{id:guid}/clinical-exam")]
    public async Task<IActionResult> UpsertClinicalExam(Guid id, [FromBody] UpsertClinicalExamRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        // ── Phase 3 validation: enum-like fields (case-insensitive) ─────────────
        var enumChecks = new (string Field, string? Value, IReadOnlyList<string> Allowed)[]
        {
            ("MolarRelationRight",  req.MolarRelationRight,  OrthoClinicalExamFields.AngleClasses),
            ("MolarRelationLeft",   req.MolarRelationLeft,   OrthoClinicalExamFields.AngleClasses),
            ("CanineRelationRight", req.CanineRelationRight, OrthoClinicalExamFields.AngleClasses),
            ("CanineRelationLeft",  req.CanineRelationLeft,  OrthoClinicalExamFields.AngleClasses),
            ("IncisorRelation",     req.IncisorRelation,     OrthoClinicalExamFields.IncisorRelations),
            ("CrossbiteType",       req.CrossbiteType,       OrthoClinicalExamFields.CrossbiteTypes),
            ("CurveOfSpee",         req.CurveOfSpee,         OrthoClinicalExamFields.CurveOfSpeeValues),
            ("ArchFormUpper",       req.ArchFormUpper,       OrthoClinicalExamFields.ArchForms),
            ("ArchFormLower",       req.ArchFormLower,       OrthoClinicalExamFields.ArchForms),
            ("LipCompetenceGrade",  req.LipCompetenceGrade,  OrthoClinicalExamFields.LipCompetenceGrades),
            ("NasolabialAngle",     req.NasolabialAngle,     OrthoClinicalExamFields.NasolabialAngles),
            ("ChinPosition",        req.ChinPosition,        OrthoClinicalExamFields.ChinPositions),
            ("OralHygiene",         req.OralHygiene,         OrthoClinicalExamFields.OralHygieneValues),
        };

        var normalized = new Dictionary<string, string?>();
        foreach (var (field, value, allowed) in enumChecks)
        {
            if (!OrthoClinicalExamFields.TryNormalize(value, allowed, out var canonical))
                return BadRequest(new { message = $"قيمة غير صالحة للحقل: {field}" });
            normalized[field] = canonical;
        }

        // ── Phase 3 validation: numeric sanity (mm: -30..30, percent: 0..200) ──
        var mmChecks = new (string Field, decimal? Value)[]
        {
            ("Overjet",             req.Overjet),
            ("Overbite",            req.Overbite),
            ("MidlineUpperShiftMm", req.MidlineUpperShiftMm),
            ("MidlineLowerShiftMm", req.MidlineLowerShiftMm),
            ("UpperCrowdingMm",     req.UpperCrowdingMm),
            ("LowerCrowdingMm",     req.LowerCrowdingMm),
            ("LowerSpacingMm",      req.LowerSpacingMm),
        };
        foreach (var (field, value) in mmChecks)
        {
            if (!OrthoClinicalExamFields.IsInRange(value, -30m, 30m))
                return BadRequest(new { message = $"قيمة خارج النطاق المسموح للحقل: {field}" });
        }
        if (!OrthoClinicalExamFields.IsInRange(req.OverbitePercent, 0m, 200m))
            return BadRequest(new { message = "قيمة خارج النطاق المسموح للحقل: OverbitePercent" });

        var existing = await db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            existing = new OrthoClinicalExam { OrthoCaseId = id };
            db.OrthoClinicalExams.Add(existing);
        }

        existing.ExamDate           = req.ExamDate != null ? DateOnly.Parse(req.ExamDate) : DateOnly.FromDateTime(DateTime.Today);
        existing.FacialSymmetry     = req.FacialSymmetry;
        existing.Profile            = req.Profile;
        existing.LipsCompetence     = req.LipsCompetence;
        existing.SmileLine          = req.SmileLine;
        existing.VerticalProportion = req.VerticalProportion;
        existing.MolarRelation      = req.MolarRelation;
        existing.CanineRelation     = req.CanineRelation;
        existing.Overjet            = req.Overjet;
        existing.Overbite           = req.Overbite;
        existing.Crossbite          = req.Crossbite;
        existing.OpenBite           = req.OpenBite;
        existing.UpperCrowding      = req.UpperCrowding;
        existing.LowerCrowding      = req.LowerCrowding;
        existing.UpperSpacing       = req.UpperSpacing;
        existing.MidlineUpper       = req.MidlineUpper;
        existing.MidlineLower       = req.MidlineLower;
        existing.CoCrDiscrepancy    = req.CoCrDiscrepancy;
        existing.TmjFindings        = req.TmjFindings;
        existing.Habits             = req.Habits;
        existing.Notes              = req.Notes;
        existing.DoctorId           = req.DoctorId;

        // Phase 3 — occlusal (enum-likes stored in canonical casing)
        existing.MolarRelationRight   = normalized["MolarRelationRight"];
        existing.MolarRelationLeft    = normalized["MolarRelationLeft"];
        existing.CanineRelationRight  = normalized["CanineRelationRight"];
        existing.CanineRelationLeft   = normalized["CanineRelationLeft"];
        existing.IncisorRelation      = normalized["IncisorRelation"];
        existing.OverbitePercent      = req.OverbitePercent;
        existing.DeepBite             = req.DeepBite;
        existing.CrossbiteType        = normalized["CrossbiteType"];
        existing.ScissorBite          = req.ScissorBite;
        existing.MidlineUpperShiftMm  = req.MidlineUpperShiftMm;
        existing.MidlineLowerShiftMm  = req.MidlineLowerShiftMm;
        existing.UpperCrowdingMm      = req.UpperCrowdingMm;
        existing.LowerCrowdingMm      = req.LowerCrowdingMm;
        existing.LowerSpacingMm       = req.LowerSpacingMm;
        existing.CurveOfSpee          = normalized["CurveOfSpee"];
        existing.ArchFormUpper        = normalized["ArchFormUpper"];
        existing.ArchFormLower        = normalized["ArchFormLower"];
        existing.BoltonDiscrepancyNote = req.BoltonDiscrepancyNote;

        // Phase 3 — extraoral additions
        existing.LipCompetenceGrade   = normalized["LipCompetenceGrade"];
        existing.NasolabialAngle      = normalized["NasolabialAngle"];
        existing.ChinPosition         = normalized["ChinPosition"];
        existing.FunctionalShift      = req.FunctionalShift;
        existing.GummySmile           = req.GummySmile;

        // Phase 3 — structured habit flags
        existing.ThumbSucking         = req.ThumbSucking;
        existing.MouthBreathing       = req.MouthBreathing;
        existing.TongueThrust         = req.TongueThrust;
        existing.LipBiting            = req.LipBiting;
        existing.NailBiting           = req.NailBiting;
        existing.Bruxism              = req.Bruxism;

        // Phase 3 — intraoral health
        existing.OralHygiene          = normalized["OralHygiene"];
        existing.GingivalCondition    = req.GingivalCondition;
        existing.PeriodontalConcerns  = req.PeriodontalConcerns;
        existing.MissingTeethFdi      = req.MissingTeethFdi;
        existing.RetainedDeciduousFdi = req.RetainedDeciduousFdi;
        existing.ImpactedTeethFdi     = req.ImpactedTeethFdi;
        existing.SupernumeraryNote    = req.SupernumeraryNote;
        existing.EctopicEruptionNote  = req.EctopicEruptionNote;
        existing.FrenumNote           = req.FrenumNote;
        existing.TongueNote           = req.TongueNote;
        existing.CariesNote           = req.CariesNote;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ الفحص السريري" });
    }

    // ─── Problem List ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/problem-list")]
    public async Task<IActionResult> GetProblemList(Guid id)
    {
        var items = await db.ProblemListItems
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Category, p.Description, p.Severity, p.SortOrder })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("{id:guid}/problem-list")]
    public async Task<IActionResult> AddProblemItem(Guid id, [FromBody] AddProblemItemRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var maxOrder = await db.ProblemListItems.Where(p => p.OrthoCaseId == id).MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var item = new ProblemListItem
        {
            OrthoCaseId = id,
            Category    = req.Category,
            Description = req.Description,
            Severity    = req.Severity,
            SortOrder   = maxOrder + 1,
        };
        db.ProblemListItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.Category, item.Description, item.Severity, item.SortOrder });
    }

    [HttpDelete("{id:guid}/problem-list/{itemId:guid}")]
    public async Task<IActionResult> DeleteProblemItem(Guid id, Guid itemId)
    {
        var item = await db.ProblemListItems.FirstOrDefaultAsync(p => p.Id == itemId && p.OrthoCaseId == id);
        if (item is null) return NotFound();
        db.ProblemListItems.Remove(item);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم الحذف" });
    }

    // ─── Treatment Plans (multiple: Plan A/B/C) ──────────────────────────────────

    [HttpGet("{id:guid}/treatment-plans")]
    public async Task<IActionResult> GetTreatmentPlans(Guid id)
    {
        var plans = await db.TreatmentPlans
            .Include(p => p.ApprovedByDoctor)
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.PlanLabel)
            .Select(p => new
            {
                p.Id,
                p.PlanVersion,
                p.PlanLabel,
                p.IsApproved,
                p.ApplianceType,
                p.BracketSystem,
                p.InitialWire,
                p.ExtractionPlan,
                p.AnchoragePlan,
                p.UseTads,
                p.UseElastics,
                p.ExpectedDurationMonths,
                p.RetentionPlan,
                p.TreatmentGoals,
                p.RisksLimitations,
                ApprovedByName = p.ApprovedByDoctor != null ? p.ApprovedByDoctor.Name : null,
                ApprovedAt     = p.ApprovedAt != null ? p.ApprovedAt.Value.ToString("yyyy-MM-dd") : null,
            })
            .ToListAsync();
        return Ok(plans);
    }

    [HttpGet("{id:guid}/treatment-plan")]
    public async Task<IActionResult> GetTreatmentPlan(Guid id)
    {
        // Backward-compatible: returns the latest (or approved) plan
        var plan = await db.TreatmentPlans
            .Include(p => p.ApprovedByDoctor)
            .Where(p => p.OrthoCaseId == id)
            .OrderByDescending(p => p.IsApproved ? 1 : 0)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        if (plan is null) return Ok(null);
        return Ok(new
        {
            plan.Id,
            plan.PlanVersion,
            plan.PlanLabel,
            plan.IsApproved,
            plan.ApplianceType,
            plan.BracketSystem,
            plan.InitialWire,
            plan.ExtractionPlan,
            plan.AnchoragePlan,
            plan.UseTads,
            plan.UseElastics,
            plan.ExpectedDurationMonths,
            plan.RetentionPlan,
            plan.TreatmentGoals,
            plan.RisksLimitations,
            ApprovedByName = plan.ApprovedByDoctor?.Name,
            ApprovedAt     = plan.ApprovedAt?.ToString("yyyy-MM-dd"),
        });
    }

    private static readonly HashSet<string> ValidPlanLabels = new(StringComparer.OrdinalIgnoreCase) { "A", "B", "C" };

    [HttpPost("{id:guid}/treatment-plans")]
    public async Task<IActionResult> CreateTreatmentPlan(Guid id, [FromBody] CreateTreatmentPlanRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        // ── Canonicalise PlanLabel: trim whitespace and force uppercase ──
        // This prevents "a" / " b " / "C" from bypassing the unique index,
        // which is case-sensitive in PostgreSQL.
        var normalizedLabel = req.PlanLabel?.Trim().ToUpperInvariant();

        // Fetch existing labels for this ortho case
        var existingLabels = await db.TreatmentPlans
            .Where(p => p.OrthoCaseId == id)
            .Select(p => p.PlanLabel)
            .ToListAsync();

        // If caller supplied a label, validate it (after normalisation)
        if (normalizedLabel is not null)
        {
            if (!ValidPlanLabels.Contains(normalizedLabel))
                return BadRequest(new { message = "تصنيف الخطة غير صالح. القيم المسموح بها: A أو B أو C فقط" });

            if (existingLabels.Contains(normalizedLabel, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = $"تصنيف الخطة {normalizedLabel} مستخدم بالفعل لهذه الحالة" });
        }

        // Auto-select next available label when caller omitted it
        var chosenLabel = normalizedLabel
            ?? new[] { "A", "B", "C" }.FirstOrDefault(l => !existingLabels.Contains(l, StringComparer.OrdinalIgnoreCase));

        if (chosenLabel is null)
            return BadRequest(new { message = "تم إنشاء الخطط A و B و C بالفعل لهذه الحالة. لا يمكن إضافة خطة رابعة" });

        var plan = new TreatmentPlan
        {
            OrthoCaseId = id,
            PlanLabel = chosenLabel,
            ApplianceType = req.ApplianceType,
            BracketSystem = req.BracketSystem,
            InitialWire = req.InitialWire,
            ExtractionPlan = req.ExtractionPlan,
            AnchoragePlan = req.AnchoragePlan,
            UseTads = req.UseTads,
            UseElastics = req.UseElastics,
            ExpectedDurationMonths = req.ExpectedDurationMonths,
            RetentionPlan = req.RetentionPlan,
            TreatmentGoals = req.TreatmentGoals,
            RisksLimitations = req.RisksLimitations,
        };
        db.TreatmentPlans.Add(plan);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent request won the race for this (OrthoCaseId, PlanLabel) pair.
            // The pre-check passed but another transaction inserted the same label first.
            return Conflict(new { message = "تصنيف الخطة مستخدم بالفعل لهذه الحالة. قم بتحديث الصفحة والمحاولة مرة أخرى." });
        }
        return Ok(new { plan.Id, plan.PlanLabel, message = "تم إنشاء خطة العلاج" });
    }

    /// <summary>
    /// Detects PostgreSQL unique-constraint violation (23505) from a DbUpdateException.
    /// Used to handle concurrent PlanLabel inserts without exposing database internals.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        if (inner is Npgsql.PostgresException pgEx)
            return pgEx.SqlState == "23505";
        // Fallback: check the exception message for the standard SQLSTATE pattern
        return inner?.Message?.Contains("23505") == true;
    }

    [HttpPut("{id:guid}/treatment-plan")]
    public async Task<IActionResult> UpsertTreatmentPlan(Guid id, [FromBody] UpsertTreatmentPlanRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.TreatmentPlans.Where(p => p.OrthoCaseId == id).OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync();
        if (existing is null)
        {
            // New plan defaults to PlanLabel "A" (see entity default).
            // If a concurrent request already created Plan A, the unique index
            // IX_TreatmentPlans_OrthoCaseId_PlanLabel will catch it → HTTP 409.
            existing = new TreatmentPlan { OrthoCaseId = id };
            db.TreatmentPlans.Add(existing);
        }

        existing.ApplianceType          = req.ApplianceType;
        existing.BracketSystem          = req.BracketSystem;
        existing.InitialWire            = req.InitialWire;
        existing.ExtractionPlan         = req.ExtractionPlan;
        existing.AnchoragePlan          = req.AnchoragePlan;
        existing.UseTads                = req.UseTads;
        existing.UseElastics            = req.UseElastics;
        existing.ExpectedDurationMonths = req.ExpectedDurationMonths;
        existing.RetentionPlan          = req.RetentionPlan;
        existing.TreatmentGoals         = req.TreatmentGoals;
        existing.RisksLimitations       = req.RisksLimitations;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent first-time request already created Plan A for this case.
            return Conflict(new { message = "تصنيف الخطة مستخدم بالفعل لهذه الحالة. قم بتحديث الصفحة والمحاولة مرة أخرى." });
        }
        return Ok(new { existing.Id, message = "تم حفظ خطة العلاج" });
    }

    [HttpPut("{id:guid}/treatment-plans/{planId:guid}")]
    public async Task<IActionResult> UpdateTreatmentPlan(Guid id, Guid planId, [FromBody] UpsertTreatmentPlanRequest req)
    {
        var plan = await db.TreatmentPlans.FirstOrDefaultAsync(p => p.Id == planId && p.OrthoCaseId == id);
        if (plan is null) return NotFound(new { message = "خطة العلاج غير موجودة" });
        if (plan.IsApproved) return BadRequest(new { message = "لا يمكن تعديل خطة معتمدة" });

        plan.ApplianceType          = req.ApplianceType;
        plan.BracketSystem          = req.BracketSystem;
        plan.InitialWire            = req.InitialWire;
        plan.ExtractionPlan         = req.ExtractionPlan;
        plan.AnchoragePlan          = req.AnchoragePlan;
        plan.UseTads                = req.UseTads;
        plan.UseElastics            = req.UseElastics;
        plan.ExpectedDurationMonths = req.ExpectedDurationMonths;
        plan.RetentionPlan          = req.RetentionPlan;
        plan.TreatmentGoals         = req.TreatmentGoals;
        plan.RisksLimitations       = req.RisksLimitations;

        await db.SaveChangesAsync();
        return Ok(new { plan.Id, message = "تم تحديث خطة العلاج" });
    }

    [HttpPatch("{id:guid}/treatment-plan/approve")]
    public async Task<IActionResult> ApproveTreatmentPlan(Guid id)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var plan = await db.TreatmentPlans
            .Where(p => p.OrthoCaseId == id)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        if (plan is null) return NotFound(new { message = "خطة العلاج غير موجودة" });

        // FIX: Record the actual approving user, not the assigned orthodontist.
        var approverId = currentUser.UserId;
        if (approverId == null) return Unauthorized(new { message = "غير مصادق عليه" });

        var isAssignedOrthodontist = orthoCase.DoctorId != null &&
            await db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        if (!isAssignedOrthodontist && !currentUser.IsAdmin)
            return Forbid("فقط طبيب التقويم المعين أو المسؤول يمكنه اعتماد الخطة.");

        // Enforce single-approved-treatment-plan invariant: un-approve other plans for this case
        var otherPlans = await db.TreatmentPlans.Where(p => p.OrthoCaseId == id && p.Id != plan.Id).ToListAsync();
        foreach (var op in otherPlans) { op.IsApproved = false; }

        Guid? approvedByDoctorId = null;
        if (isAssignedOrthodontist)
        {
            approvedByDoctorId = orthoCase.DoctorId;
        }
        else if (currentUser.IsAdmin)
        {
            var adminDoctor = await db.Doctors
                .Where(d => d.UserId == approverId && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();
            approvedByDoctorId = adminDoctor;
        }

        plan.IsApproved = true;
        plan.ApprovedAt = DateTime.UtcNow;
        plan.ApprovedBy = approvedByDoctorId;
        await db.SaveChangesAsync();

        // If admin approved without a linked Doctor record, approvedByDoctorId is null.
        // The approval is still valid (recorded via ApprovedAt timestamp + audit logs),
        // but ApprovedBy will be null — UI should display "Admin" in this case.
        return Ok(new
        {
            plan.Id,
            plan.PlanVersion,
            plan.PlanLabel,
            plan.IsApproved,
            ApprovedAt = plan.ApprovedAt?.ToString("yyyy-MM-dd"),
            ApprovedByDoctorId = approvedByDoctorId,
            ApprovedByUserId = approverId,
        });
    }

    [HttpPatch("{id:guid}/treatment-plans/{planId:guid}/approve")]
    public async Task<IActionResult> ApproveSpecificTreatmentPlan(Guid id, Guid planId)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var plan = await db.TreatmentPlans.FirstOrDefaultAsync(p => p.Id == planId && p.OrthoCaseId == id);
        if (plan is null) return NotFound(new { message = "خطة العلاج غير موجودة" });

        var approverId = currentUser.UserId;
        if (approverId == null) return Unauthorized(new { message = "غير مصادق عليه" });

        var isAssignedOrthodontist = orthoCase.DoctorId != null &&
            await db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        if (!isAssignedOrthodontist && !currentUser.IsAdmin)
            return Forbid("فقط طبيب التقويم المعين أو المسؤول يمكنه اعتماد الخطة.");

        // Un-approve other plans for this case
        var otherPlans = await db.TreatmentPlans.Where(p => p.OrthoCaseId == id && p.Id != planId).ToListAsync();
        foreach (var op in otherPlans) { op.IsApproved = false; }

        Guid? approvedByDoctorId = null;
        if (isAssignedOrthodontist) { approvedByDoctorId = orthoCase.DoctorId; }
        else if (currentUser.IsAdmin)
        {
            var adminDoctor = await db.Doctors
                .Where(d => d.UserId == approverId && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();
            approvedByDoctorId = adminDoctor;
        }

        plan.IsApproved = true;
        plan.ApprovedAt = DateTime.UtcNow;
        plan.ApprovedBy = approvedByDoctorId;
        await db.SaveChangesAsync();

        return Ok(new
        {
            plan.Id,
            plan.PlanLabel,
            plan.IsApproved,
            ApprovedAt = plan.ApprovedAt?.ToString("yyyy-MM-dd"),
            ApprovedByDoctorId = approvedByDoctorId,
            ApprovedByUserId = approverId,
        });
    }

    // ─── Extraction Decision ─────────────────────────────────────────────────────

    [HttpGet("{id:guid}/extraction-decision")]
    public async Task<IActionResult> GetExtractionDecision(Guid id)
    {
        var decision = await db.ExtractionDecisions
            .Include(e => e.Doctor)
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
        if (decision is null) return Ok(null);
        return Ok(new {
            decision.Id,
            decision.Decision,
            decision.ProExtraction,
            decision.ConExtraction,
            decision.DoctorNotes,
            decision.AiRecommendation,
            DecidedByName = decision.Doctor?.Name,
            DecidedAt     = decision.DecidedAt?.ToString("yyyy-MM-dd"),
        });
    }

    [HttpPut("{id:guid}/extraction-decision")]
    public async Task<IActionResult> UpsertExtractionDecision(Guid id, [FromBody] UpsertExtractionDecisionRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.ExtractionDecisions.Where(e => e.OrthoCaseId == id).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync();
        if (existing is null)
        {
            existing = new ExtractionDecision { OrthoCaseId = id };
            db.ExtractionDecisions.Add(existing);
        }
        existing.Decision      = req.Decision;
        existing.DoctorNotes   = req.DoctorNotes;
        existing.ProExtraction = req.ProExtraction;
        existing.ConExtraction = req.ConExtraction;
        existing.DecidedAt     = DateTime.UtcNow;

        // Mirror to orthoCase for quick access
        orthoCase.ExtractionDecisionValue = req.Decision;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ قرار الخلع" });
    }

    // ─── Records Checklist ───────────────────────────────────────────────────────

    [HttpGet("{id:guid}/checklist")]
    public async Task<IActionResult> GetChecklist(Guid id)
    {
        var checklist = await db.RecordsChecklists
            .Where(r => r.OrthoCaseId == id)
            .FirstOrDefaultAsync();

        if (checklist is not null)
        {
            return Ok(new
            {
                checklist.Id,
                checklist.OrthoCaseId,
                checklist.ExtraoralFrontal,
                checklist.ExtraoralProfile,
                checklist.ExtraoralSmile,
                checklist.IntraoralFrontal,
                checklist.IntraoralRight,
                checklist.IntraoralLeft,
                checklist.UpperOcclusal,
                checklist.LowerOcclusal,
                checklist.Opg,
                checklist.LateralCeph,
                checklist.Cbct,
                checklist.StudyModels,
                checklist.Consent,
                checklist.Contract,
            });
        }

        // Auto-derive from existing data if no checklist exists yet
        var photos = await db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == id).ToListAsync();
        var hasCeph = await db.CephAnalyses.AnyAsync(c => c.OrthoCaseId == id);
        var hasModel = await db.ModelAnalyses.AnyAsync(m => m.OrthoCaseId == id);
        var patientId = await db.OrthoCases.Where(oc => oc.Id == id).Select(oc => oc.PatientId).FirstOrDefaultAsync();
        var contract = await db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == id)
            .AnyAsync();
        if (!contract && patientId != Guid.Empty)
        {
            var unlinkedCount = await db.Contracts
                .Where(c => c.PatientId == patientId && c.RelatedCaseId == null &&
                    (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
                .CountAsync();
            contract = unlinkedCount == 1;
        }

        // Auto-derive each item from (Category, Subtype) tags with the legacy
        // caption-keyword heuristic as an OR'd fallback (untagged photos keep working)
        return Ok(new
        {
            Id = (Guid?)null,
            OrthoCaseId = id,
            ExtraoralFrontal = OrthoPhotoRecords.DeriveExtraoralFrontal(photos),
            ExtraoralProfile = OrthoPhotoRecords.DeriveExtraoralProfile(photos),
            ExtraoralSmile = OrthoPhotoRecords.DeriveExtraoralSmile(photos),
            IntraoralFrontal = OrthoPhotoRecords.DeriveIntraoralFrontal(photos),
            IntraoralRight = OrthoPhotoRecords.DeriveIntraoralRight(photos),
            IntraoralLeft = OrthoPhotoRecords.DeriveIntraoralLeft(photos),
            UpperOcclusal = OrthoPhotoRecords.DeriveUpperOcclusal(photos),
            LowerOcclusal = OrthoPhotoRecords.DeriveLowerOcclusal(photos),
            Opg = OrthoPhotoRecords.DeriveOpg(photos),
            LateralCeph = hasCeph || OrthoPhotoRecords.DeriveLateralCeph(photos),
            Cbct = OrthoPhotoRecords.DeriveCbct(photos),
            StudyModels = hasModel,
            Consent = false,
            Contract = contract,
        });
    }

    [HttpPut("{id:guid}/checklist")]
    public async Task<IActionResult> UpsertChecklist(Guid id, [FromBody] UpsertChecklistRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.RecordsChecklists.FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (existing is null)
        {
            existing = new RecordsChecklist { OrthoCaseId = id };
            db.RecordsChecklists.Add(existing);
        }

        existing.ExtraoralFrontal = req.ExtraoralFrontal;
        existing.ExtraoralProfile = req.ExtraoralProfile;
        existing.ExtraoralSmile   = req.ExtraoralSmile;
        existing.IntraoralFrontal = req.IntraoralFrontal;
        existing.IntraoralRight   = req.IntraoralRight;
        existing.IntraoralLeft    = req.IntraoralLeft;
        existing.UpperOcclusal    = req.UpperOcclusal;
        existing.LowerOcclusal    = req.LowerOcclusal;
        existing.Opg              = req.Opg;
        existing.LateralCeph      = req.LateralCeph;
        existing.Cbct             = req.Cbct;
        existing.StudyModels      = req.StudyModels;
        existing.Consent          = req.Consent;
        existing.Contract         = req.Contract;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم تحديث قائمة السجلات" });
    }

    // ─── Diagnosis (enhanced) ─────────────────────────────────────────────────────

    [HttpGet("{id:guid}/diagnosis")]
    public async Task<IActionResult> GetDiagnosis(Guid id)
    {
        var latestCephInfo = await db.CephAnalyses
            .AsNoTracking()
            .Where(a => a.OrthoCaseId == id)
            .OrderByDescending(a => a.AnalysisDate)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(a => new
            {
                a.Id,
                AnalysisDate = a.AnalysisDate.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();
        var latestProfile = await db.PhotoAnalyses
            .AsNoTracking()
            .Where(a => a.OrthoCaseId == id && a.ViewType == "profile")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                AnalysisDate = a.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();
        var latestFrontal = await db.PhotoAnalyses
            .AsNoTracking()
            .Where(a => a.OrthoCaseId == id && a.ViewType == "frontal")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                AnalysisDate = a.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();

        var diagnosis = await db.OrthoDiagnoses
            .Include(d => d.ApprovedByDoctor)
            .Where(d => d.OrthoCaseId == id)
            .FirstOrDefaultAsync();

        if (diagnosis is not null)
        {
            return Ok(new
            {
                diagnosis.Id,
                diagnosis.SkeletalClassification,
                diagnosis.DentalClassification,
                diagnosis.FacialPattern,
                diagnosis.SoftTissueDiagnosis,
                diagnosis.FunctionalDiagnosis,
                diagnosis.Etiology,
                diagnosis.ANB,
                diagnosis.Wits,
                diagnosis.FMA,
                diagnosis.SNA,
                diagnosis.SNB,
                diagnosis.IMPA,
                diagnosis.Summary,
                diagnosis.CephSourceAnalysisId,
                diagnosis.CephSyncedAt,
                diagnosis.PhotoAnalysisSummary,
                diagnosis.ProfileSourceAnalysisId,
                diagnosis.FrontalSourceAnalysisId,
                diagnosis.PhotoAnalysisSyncedAt,
                LatestCephAnalysisId = latestCephInfo?.Id,
                LatestCephAnalysisDate = latestCephInfo?.AnalysisDate,
                IsCephSyncOutdated = latestCephInfo is not null &&
                    diagnosis.CephSourceAnalysisId != latestCephInfo.Id,
                LatestProfileAnalysisId = latestProfile?.Id,
                LatestProfileAnalysisDate = latestProfile?.AnalysisDate,
                LatestFrontalAnalysisId = latestFrontal?.Id,
                LatestFrontalAnalysisDate = latestFrontal?.AnalysisDate,
                IsPhotoAnalysisSyncOutdated =
                    (latestProfile is not null && diagnosis.ProfileSourceAnalysisId != latestProfile.Id) ||
                    (latestFrontal is not null && diagnosis.FrontalSourceAnalysisId != latestFrontal.Id),
                IsApproved = diagnosis.ApprovedAt != null,
                ApprovedByName = diagnosis.ApprovedByDoctor?.Name,
                ApprovedAt = diagnosis.ApprovedAt?.ToString("yyyy-MM-dd"),
            });
        }

        // Compute a summary from ClinicalExam, ProblemList, and CephAnalysis measurements
        var orthoCase = await db.OrthoCases
            .Include(c => c.ClinicalExam)
            .Include(c => c.ProblemList)
            .Include(c => c.CephAnalyses)
                .ThenInclude(a => a.Measurements)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var latestCeph = orthoCase.CephAnalyses
            .OrderByDescending(a => a.AnalysisDate)
            .FirstOrDefault();
        var measurements = latestCeph?.Measurements ?? [];

        var anbValue = measurements.FirstOrDefault(m => m.MeasurementName == "ANB")?.MeasurementValue;
        var witsValue = measurements.FirstOrDefault(m => m.MeasurementName == "Wits")?.MeasurementValue;
        var fmaValue = measurements.FirstOrDefault(m => m.MeasurementName == "FMA")?.MeasurementValue;
        var snaValue = measurements.FirstOrDefault(m => m.MeasurementName == "SNA")?.MeasurementValue;
        var snbValue = measurements.FirstOrDefault(m => m.MeasurementName == "SNB")?.MeasurementValue;
        var impaValue = measurements.FirstOrDefault(m => m.MeasurementName == "IMPA")?.MeasurementValue;

        string? skeletalClass = anbValue switch
        {
            >= 0 and <= 4 => "Class I",
            > 4 => "Class II",
            < 0 => "Class III",
            null => null
        };

        string? facialPattern = fmaValue switch
        {
            < 22 => "Hypodivergent",
            >= 22 and <= 28 => "Normodivergent",
            > 28 => "Hyperdivergent",
            null => null
        };

        string? dentalClass = orthoCase.ClinicalExam?.MolarRelation;

        var problemSummary = orthoCase.ProblemList.Count > 0
            ? string.Join("، ", orthoCase.ProblemList.OrderBy(p => p.SortOrder).Select(p => p.Description))
            : null;

        return Ok(new
        {
            Id = (Guid?)null,
            SkeletalClassification = skeletalClass,
            DentalClassification = dentalClass,
            FacialPattern = facialPattern,
            SoftTissueDiagnosis = (string?)null,
            FunctionalDiagnosis = (string?)null,
            Etiology = (string?)null,
            ANB = anbValue,
            Wits = witsValue,
            FMA = fmaValue,
            SNA = snaValue,
            SNB = snbValue,
            IMPA = impaValue,
            Summary = problemSummary,
            CephSourceAnalysisId = (Guid?)null,
            CephSyncedAt = (DateTime?)null,
            PhotoAnalysisSummary = (string?)null,
            ProfileSourceAnalysisId = (Guid?)null,
            FrontalSourceAnalysisId = (Guid?)null,
            PhotoAnalysisSyncedAt = (DateTime?)null,
            LatestCephAnalysisId = latestCephInfo?.Id,
            LatestCephAnalysisDate = latestCephInfo?.AnalysisDate,
            IsCephSyncOutdated = latestCephInfo is not null,
            LatestProfileAnalysisId = latestProfile?.Id,
            LatestProfileAnalysisDate = latestProfile?.AnalysisDate,
            LatestFrontalAnalysisId = latestFrontal?.Id,
            LatestFrontalAnalysisDate = latestFrontal?.AnalysisDate,
            IsPhotoAnalysisSyncOutdated = latestProfile is not null || latestFrontal is not null,
            IsApproved = false,
            ApprovedByName = (string?)null,
            ApprovedAt = (string?)null,
        });
    }

    [HttpPut("{id:guid}/diagnosis")]
    public async Task<IActionResult> UpsertDiagnosis(Guid id, [FromBody] UpsertDiagnosisRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.OrthoDiagnoses.FirstOrDefaultAsync(d => d.OrthoCaseId == id);
        if (existing is null)
        {
            existing = new OrthoDiagnosis { OrthoCaseId = id };
            db.OrthoDiagnoses.Add(existing);
        }

        existing.SkeletalClassification = req.SkeletalClassification;
        existing.DentalClassification   = req.DentalClassification;
        existing.FacialPattern          = req.FacialPattern;
        existing.SoftTissueDiagnosis    = req.SoftTissueDiagnosis;
        existing.FunctionalDiagnosis    = req.FunctionalDiagnosis;
        existing.Etiology               = req.Etiology;
        existing.ANB                    = req.ANB;
        existing.Wits                   = req.Wits;
        existing.FMA                    = req.FMA;
        existing.SNA                    = req.SNA;
        existing.SNB                    = req.SNB;
        existing.IMPA                   = req.IMPA;
        existing.Summary                = req.Summary;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ التشخيص" });
    }

    [HttpPatch("{id:guid}/diagnosis/approve")]
    public async Task<IActionResult> ApproveDiagnosis(Guid id)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var diagnosis = await db.OrthoDiagnoses.FirstOrDefaultAsync(d => d.OrthoCaseId == id);
        if (diagnosis is null) return NotFound(new { message = "التشخيص غير موجود" });

        var approverId = currentUser.UserId;
        if (approverId == null) return Unauthorized(new { message = "غير مصادق عليه" });

        var isAssignedOrthodontist = orthoCase.DoctorId != null &&
            await db.Doctors.AnyAsync(d => d.Id == orthoCase.DoctorId && d.UserId == approverId && d.IsActive);

        if (!isAssignedOrthodontist && !currentUser.IsAdmin)
            return Forbid("فقط طبيب التقويم المعين أو المسؤول يمكنه اعتماد التشخيص.");

        Guid? approvedByDoctorId = null;
        if (isAssignedOrthodontist) { approvedByDoctorId = orthoCase.DoctorId; }
        else if (currentUser.IsAdmin)
        {
            var adminDoctor = await db.Doctors
                .Where(d => d.UserId == approverId && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();
            approvedByDoctorId = adminDoctor;
        }

        diagnosis.ApprovedBy = approvedByDoctorId;
        diagnosis.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new
        {
            diagnosis.Id,
            IsApproved = true,
            ApprovedAt = diagnosis.ApprovedAt?.ToString("yyyy-MM-dd"),
            ApprovedByDoctorId = approvedByDoctorId,
            ApprovedByUserId = approverId,
            message = "تم اعتماد التشخيص",
        });
    }

    // ─── Retention Records ─────────────────────────────────────────────────────

    [HttpGet("{id:guid}/retention")]
    public async Task<IActionResult> GetRetention(Guid id)
    {
        var record = await db.RetentionRecords
            .Include(r => r.Visits)
            .Where(r => r.OrthoCaseId == id)
            .FirstOrDefaultAsync();
        if (record is null) return Ok(null);
        return Ok(new
        {
            record.Id,
            DebondDate   = record.DebondDate?.ToString("yyyy-MM-dd"),
            record.UpperRetainer,
            record.LowerRetainer,
            record.Instructions,
            record.Status,
            Visits = record.Visits
                .OrderBy(v => v.VisitDate)
                .Select(v => new
                {
                    v.Id,
                    VisitDate     = v.VisitDate?.ToString("yyyy-MM-dd"),
                    v.Period,
                    v.ToothStability,
                    v.RetainerStatus,
                    v.Notes,
                })
        });
    }

    [HttpPut("{id:guid}/retention")]
    public async Task<IActionResult> UpsertRetention(Guid id, [FromBody] UpsertRetentionRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.RetentionRecords.FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (existing is null)
        {
            existing = new RetentionRecord { OrthoCaseId = id };
            db.RetentionRecords.Add(existing);
        }

        existing.DebondDate    = req.DebondDate != null ? DateOnly.Parse(req.DebondDate) : null;
        existing.UpperRetainer = req.UpperRetainer;
        existing.LowerRetainer = req.LowerRetainer;
        existing.Instructions  = req.Instructions;
        existing.Status        = req.Status ?? existing.Status;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ سجل الاحتفاظ" });
    }

    [HttpPost("{id:guid}/retention/visits")]
    public async Task<IActionResult> AddRetentionVisit(Guid id, [FromBody] CreateRetentionVisitRequest req)
    {
        var retention = await db.RetentionRecords.FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (retention is null) return NotFound(new { message = "سجل الاحتفاظ غير موجود — أنشئ سجل الاحتفاظ أولاً" });

        var visit = new RetentionVisit
        {
            RetentionRecordId = retention.Id,
            VisitDate         = req.VisitDate != null ? DateOnly.Parse(req.VisitDate) : DateOnly.FromDateTime(DateTime.Today),
            Period            = req.Period,
            ToothStability    = req.ToothStability,
            RetainerStatus    = req.RetainerStatus,
            Notes             = req.Notes,
        };
        db.RetentionVisits.Add(visit);
        await db.SaveChangesAsync();
        return Ok(new
        {
            visit.Id,
            VisitDate     = visit.VisitDate?.ToString("yyyy-MM-dd"),
            visit.Period,
            visit.ToothStability,
            visit.RetainerStatus,
            visit.Notes,
        });
    }

    [HttpGet("{id:guid}/retention/visits")]
    public async Task<IActionResult> GetRetentionVisits(Guid id)
    {
        var visits = await db.RetentionVisits
            .Where(v => v.RetentionRecord.OrthoCaseId == id)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new
            {
                v.Id,
                VisitDate     = v.VisitDate != null ? v.VisitDate.Value.ToString("yyyy-MM-dd") : null,
                v.Period,
                v.ToothStability,
                v.RetainerStatus,
                v.Notes,
            })
            .ToListAsync();
        return Ok(visits);
    }

    // ─── Clinical Photos ────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/photos")]
    public async Task<IActionResult> AddPhoto(Guid id, [FromBody] AddOrthoPhotoRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        if (!OrthoPhotoRecords.TryNormalizeCategory(req.Category, out var category))
            return BadRequest(new { message = "فئة الصورة غير صالحة" });
        if (!OrthoPhotoRecords.TryNormalizePhase(req.TreatmentPhase, out var treatmentPhase))
            return BadRequest(new { message = "مرحلة العلاج غير صالحة" });

        var maxOrder = await db.OrthoClinicalPhotos
            .Where(p => p.OrthoCaseId == id)
            .MaxAsync(p => (int?)p.SortOrder) ?? 0;

        var photo = new OrthoClinicalPhoto
        {
            OrthoCaseId         = id,
            PhotoUrl            = req.PhotoUrl,
            PhotoType           = req.PhotoType ?? "Intraoral",
            Caption             = req.Caption,
            TakenAt             = req.TakenAt ?? DateTime.UtcNow,
            SortOrder           = req.SortOrder ?? (maxOrder + 1),
            Category            = category,
            Subtype             = string.IsNullOrWhiteSpace(req.Subtype) ? null : req.Subtype.Trim(),
            TreatmentPhase      = treatmentPhase,
            IsSelectedForReport = req.IsSelectedForReport ?? false,
        };
        db.OrthoClinicalPhotos.Add(photo);
        await db.SaveChangesAsync();

        return Ok(new
        {
            photo.Id,
            photo.PhotoUrl,
            photo.PhotoType,
            photo.Caption,
            TakenAt   = photo.TakenAt.ToString("yyyy-MM-dd"),
            photo.SortOrder,
            photo.Category,
            photo.Subtype,
            photo.TreatmentPhase,
            photo.IsSelectedForReport,
            PreparationStatus = "OriginalUploaded",
            IsPreparedForReport = false,
        });
    }

    [HttpGet("{id:guid}/photos")]
    public async Task<IActionResult> GetPhotos(
        Guid id,
        [FromQuery] string? category = null,
        [FromQuery] string? phase = null,
        [FromQuery] bool? selectedOnly = null)
    {
        if (!OrthoPhotoRecords.TryNormalizeCategory(category, out var normalizedCategory))
            return BadRequest(new { message = "فئة الصورة غير صالحة" });
        if (!OrthoPhotoRecords.TryNormalizePhase(phase, out var normalizedPhase))
            return BadRequest(new { message = "مرحلة العلاج غير صالحة" });

        var query = db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == id);
        if (normalizedCategory is not null)
            query = query.Where(p => p.Category == normalizedCategory);
        if (normalizedPhase is not null)
            query = query.Where(p => p.TreatmentPhase == normalizedPhase);
        if (selectedOnly == true)
            query = query.Where(p => p.IsSelectedForReport);

        var photos = await query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.TakenAt)
            .Select(p => new
            {
                p.Id,
                p.PhotoUrl,
                p.PhotoType,
                p.Caption,
                TakenAt   = p.TakenAt.ToString("yyyy-MM-dd"),
                p.SortOrder,
                p.Category,
                p.Subtype,
                p.TreatmentPhase,
                p.IsSelectedForReport,
                PreparationStatus = p.ImagePreparation != null
                    ? p.ImagePreparation.Status
                    : "OriginalUploaded",
                IsPreparedForReport = p.ImagePreparation != null,
                PreparedImageUrl = p.ImagePreparation != null
                    ? p.ImagePreparation.PreparedImageUrl
                    : null,
            })
            .ToListAsync();
        return Ok(photos);
    }

    [HttpPatch("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> UpdatePhoto(Guid id, Guid photoId, [FromBody] UpdateOrthoPhotoRequest req)
    {
        var photo = await db.OrthoClinicalPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        if (req.Category is not null)
        {
            if (!OrthoPhotoRecords.TryNormalizeCategory(req.Category, out var category))
                return BadRequest(new { message = "فئة الصورة غير صالحة" });
            photo.Category = category; // empty string clears the tag
        }
        if (req.TreatmentPhase is not null)
        {
            if (!OrthoPhotoRecords.TryNormalizePhase(req.TreatmentPhase, out var treatmentPhase))
                return BadRequest(new { message = "مرحلة العلاج غير صالحة" });
            photo.TreatmentPhase = treatmentPhase; // empty string clears the tag
        }
        if (req.Subtype is not null)
            photo.Subtype = string.IsNullOrWhiteSpace(req.Subtype) ? null : req.Subtype.Trim();
        if (req.Caption is not null)
            photo.Caption = string.IsNullOrWhiteSpace(req.Caption) ? null : req.Caption;
        if (req.IsSelectedForReport.HasValue)
            photo.IsSelectedForReport = req.IsSelectedForReport.Value;

        await db.SaveChangesAsync();

        return Ok(new
        {
            photo.Id,
            photo.PhotoUrl,
            photo.PhotoType,
            photo.Caption,
            TakenAt   = photo.TakenAt.ToString("yyyy-MM-dd"),
            photo.SortOrder,
            photo.Category,
            photo.Subtype,
            photo.TreatmentPhase,
            photo.IsSelectedForReport,
            message = "تم تحديث بيانات الصورة",
        });
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}/preparation")]
    public async Task<IActionResult> GetImagePreparation(Guid id, Guid photoId)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var photo = await db.OrthoClinicalPhotos
            .AsNoTracking()
            .Include(p => p.ImagePreparation)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        return Ok(MapImagePreparation(photo, photo.ImagePreparation));
    }

    [HttpPut("{id:guid}/photos/{photoId:guid}/preparation")]
    public async Task<IActionResult> SaveImagePreparation(
        Guid id,
        Guid photoId,
        [FromBody] SaveOrthoImagePreparationRequest req)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var photo = await db.OrthoClinicalPhotos
            .Include(p => p.ImagePreparation)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        if (req.CropX < 0 || req.CropY < 0 || req.CropWidth <= 0 || req.CropHeight <= 0 ||
            req.CropX + req.CropWidth > 1.00001m || req.CropY + req.CropHeight > 1.00001m)
            return BadRequest(new { message = "حدود القص غير صالحة" });
        if (req.Zoom is < 1 or > 4)
            return BadRequest(new { message = "قيمة التكبير يجب أن تكون بين 1 و 4" });
        if (req.RotationDegrees is < -180 or > 180)
            return BadRequest(new { message = "زاوية الدوران يجب أن تكون بين -180 و 180" });
        if (req.Brightness is < -100 or > 100 || req.Contrast is < -100 or > 100)
            return BadRequest(new { message = "الإضاءة والتباين يجب أن يكونا بين -100 و 100" });
        if (!ImageAspectRatios.TryGetValue(req.AspectRatio?.Trim() ?? "Original", out var aspectRatio))
            return BadRequest(new { message = "نسبة أبعاد الصورة غير مدعومة" });
        if (!ImagePreparationStatuses.TryGetValue(req.Status?.Trim() ?? "PreparedForReport", out var status))
            return BadRequest(new { message = "حالة تجهيز الصورة غير صالحة" });

        var preparation = photo.ImagePreparation;
        if (preparation is null)
        {
            preparation = new OrthoImagePreparation
            {
                OrthoClinicalPhotoId = photo.Id,
                PreparedAt = DateTime.UtcNow,
            };
            db.OrthoImagePreparations.Add(preparation);
        }

        preparation.CropX = req.CropX;
        preparation.CropY = req.CropY;
        preparation.CropWidth = req.CropWidth;
        preparation.CropHeight = req.CropHeight;
        preparation.Zoom = req.Zoom;
        preparation.RotationDegrees = req.RotationDegrees;
        preparation.Brightness = req.Brightness;
        preparation.Contrast = req.Contrast;
        preparation.FlipHorizontal = req.FlipHorizontal;
        preparation.FlipVertical = req.FlipVertical;
        preparation.AspectRatio = aspectRatio;
        preparation.Preset = string.IsNullOrWhiteSpace(req.Preset) ? null : req.Preset.Trim();
        preparation.Status = status;
        preparation.PreparedAt ??= DateTime.UtcNow;

        if (status == "ApprovedForPresentation")
        {
            preparation.ApprovedBy = currentUser.UserId;
            preparation.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            preparation.ApprovedBy = null;
            preparation.ApprovedAt = null;
        }

        photo.IsSelectedForReport =
            status is "SelectedForPresentation" or "ApprovedForPresentation";

        await db.SaveChangesAsync();
        return Ok(MapImagePreparation(photo, preparation));
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}/preparation")]
    public async Task<IActionResult> ResetImagePreparation(Guid id, Guid photoId)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var photo = await db.OrthoClinicalPhotos
            .Include(p => p.ImagePreparation)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        if (photo.ImagePreparation is not null)
            db.OrthoImagePreparations.Remove(photo.ImagePreparation);
        photo.IsSelectedForReport = false;
        await db.SaveChangesAsync();

        return Ok(MapImagePreparation(photo, null));
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId)
    {
        var accessError = await GetCaseAccessErrorAsync(id);
        if (accessError is not null) return accessError;

        var photo = await db.OrthoClinicalPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        db.OrthoClinicalPhotos.Remove(photo);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الصورة" });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class UpdateStageRequest
{
    public string Status { get; set; } = string.Empty;
}

public sealed class CreateOrthoFollowUpAppointmentRequest
{
    public string? AppointmentDate { get; init; }
    public string? StartTime { get; init; }
    public int? DurationMinutes { get; init; }
    public string? AppointmentType { get; init; }
    public Guid? DoctorId { get; init; }
    public Guid? ServiceId { get; init; }
    public string? Notes { get; init; }
}

public sealed class AddProblemItemRequest
{
    public string Category    { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Severity   { get; init; }
}

public sealed class UpsertExtractionDecisionRequest
{
    public string? Decision    { get; init; }
    public string? DoctorNotes { get; init; }
    public System.Text.Json.JsonDocument? ProExtraction { get; init; }
    public System.Text.Json.JsonDocument? ConExtraction { get; init; }
}

public sealed class CreateTreatmentPlanRequest
{
    public string? PlanLabel          { get; init; }
    public string? ApplianceType      { get; init; }
    public string? BracketSystem      { get; init; }
    public string? InitialWire        { get; init; }
    public string? ExtractionPlan     { get; init; }
    public string? AnchoragePlan      { get; init; }
    public bool UseTads               { get; init; }
    public bool UseElastics           { get; init; }
    public int? ExpectedDurationMonths { get; init; }
    public string? RetentionPlan      { get; init; }
    public string? TreatmentGoals     { get; init; }
    public string? RisksLimitations   { get; init; }
}

public sealed class UpsertTreatmentPlanRequest
{
    public string? ApplianceType          { get; init; }
    public string? BracketSystem          { get; init; }
    public string? InitialWire            { get; init; }
    public string? ExtractionPlan         { get; init; }
    public string? AnchoragePlan          { get; init; }
    public bool UseTads                   { get; init; }
    public bool UseElastics               { get; init; }
    public int? ExpectedDurationMonths    { get; init; }
    public string? RetentionPlan          { get; init; }
    public string? TreatmentGoals         { get; init; }
    public string? RisksLimitations       { get; init; }
}

public sealed class UpsertRetentionRequest
{
    public string? DebondDate    { get; init; }
    public string? UpperRetainer { get; init; }
    public string? LowerRetainer { get; init; }
    public string? Instructions  { get; init; }
    public string? Status        { get; init; }
}

public sealed class CreateRetentionVisitRequest
{
    public string? VisitDate      { get; init; }
    public string? Period         { get; init; }
    public string? ToothStability { get; init; }
    public string? RetainerStatus { get; init; }
    public string? Notes          { get; init; }
}

public sealed class UpsertDiagnosisRequest
{
    public string? SkeletalClassification { get; init; }
    public string? DentalClassification   { get; init; }
    public string? FacialPattern          { get; init; }
    public string? SoftTissueDiagnosis    { get; init; }
    public string? FunctionalDiagnosis    { get; init; }
    public string? Etiology               { get; init; }
    public decimal? ANB                   { get; init; }
    public decimal? Wits                  { get; init; }
    public decimal? FMA                   { get; init; }
    public decimal? SNA                   { get; init; }
    public decimal? SNB                   { get; init; }
    public decimal? IMPA                  { get; init; }
    public string? Summary                { get; init; }
}

public sealed class UpsertChecklistRequest
{
    public bool ExtraoralFrontal { get; init; }
    public bool ExtraoralProfile { get; init; }
    public bool ExtraoralSmile   { get; init; }
    public bool IntraoralFrontal { get; init; }
    public bool IntraoralRight   { get; init; }
    public bool IntraoralLeft    { get; init; }
    public bool UpperOcclusal    { get; init; }
    public bool LowerOcclusal    { get; init; }
    public bool Opg              { get; init; }
    public bool LateralCeph      { get; init; }
    public bool Cbct             { get; init; }
    public bool StudyModels      { get; init; }
    public bool Consent          { get; init; }
    public bool Contract         { get; init; }
}

public sealed class AddOrthoPhotoRequest
{
    public string PhotoUrl   { get; init; } = string.Empty;
    public string? PhotoType { get; init; }
    public string? Caption   { get; init; }
    public DateTime? TakenAt { get; init; }
    public int? SortOrder    { get; init; }
    /// <summary>Optional standardized category — validated against OrthoPhotoCategory (case-insensitive).</summary>
    public string? Category  { get; init; }
    /// <summary>Optional standardized subtype (e.g. FrontalRest, Profile, UpperOcclusal, OPG, LateralCeph, CBCT).</summary>
    public string? Subtype   { get; init; }
    /// <summary>Optional treatment phase — validated against OrthoTreatmentPhase (case-insensitive).</summary>
    public string? TreatmentPhase { get; init; }
    public bool? IsSelectedForReport { get; init; }
}

public sealed class UpdateOrthoPhotoRequest
{
    // All fields optional — only provided (non-null) fields are updated.
    // Empty strings clear the corresponding tag.
    public string? Category  { get; init; }
    public string? Subtype   { get; init; }
    public string? TreatmentPhase { get; init; }
    public bool? IsSelectedForReport { get; init; }
    public string? Caption   { get; init; }
}

public sealed class SaveOrthoImagePreparationRequest
{
    public decimal CropX { get; init; }
    public decimal CropY { get; init; }
    public decimal CropWidth { get; init; } = 1m;
    public decimal CropHeight { get; init; } = 1m;
    public decimal Zoom { get; init; } = 1m;
    public int RotationDegrees { get; init; }
    public int Brightness { get; init; }
    public int Contrast { get; init; }
    public bool FlipHorizontal { get; init; }
    public bool FlipVertical { get; init; }
    public string? AspectRatio { get; init; } = "Original";
    public string? Preset { get; init; }
    public string? Status { get; init; } = "PreparedForReport";
}
