using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AqlanDentalPro.API.Controllers;

// ── Request DTOs ────────────────────────────────────────────────────────────────
public sealed class CreateOrthoSurgicalCaseRequest
{
    public Guid OrthoCaseId { get; init; }
    public Guid? CephAnalysisId { get; init; }
    public Guid? SurgeonId { get; init; }
    public string? DiagnosisSummary { get; init; }
}

public sealed class UpdateOrthoSurgicalCaseRequest
{
    public Guid? SurgeonId { get; init; }
    public Guid? CephAnalysisId { get; init; }
    public string? DiagnosisSummary { get; init; }
}

public sealed class UpdateOrthoSurgicalStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed class SurgeonReviewRequest
{
    public string Decision { get; init; } = string.Empty; // Approved|RequestChanges|NotCandidate|NeedsImaging
    public string? ProposedProcedure { get; init; }
    public string? RequiredRecords { get; init; }
    public string? Risks { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateSurgeryFromPlanRequest
{
    public Guid? DoctorId { get; init; }
    public string? SurgeryType { get; init; }
    public string? TeethInvolved { get; init; }
}

public sealed class UpsertJointPlanRequest
{
    public string? ProcedureType { get; init; }
    public string? Timing { get; init; }
    public string? OrthodonticObjectives { get; init; }
    public string? SurgicalObjectives { get; init; }
    public string? PreSurgicalRequirements { get; init; }
    public string? PostSurgicalPlan { get; init; }
    public string? Risks { get; init; }
    public string? PatientExplanation { get; init; }
}

public sealed class CreateOrthoSurgicalCommentRequest
{
    public string Body { get; init; } = string.Empty;
}

// ── Surgical VTO (Sprint A9) request DTOs ───────────────────────────────────────
// Movement inputs are nullable decimals — a scenario may move only one jaw (e.g. isolated
// Le Fort I) and leave the rest null. The controller recomputes PredictedSNA/SNB/ANB/Wits/
// Overjet from these inputs and the approved CephAnalysis baseline, so they are NOT accepted
// from the client. Notes is free-form (capped at 4000 chars in the entity/DDL).
public sealed class CreateOrthoSurgicalVtoRequest
{
    public decimal? MaxillaMoveMm { get; init; }
    public decimal? MandibleMoveMm { get; init; }
    public decimal? ChinMoveMm { get; init; }
    public decimal? RotationDegree { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateOrthoSurgicalVtoRequest
{
    public decimal? MaxillaMoveMm { get; init; }
    public decimal? MandibleMoveMm { get; init; }
    public decimal? ChinMoveMm { get; init; }
    public decimal? RotationDegree { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Sprint A1 — the shared Ortho-Surgical (orthognathic) planning workspace API.
/// A thin workflow controller over the <see cref="OrthoSurgicalCase"/> bridge: it links
/// the existing OrthoCase/Ceph/SurgeryCase, drives the dual-approval status machine, and
/// creates the real SurgeryCase for execution. It never duplicates diagnosis, cephalometry
/// or the operative record — those are read from / written to their own modules.
/// </summary>
[ApiController]
[Route("api/ortho-surgical-cases")]
[Authorize(Policy = "OrthoSurgicalAccess")]
public class OrthoSurgicalCasesController(
    AppDbContext db,
    ILogger<OrthoSurgicalCasesController> logger,
    IPatientAccessService patientAccess,
    IAuditService audit,
    ICurrentUserService currentUser) : ControllerBase
{
    private Task<bool> CanAsync(string action) => PermissionGuard.HasAsync(db, currentUser, "ortho_surgical", action);
    private IActionResult Deny() => StatusCode(403, new { message = "غير مصرح لك بهذا الإجراء" });

    // Per-patient access guard for doctors (mirrors SurgeryController.DenyIfDoctorCannotAccess).
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "OrthoSurgicalCase", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }

    // ── List ────────────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? surgeonId,
        [FromQuery] Guid? patientId,
        [FromQuery] Guid? orthoCaseId,
        [FromQuery] bool? pendingSurgeonReview,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!await CanAsync("view")) return Deny();
        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        var query = db.OrthoSurgicalCases
            .Include(c => c.Patient)
            .Include(c => c.Orthodontist)
            .Include(c => c.Surgeon)
            .Where(c => c.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrthoSurgicalStatus>(status, true, out var st))
            query = query.Where(c => c.Status == st);
        if (surgeonId.HasValue) query = query.Where(c => c.SurgeonId == surgeonId);
        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (orthoCaseId.HasValue) query = query.Where(c => c.OrthoCaseId == orthoCaseId);
        if (pendingSurgeonReview == true)
            query = query.Where(c => c.Status == OrthoSurgicalStatus.SentToSurgeon
                                  || c.Status == OrthoSurgicalStatus.SurgeonReviewPending);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.CaseNumber,
                c.PatientId,
                PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
                PatientNumber = c.Patient.PatientNumber,
                c.OrthoCaseId,
                c.CephAnalysisId,
                c.SurgeryCaseId,
                OrthodontistName = c.Orthodontist != null ? c.Orthodontist.Name : null,
                SurgeonName = c.Surgeon != null ? c.Surgeon.Name : null,
                Status = c.Status.ToString(),
                StatusLabel = OrthoSurgicalStatusTransitions.GetArabicLabel(c.Status),
                ResponsibleParty = ResponsiblePartyLabel(c.Status),
                c.OrthodontistApprovedAt,
                c.SurgeonApprovedAt,
                CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize });
    }

    // ── Detail ──────────────────────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases
            .Include(x => x.Patient)
            .Include(x => x.Orthodontist)
            .Include(x => x.Surgeon)
            .Include(x => x.SurgeonReview)
            .Include(x => x.JointPlan)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        return Ok(new
        {
            c.Id,
            c.CaseNumber,
            c.PatientId,
            PatientName = c.Patient.FirstName + " " + c.Patient.LastName,
            PatientNumber = c.Patient.PatientNumber,
            c.OrthoCaseId,
            c.CephAnalysisId,
            c.SurgeryCaseId,
            c.OrthodontistId,
            OrthodontistName = c.Orthodontist?.Name,
            c.SurgeonId,
            SurgeonName = c.Surgeon?.Name,
            Status = c.Status.ToString(),
            StatusLabel = OrthoSurgicalStatusTransitions.GetArabicLabel(c.Status),
            ResponsibleParty = ResponsiblePartyLabel(c.Status),
            AllowedTransitions = OrthoSurgicalStatusTransitions.GetAllowedTransitions(c.Status)
                .Select(s => s.ToString()),
            c.DiagnosisSummary,
            c.OrthodontistApprovedAt,
            c.SurgeonApprovedAt,
            SurgeonReview = c.SurgeonReview == null ? null : new
            {
                c.SurgeonReview.Decision,
                c.SurgeonReview.ProposedProcedure,
                c.SurgeonReview.RequiredRecords,
                c.SurgeonReview.Risks,
                c.SurgeonReview.Notes,
                c.SurgeonReview.ReviewedAt
            },
            JointPlan = c.JointPlan == null ? null : new
            {
                c.JointPlan.ProcedureType,
                c.JointPlan.Timing,
                c.JointPlan.OrthodonticObjectives,
                c.JointPlan.SurgicalObjectives,
                c.JointPlan.PreSurgicalRequirements,
                c.JointPlan.PostSurgicalPlan,
                c.JointPlan.Risks,
                c.JointPlan.PatientExplanation,
                c.JointPlan.LockedAt
            },
            CreatedAt = c.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    // ── Readiness (Sprint A3) ──────────────────────────────────────────────────────
    // Pure READ over the existing RecordsChecklist / OrthoDiagnosis / CephAnalysis
    // entities — nothing is duplicated or persisted here. Drives the workspace's
    // readiness gates (RecordsReady / CephReady / DiagnosisReady / SurgeonReviewReady)
    // per docs/ortho-module/ORTHO_SURGICAL_AI_VISION_EXPANSION.md §7.
    [HttpGet("{id:guid}/readiness")]
    public async Task<IActionResult> GetReadiness(Guid id)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var checklist = await db.RecordsChecklists.FirstOrDefaultAsync(r => r.OrthoCaseId == c.OrthoCaseId);
        var diagnosis = await db.OrthoDiagnoses.FirstOrDefaultAsync(d => d.OrthoCaseId == c.OrthoCaseId);

        // Prefer the ceph analysis explicitly linked to this case; fall back to the
        // most recent analysis on the ortho case if none was linked yet.
        var ceph = c.CephAnalysisId.HasValue
            ? await db.CephAnalyses.FirstOrDefaultAsync(a => a.Id == c.CephAnalysisId && a.IsActive)
            : await db.CephAnalyses
                .Where(a => a.OrthoCaseId == c.OrthoCaseId && a.IsActive)
                .OrderByDescending(a => a.AnalysisDate)
                .FirstOrDefaultAsync();

        var missing = new List<string>();

        var recordsCore = checklist is not null
            && checklist.ExtraoralFrontal && checklist.ExtraoralProfile
            && checklist.IntraoralFrontal && checklist.IntraoralRight && checklist.IntraoralLeft
            && checklist.Opg && checklist.LateralCeph && checklist.StudyModels;
        if (checklist is null) missing.Add("لم تُحفظ قائمة السجلات بعد");
        else
        {
            if (!checklist.ExtraoralFrontal || !checklist.ExtraoralProfile) missing.Add("صور خارج الفم (أمامي/جانبي)");
            if (!checklist.IntraoralFrontal || !checklist.IntraoralRight || !checklist.IntraoralLeft) missing.Add("صور داخل الفم");
            if (!checklist.Opg) missing.Add("صورة بانوراما OPG");
            if (!checklist.LateralCeph) missing.Add("سيفالو جانبي");
            if (!checklist.StudyModels) missing.Add("نماذج دراسية / مسح داخل الفم");
        }

        var cephReady = ceph is not null && ceph.IsApproved;
        if (ceph is null) missing.Add("لا يوجد تحليل سيفالو مرتبط بالحالة");
        else if (!ceph.IsApproved) missing.Add("تحليل السيفالو لم يُعتمد بعد");

        var diagnosisReady = diagnosis is not null
            && !string.IsNullOrWhiteSpace(diagnosis.SkeletalClassification)
            && diagnosis.ApprovedAt is not null;
        if (diagnosis is null) missing.Add("لا يوجد تشخيص تقويمي محفوظ");
        else if (diagnosis.ApprovedAt is null) missing.Add("التشخيص التقويمي لم يُعتمد بعد");

        var surgeonReviewReady = recordsCore && cephReady && diagnosisReady;

        return Ok(new
        {
            OrthoSurgicalCaseId = c.Id,
            RecordsReady = recordsCore,
            CephReady = cephReady,
            DiagnosisReady = diagnosisReady,
            SurgeonReviewReady = surgeonReviewReady,
            Missing = missing,
            Checklist = checklist is null ? null : new
            {
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
                checklist.Contract
            },
            Diagnosis = diagnosis is null ? null : new
            {
                diagnosis.SkeletalClassification,
                diagnosis.DentalClassification,
                diagnosis.FacialPattern,
                diagnosis.Summary,
                diagnosis.ApprovedAt
            },
            Ceph = ceph is null ? null : new
            {
                ceph.Id,
                ceph.IsApproved,
                ceph.AnalysisDate
            }
        });
    }

    // ── Discussion comments (Sprint A4) ───────────────────────────────────────────
    // The back-and-forth between orthodontist and surgeon across review rounds.
    // Distinct from SurgeonReview.Notes (a single upsertable field that gets overwritten
    // on each review) — comments accumulate and keep the full collaboration history.
    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid id)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var comments = await db.OrthoSurgicalComments
            .Where(m => m.OrthoSurgicalCaseId == id && m.IsActive)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.AuthorUserId,
                m.AuthorRole,
                m.Body,
                CreatedAt = m.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
            .ToListAsync();

        return Ok(new { data = comments });
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateOrthoSurgicalCommentRequest req)
    {
        // Same actors who can edit the case (orthodontist/surgeon/admin) can comment on it.
        if (!await CanAsync("edit")) return Deny();

        var body = req.Body?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(body))
            return BadRequest(new { message = "نص التعليق مطلوب" });
        if (body.Length > 2000)
            return BadRequest(new { message = "التعليق طويل جدًا (الحد الأقصى 2000 حرف)" });

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var comment = new OrthoSurgicalComment
        {
            OrthoSurgicalCaseId = id,
            AuthorUserId = currentUser.UserId,
            AuthorRole = currentUser.Role?.ToString(),
            Body = body
        };
        db.OrthoSurgicalComments.Add(comment);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Create, "OrthoSurgicalComment", comment.Id,
            newData: new { orthoSurgicalCaseId = id });

        return Ok(new
        {
            comment.Id,
            comment.AuthorUserId,
            comment.AuthorRole,
            comment.Body,
            CreatedAt = comment.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
        });
    }

    // ── Audit trail (Sprint A4) ────────────────────────────────────────────────────
    // Pure READ over the existing AuditLogs table (already written by every action in
    // this controller via IAuditService) — no new audit mechanism, just a per-case view.
    [HttpGet("{id:guid}/audit-trail")]
    public async Task<IActionResult> GetAuditTrail(Guid id)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var entries = await db.AuditLogs
            .Include(a => a.User)
            .Where(a => a.Resource == "OrthoSurgicalCase" && a.ResourceId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .Select(a => new
            {
                a.Id,
                Action = a.Action.ToString(),
                Username = a.User != null ? a.User.Username : "النظام",
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
            .ToListAsync();

        return Ok(new { data = entries });
    }

    // ── Create (from an existing OrthoCase) ───────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrthoSurgicalCaseRequest req)
    {
        if (!await CanAsync("create")) return Deny();

        if (req.OrthoCaseId == Guid.Empty)
            return BadRequest(new { message = "حالة التقويم مطلوبة" });

        var orthoCase = await db.OrthoCases.FirstOrDefaultAsync(o => o.Id == req.OrthoCaseId && o.IsActive);
        if (orthoCase is null)
            return NotFound(new { message = "حالة التقويم غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(orthoCase.PatientId);
        if (denied is not null) return denied;

        // One active ortho-surgical case per ortho case (avoid duplicates).
        if (await db.OrthoSurgicalCases.AnyAsync(c => c.OrthoCaseId == req.OrthoCaseId && c.IsActive))
            return Conflict(new { message = "توجد حالة تقويمية جراحية مرتبطة بحالة التقويم هذه بالفعل" });

        if (req.CephAnalysisId.HasValue &&
            !await db.CephAnalyses.AnyAsync(a => a.Id == req.CephAnalysisId && a.IsActive))
            return BadRequest(new { message = "تحليل السيفالو المحدد غير موجود" });

        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var lockKey = Math.Abs("OrthoSurgicalCaseNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                var year = DateTime.UtcNow.Year;
                var count = await db.OrthoSurgicalCases.IgnoreQueryFilters()
                    .CountAsync(c => c.CaseNumber.StartsWith($"OS-{year}-"));

                var entity = new OrthoSurgicalCase
                {
                    CaseNumber = $"OS-{year}-{(count + 1):D3}",
                    PatientId = orthoCase.PatientId,
                    OrthoCaseId = orthoCase.Id,
                    CephAnalysisId = req.CephAnalysisId,
                    OrthodontistId = orthoCase.DoctorId, // Doctors.Id
                    SurgeonId = req.SurgeonId,
                    BranchId = orthoCase.BranchId,
                    Status = OrthoSurgicalStatus.DraftByOrthodontist,
                    DiagnosisSummary = req.DiagnosisSummary
                };
                db.OrthoSurgicalCases.Add(entity);

                try
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await tx.RollbackAsync();
                    logger.LogWarning("OrthoSurgical case number collision on attempt {Attempt}, retrying", attempt + 1);
                    continue;
                }

                await audit.LogAsync(AuditAction.Create, "OrthoSurgicalCase", entity.Id,
                    newData: new { entity.CaseNumber, entity.OrthoCaseId, entity.PatientId });

                return CreatedAtAction(nameof(GetById), new { id = entity.Id },
                    new { entity.Id, entity.CaseNumber });
            }
            catch (Exception ex) when (ex is not DbUpdateException)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        return StatusCode(500, new { message = "فشل إنشاء الحالة بعد عدة محاولات" });
    }

    // ── Update basic fields ───────────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrthoSurgicalCaseRequest req)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        if (req.CephAnalysisId.HasValue &&
            !await db.CephAnalyses.AnyAsync(a => a.Id == req.CephAnalysisId && a.IsActive))
            return BadRequest(new { message = "تحليل السيفالو المحدد غير موجود" });

        if (req.SurgeonId.HasValue) c.SurgeonId = req.SurgeonId;
        if (req.CephAnalysisId.HasValue) c.CephAnalysisId = req.CephAnalysisId;
        if (req.DiagnosisSummary is not null) c.DiagnosisSummary = req.DiagnosisSummary;
        c.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(new { c.Id, message = "تم الحفظ" });
    }

    // ── Status transition ─────────────────────────────────────────────────────────
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrthoSurgicalStatusRequest req)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        if (!Enum.TryParse<OrthoSurgicalStatus>(req.Status, true, out var newStatus))
            return BadRequest(new { message = "الحالة غير صالحة" });

        var transitionError = OrthoSurgicalStatusTransitions.GetValidationError(c.Status, newStatus);
        if (transitionError is not null)
            return BadRequest(new { message = transitionError });

        var oldStatus = c.Status;
        c.Status = newStatus;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Update, "OrthoSurgicalCase", c.Id,
            newData: new { from = oldStatus.ToString(), to = newStatus.ToString() });

        return Ok(new { c.Id, status = newStatus.ToString() });
    }

    // ── Send to surgeon ───────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/send-to-surgeon")]
    public async Task<IActionResult> SendToSurgeon(Guid id)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var error = OrthoSurgicalStatusTransitions.GetValidationError(c.Status, OrthoSurgicalStatus.SentToSurgeon);
        if (error is not null) return BadRequest(new { message = error });

        c.Status = OrthoSurgicalStatus.SentToSurgeon;
        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { c.Id, status = c.Status.ToString() });
    }

    // ── Surgeon review (upsert) ────────────────────────────────────────────────────
    [HttpPost("{id:guid}/surgeon-review")]
    public async Task<IActionResult> UpsertSurgeonReview(Guid id, [FromBody] SurgeonReviewRequest req)
    {
        // Only the surgeon (or admin) may record the surgeon review.
        if (!await CanAsync("approve")) return Deny();
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.OralSurgeon)
            return StatusCode(403, new { message = "مراجعة الجراح مقتصرة على أخصائي الجراحة" });

        var validDecisions = new[] { "Approved", "RequestChanges", "NotCandidate", "NeedsImaging" };
        if (string.IsNullOrWhiteSpace(req.Decision) || !validDecisions.Contains(req.Decision))
            return BadRequest(new { message = "قرار المراجعة غير صالح" });

        var c = await db.OrthoSurgicalCases
            .Include(x => x.SurgeonReview)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        // Move into "review pending" the first time a surgeon opens the case.
        if (c.Status == OrthoSurgicalStatus.SentToSurgeon)
            c.Status = OrthoSurgicalStatus.SurgeonReviewPending;

        var review = c.SurgeonReview;
        if (review is null)
        {
            review = new SurgeonReview { OrthoSurgicalCaseId = c.Id };
            db.SurgeonReviews.Add(review);
        }
        review.SurgeonId = c.SurgeonId;
        review.Decision = req.Decision;
        review.ProposedProcedure = req.ProposedProcedure;
        review.RequiredRecords = req.RequiredRecords;
        review.Risks = req.Risks;
        review.Notes = req.Notes;
        review.ReviewedAt = DateTime.UtcNow;

        // Reflect a "request changes" / "not candidate" decision in the case status.
        if (req.Decision == "RequestChanges" &&
            OrthoSurgicalStatusTransitions.IsValidTransition(c.Status, OrthoSurgicalStatus.SurgeonRequestedChanges))
            c.Status = OrthoSurgicalStatus.SurgeonRequestedChanges;
        else if (req.Decision == "NotCandidate" &&
            OrthoSurgicalStatusTransitions.IsValidTransition(c.Status, OrthoSurgicalStatus.NotSurgicalCandidate))
            c.Status = OrthoSurgicalStatus.NotSurgicalCandidate;

        c.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Update, "OrthoSurgicalCase", c.Id,
            newData: new { surgeonReview = req.Decision });

        return Ok(new { c.Id, review.Decision, status = c.Status.ToString() });
    }

    // ── Joint plan (Sprint A5) ────────────────────────────────────────────────────
    // Either side (orthodontist/surgeon/admin) can draft/edit the joint plan content
    // while it is unlocked. Once BOTH approvals land (ApplyApproval below), the plan
    // is locked — no further edits are possible, matching the "immutable once approved"
    // rule from ORTHO_SURGICAL_AI_VISION_EXPANSION.md §7 (JointPlanReady / lock).
    [HttpPut("{id:guid}/joint-plan")]
    public async Task<IActionResult> UpsertJointPlan(Guid id, [FromBody] UpsertJointPlanRequest req)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases
            .Include(x => x.JointPlan)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        if (c.JointPlan?.LockedAt is not null)
            return BadRequest(new { message = "لا يمكن تعديل الخطة المشتركة بعد اعتماد الطرفين" });

        var plan = c.JointPlan;
        if (plan is null)
        {
            plan = new JointPlan { OrthoSurgicalCaseId = c.Id };
            db.JointPlans.Add(plan);
        }

        plan.ProcedureType = req.ProcedureType;
        plan.Timing = req.Timing;
        plan.OrthodonticObjectives = req.OrthodonticObjectives;
        plan.SurgicalObjectives = req.SurgicalObjectives;
        plan.PreSurgicalRequirements = req.PreSurgicalRequirements;
        plan.PostSurgicalPlan = req.PostSurgicalPlan;
        plan.Risks = req.Risks;
        plan.PatientExplanation = req.PatientExplanation;

        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Update, "OrthoSurgicalCase", c.Id,
            newData: new { jointPlanUpdated = true });

        return Ok(new
        {
            c.Id,
            plan.ProcedureType,
            plan.Timing,
            plan.OrthodonticObjectives,
            plan.SurgicalObjectives,
            plan.PreSurgicalRequirements,
            plan.PostSurgicalPlan,
            plan.Risks,
            plan.PatientExplanation,
            plan.LockedAt
        });
    }

    // ── Dual approval ──────────────────────────────────────────────────────────────
    [HttpPost("{id:guid}/approve-orthodontist")]
    public async Task<IActionResult> ApproveOrthodontist(Guid id)
    {
        if (!await CanAsync("approve")) return Deny();
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Orthodontist)
            return StatusCode(403, new { message = "اعتماد التقويم مقتصر على أخصائي التقويم" });
        return await ApplyApproval(id, orthodontist: true);
    }

    [HttpPost("{id:guid}/approve-surgeon")]
    public async Task<IActionResult> ApproveSurgeon(Guid id)
    {
        if (!await CanAsync("approve")) return Deny();
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.OralSurgeon)
            return StatusCode(403, new { message = "اعتماد الجراحة مقتصر على أخصائي الجراحة" });
        return await ApplyApproval(id, orthodontist: false);
    }

    private async Task<IActionResult> ApplyApproval(Guid id, bool orthodontist)
    {
        var c = await db.OrthoSurgicalCases
            .Include(x => x.JointPlan)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        // Approval only makes sense once the surgeon has reviewed the case.
        if (c.Status != OrthoSurgicalStatus.SurgeonReviewPending &&
            c.Status != OrthoSurgicalStatus.JointPlanApproved)
            return BadRequest(new { message = "لا يمكن الاعتماد قبل مراجعة الجراح" });

        var now = DateTime.UtcNow;
        if (orthodontist) c.OrthodontistApprovedAt = now;
        else c.SurgeonApprovedAt = now;

        // When BOTH sides have approved, lock the joint plan and advance the status.
        // A JointPlan may not exist yet if neither side used the joint-plan editing
        // endpoint before approving — auto-create a bare row so locking never silently
        // no-ops (A5 fix: previously this branch required c.JointPlan to already exist).
        if (c.OrthodontistApprovedAt is not null && c.SurgeonApprovedAt is not null)
        {
            if (c.JointPlan is null)
            {
                c.JointPlan = new JointPlan { OrthoSurgicalCaseId = c.Id };
                db.JointPlans.Add(c.JointPlan);
            }
            if (c.JointPlan.LockedAt is null)
            {
                c.JointPlan.OrthodontistApprovedAt = c.OrthodontistApprovedAt;
                c.JointPlan.SurgeonApprovedAt = c.SurgeonApprovedAt;
                c.JointPlan.LockedAt = now;
            }
            if (OrthoSurgicalStatusTransitions.IsValidTransition(c.Status, OrthoSurgicalStatus.JointPlanApproved))
                c.Status = OrthoSurgicalStatus.JointPlanApproved;
        }

        c.UpdatedAt = now;
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Update, "OrthoSurgicalCase", c.Id,
            newData: new { approvedBy = orthodontist ? "orthodontist" : "surgeon", status = c.Status.ToString() });

        return Ok(new
        {
            c.Id,
            status = c.Status.ToString(),
            c.OrthodontistApprovedAt,
            c.SurgeonApprovedAt,
            bothApproved = c.OrthodontistApprovedAt is not null && c.SurgeonApprovedAt is not null
        });
    }

    // ── Surgery execution summary (Sprint A6) ─────────────────────────────────────
    // Pure READ over the existing SurgeryCase/PreopReport/OperativeReport/PostopRecord
    // once create-surgery-case has linked one. Gives an inline glance at execution
    // status without duplicating the surgery module — the full record stays at
    // /surgery/{id}, which this only summarizes and links out to.
    [HttpGet("{id:guid}/surgery-summary")]
    public async Task<IActionResult> GetSurgerySummary(Guid id)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        if (c.SurgeryCaseId is null)
            return Ok(new { linked = false });

        var surgery = await db.SurgeryCases
            .Include(s => s.Doctor)
            .FirstOrDefaultAsync(s => s.Id == c.SurgeryCaseId);
        if (surgery is null)
            return Ok(new { linked = false });

        var preop = await db.PreopReports.FirstOrDefaultAsync(p => p.SurgeryCaseId == surgery.Id);
        var operative = await db.OperativeReports.FirstOrDefaultAsync(o => o.SurgeryCaseId == surgery.Id);
        var postop = await db.PostopRecords.FirstOrDefaultAsync(p => p.SurgeryCaseId == surgery.Id);

        return Ok(new
        {
            Linked = true,
            surgery.Id,
            surgery.CaseNumber,
            surgery.SurgeryType,
            Status = surgery.Status.ToString(),
            DoctorName = surgery.Doctor?.Name,
            Preop = preop is null ? null : new
            {
                SurgeryDate = preop.SurgeryDate?.ToString("yyyy-MM-dd"),
                preop.ConsentSigned
            },
            Operative = operative is null ? null : new
            {
                SurgeryDateTime = operative.SurgeryDateTime?.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                operative.Outcome,
                operative.ApprovedAt
            },
            Postop = postop is null ? null : new
            {
                HasInstructions = !string.IsNullOrWhiteSpace(postop.Instructions)
            }
        });
    }

    // ── Create the real SurgeryCase for execution ──────────────────────────────────
    [HttpPost("{id:guid}/create-surgery-case")]
    public async Task<IActionResult> CreateSurgeryCase(Guid id, [FromBody] CreateSurgeryFromPlanRequest req)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases
            .Include(x => x.JointPlan)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        if (c.Status != OrthoSurgicalStatus.ReadyForSurgery)
            return BadRequest(new { message = "لا يمكن فتح حالة جراحية قبل أن تصبح الخطة جاهزة للجراحة" });

        if (c.SurgeryCaseId is not null)
            return Conflict(new { message = "توجد حالة جراحية مرتبطة بهذه الخطة بالفعل" });

        var surgeryType = !string.IsNullOrWhiteSpace(req.SurgeryType)
            ? req.SurgeryType!
            : (!string.IsNullOrWhiteSpace(c.JointPlan?.ProcedureType) ? c.JointPlan!.ProcedureType! : "جراحة الفكين التقويمية");

        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var lockKey = Math.Abs("SurgeryCaseNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                var year = DateTime.UtcNow.Year;
                var count = await db.SurgeryCases.IgnoreQueryFilters()
                    .CountAsync(s => s.CaseNumber.StartsWith($"SU-{year}-"));

                var surgery = new SurgeryCase
                {
                    CaseNumber = $"SU-{year}-{(count + 1):D3}",
                    PatientId = c.PatientId,
                    DoctorId = req.DoctorId ?? c.SurgeonId,
                    SurgeryType = surgeryType,
                    TeethInvolved = req.TeethInvolved,
                    Status = SurgeryCaseStatus.Scheduled
                };
                db.SurgeryCases.Add(surgery);

                c.SurgeryCaseId = surgery.Id;
                c.Status = OrthoSurgicalStatus.SurgeryScheduled;
                c.UpdatedAt = DateTime.UtcNow;

                try
                {
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    await tx.RollbackAsync();
                    logger.LogWarning("Surgery case number collision on attempt {Attempt}, retrying", attempt + 1);
                    continue;
                }

                await audit.LogAsync(AuditAction.Create, "SurgeryCase", surgery.Id,
                    newData: new { surgery.CaseNumber, fromOrthoSurgicalCase = c.Id });

                return Ok(new { orthoSurgicalCaseId = c.Id, surgeryCaseId = surgery.Id, surgery.CaseNumber, status = c.Status.ToString() });
            }
            catch (Exception ex) when (ex is not DbUpdateException)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        return StatusCode(500, new { message = "فشل إنشاء الحالة الجراحية بعد عدة محاولات" });
    }

    // ── PDF reports (Sprint A7) ────────────────────────────────────────────────────
    // Aggregate existing data only (no new computation). Same per-patient access guard
    // as the rest of the controller; never expose exception details in the response.
    [HttpGet("{id:guid}/report/pdf")]
    public async Task<IActionResult> GetDoctorReportPdf(
        Guid id,
        [FromServices] AqlanDentalPro.API.Services.OrthoSurgicalReportPdfGenerator generator)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        try
        {
            var pdf = await generator.GenerateDoctorReportAsync(id);
            return File(pdf, "application/pdf", $"ortho-surgical-report-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate ortho-surgical doctor report for case {CaseId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء التقرير" });
        }
    }

    [HttpGet("{id:guid}/patient-explanation/pdf")]
    public async Task<IActionResult> GetPatientExplanationPdf(
        Guid id,
        [FromServices] AqlanDentalPro.API.Services.OrthoSurgicalReportPdfGenerator generator)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        try
        {
            var pdf = await generator.GeneratePatientExplanationAsync(id);
            return File(pdf, "application/pdf", $"ortho-surgical-patient-explanation-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate ortho-surgical patient explanation for case {CaseId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء التقرير" });
        }
    }

    // ── AI text assistant (Sprint A8) ─────────────────────────────────────────────
    // Draft-only: never written to JointPlan/SurgeonReview automatically. The caller
    // (doctor) copies text into the joint-plan editor explicitly after review. Same
    // safety machinery as OrthoCaseAiController/CephController's draft-diagnosis:
    // Settings-gated, honest Arabic errors, every attempt audited.
    [HttpPost("{id:guid}/ai/draft")]
    public async Task<IActionResult> GenerateAiDraft(
        Guid id,
        [FromBody] OrthoCaseDraftRequestDto request,
        [FromServices] OrthoSurgicalDraftService draftService,
        CancellationToken ct)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        try
        {
            var result = await draftService.GenerateAsync(id, request.Section, ct);
            return result is null
                ? NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" })
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CephAiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (CephAiLimitReachedException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (CephAiUpstreamException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = CephAiDraftService.UpstreamFailureMessageAr });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate ortho-surgical AI draft for case {CaseId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "تعذر توليد المسودة حالياً" });
        }
    }

    // ── Surgical VTO (Sprint A9) ─────────────────────────────────────────────────
    // A Visual Treatment Objective scenario for an ortho-surgical case: the doctor records
    // a planned hard-tissue movement (maxilla/mandible/chin in mm + rotation in degrees) and
    // the backend computes the resulting predicted SNA/SNB/ANB/Wits/Overjet from the approved
    // CephAnalysis baseline using documented geometric relationships. No soft-tissue prediction
    // is performed — that requires documented clinical ratios (deferred). The mandatory Arabic
    // disclaimer is included in every response so the frontend can render it on every VTO view.
    //
    // STRICT GATE: no VTO is allowed without an approved CephAnalysis. Returning 400 Arabic
    // message if (case.CephAnalysisId == null || !ceph.IsApproved) — per handoff §5/§3.

    private const string VtoDisclaimerAr =
        "هذه محاكاة تخطيطية تقريبية ولا تُعد قرارًا جراحيًا نهائيًا.";
    private const string VtoNoApprovedCephMessageAr =
        "لا يمكن إنشاء محاكاة VTO جراحية بدون تحليل سيفالومتري معتمد";

    [HttpGet("{id:guid}/vto")]
    public async Task<IActionResult> GetVtos(Guid id)
    {
        if (!await CanAsync("view")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var vtos = await db.OrthoSurgicalVtos
            .Where(v => v.OrthoSurgicalCaseId == id && v.IsActive)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id,
                v.OrthoSurgicalCaseId,
                v.CephAnalysisId,
                v.MaxillaMoveMm,
                v.MandibleMoveMm,
                v.ChinMoveMm,
                v.RotationDegree,
                v.PredictedSNA,
                v.PredictedSNB,
                v.PredictedANB,
                v.PredictedWits,
                v.PredictedOverjet,
                v.Notes,
                v.CreatedBy,
                v.IsApprovedByOrthodontist,
                v.ApprovedAt,
                v.ApprovedByUserId,
                CreatedAt = v.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Disclaimer = VtoDisclaimerAr
            })
            .ToListAsync();

        return Ok(new { data = vtos, disclaimer = VtoDisclaimerAr });
    }

    [HttpPost("{id:guid}/vto")]
    public async Task<IActionResult> CreateVto(Guid id, [FromBody] CreateOrthoSurgicalVtoRequest req)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        // ── STRICT GATE: approved CephAnalysis required ──
        if (!c.CephAnalysisId.HasValue)
            return BadRequest(new { message = VtoNoApprovedCephMessageAr });

        var ceph = await db.CephAnalyses
            .Include(a => a.Measurements)
            .FirstOrDefaultAsync(a => a.Id == c.CephAnalysisId && a.IsActive);
        if (ceph is null || !ceph.IsApproved)
            return BadRequest(new { message = VtoNoApprovedCephMessageAr });

        var baseline = LoadBaselineMeasurements(ceph, c.OrthoCaseId).GetAwaiter().GetResult();
        var predicted = ComputePredictedMeasurements(
            req.MaxillaMoveMm, req.MandibleMoveMm, req.ChinMoveMm, req.RotationDegree, baseline);

        var vto = new OrthoSurgicalVto
        {
            OrthoSurgicalCaseId = id,
            CephAnalysisId = c.CephAnalysisId,
            MaxillaMoveMm = req.MaxillaMoveMm,
            MandibleMoveMm = req.MandibleMoveMm,
            ChinMoveMm = req.ChinMoveMm,
            RotationDegree = req.RotationDegree,
            PredictedSNA = predicted.SNA,
            PredictedSNB = predicted.SNB,
            PredictedANB = predicted.ANB,
            PredictedWits = predicted.Wits,
            PredictedOverjet = predicted.Overjet,
            Notes = req.Notes,
            CreatedBy = currentUser.UserId,
            IsApprovedByOrthodontist = false // explicit — never auto-approved
        };
        db.OrthoSurgicalVtos.Add(vto);
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Create, "OrthoSurgicalVto", vto.Id,
            newData: new { orthoSurgicalCaseId = id, cephAnalysisId = c.CephAnalysisId });

        return Ok(BuildVtoDto(vto));
    }

    [HttpPut("{id:guid}/vto/{vtoId:guid}")]
    public async Task<IActionResult> UpdateVto(Guid id, Guid vtoId, [FromBody] UpdateOrthoSurgicalVtoRequest req)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var vto = await db.OrthoSurgicalVtos.FirstOrDefaultAsync(v => v.Id == vtoId && v.OrthoSurgicalCaseId == id && v.IsActive);
        if (vto is null) return NotFound(new { message = "سيناريو المحاكاة غير موجود" });

        // Once the orthodontist has approved a scenario, it becomes immutable (the approved
        // snapshot must not be silently re-shaped by another editor — re-approval is required
        // after a change). Mirrors JointPlan.LockedAt semantics from A5.
        if (vto.IsApprovedByOrthodontist)
            return BadRequest(new { message = "لا يمكن تعديل محاكاة معتمدة — أنشئ سيناريو جديدًا" });

        // Re-load the baseline (it may have changed since creation; we always recompute against
        // the currently linked approved CephAnalysis). The strict gate still applies on update.
        if (!c.CephAnalysisId.HasValue)
            return BadRequest(new { message = VtoNoApprovedCephMessageAr });
        var ceph = await db.CephAnalyses
            .Include(a => a.Measurements)
            .FirstOrDefaultAsync(a => a.Id == c.CephAnalysisId && a.IsActive);
        if (ceph is null || !ceph.IsApproved)
            return BadRequest(new { message = VtoNoApprovedCephMessageAr });

        var baseline = await LoadBaselineMeasurements(ceph, c.OrthoCaseId);
        var predicted = ComputePredictedMeasurements(
            req.MaxillaMoveMm, req.MandibleMoveMm, req.ChinMoveMm, req.RotationDegree, baseline);

        vto.MaxillaMoveMm = req.MaxillaMoveMm;
        vto.MandibleMoveMm = req.MandibleMoveMm;
        vto.ChinMoveMm = req.ChinMoveMm;
        vto.RotationDegree = req.RotationDegree;
        vto.Notes = req.Notes;
        vto.PredictedSNA = predicted.SNA;
        vto.PredictedSNB = predicted.SNB;
        vto.PredictedANB = predicted.ANB;
        vto.PredictedWits = predicted.Wits;
        vto.PredictedOverjet = predicted.Overjet;
        vto.CephAnalysisId = c.CephAnalysisId;
        vto.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "OrthoSurgicalVto", vto.Id,
            newData: new { orthoSurgicalCaseId = id, recomputed = true });

        return Ok(BuildVtoDto(vto));
    }

    [HttpPost("{id:guid}/vto/{vtoId:guid}/approve")]
    public async Task<IActionResult> ApproveVto(Guid id, Guid vtoId)
    {
        if (!await CanAsync("edit")) return Deny();
        // VTO approval is the orthodontist's sign-off on a planning scenario — restricted to
        // Orthodontist + Admin (NOT the oral surgeon, who is consulted via the case-level dual
        // approval flow instead). PatientAccessFilter still applies via DenyIfDoctorCannotAccess.
        if (currentUser.Role != UserRole.Admin && currentUser.Role != UserRole.Orthodontist)
            return StatusCode(403, new { message = "اعتماد سيناريو المحاكاة مقتصر على أخصائي التقويم" });

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var vto = await db.OrthoSurgicalVtos.FirstOrDefaultAsync(v => v.Id == vtoId && v.OrthoSurgicalCaseId == id && v.IsActive);
        if (vto is null) return NotFound(new { message = "سيناريو المحاكاة غير موجود" });

        if (vto.IsApprovedByOrthodontist)
            return BadRequest(new { message = "السيناريو معتمد بالفعل" });

        vto.IsApprovedByOrthodontist = true;
        vto.ApprovedAt = DateTime.UtcNow;
        vto.ApprovedByUserId = currentUser.UserId;
        vto.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Approve, "OrthoSurgicalVto", vto.Id,
            newData: new { orthoSurgicalCaseId = id, approvedBy = currentUser.UserId });

        return Ok(BuildVtoDto(vto));
    }

    [HttpDelete("{id:guid}/vto/{vtoId:guid}")]
    public async Task<IActionResult> DeleteVto(Guid id, Guid vtoId)
    {
        if (!await CanAsync("edit")) return Deny();

        var c = await db.OrthoSurgicalCases.FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
        if (c is null) return NotFound(new { message = "الحالة التقويمية الجراحية غير موجودة" });

        var denied = await DenyIfDoctorCannotAccess(c.PatientId);
        if (denied is not null) return denied;

        var vto = await db.OrthoSurgicalVtos.FirstOrDefaultAsync(v => v.Id == vtoId && v.OrthoSurgicalCaseId == id && v.IsActive);
        if (vto is null) return NotFound(new { message = "سيناريو المحاكاة غير موجود" });

        // Soft-delete only — never hard-delete (history must be preserved per CLAUDE.md "لا حذف
        // حالة لها سجلات"). The global ISoftDeletable query filter hides it from subsequent reads.
        vto.IsActive = false;
        vto.DeletedAt = DateTime.UtcNow;
        vto.DeletedBy = currentUser.UserId;
        vto.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.LogAsync(AuditAction.Delete, "OrthoSurgicalVto", vto.Id,
            newData: new { orthoSurgicalCaseId = id, softDeleted = true });

        return Ok(new { id = vto.Id, deleted = true });
    }

    // Loads baseline SNA/SNB/Wits/Overjet from the approved CephAnalysis.Measurements
    // (canonical keys "SNA"/"SNB"/"Wits"/"Overjet" — produced by CephService.SteinerAnalysis),
    // falling back to the OrthoDiagnosis snapshot (SNA/SNB/Wits) and the latest OrthoClinicalExam
    // (Overjet) when a measurement is not stored on the analysis. Returns nulls gracefully — the
    // caller's predicted value then stays null and the UI shows "—".
    private async Task<BaselineCeph> LoadBaselineMeasurements(CephAnalysis ceph, Guid orthoCaseId)
    {
        static decimal? Find(CephAnalysis a, string name) =>
            a.Measurements?.FirstOrDefault(m => m.MeasurementName == name && m.IsActive)?.MeasurementValue;

        var baseline = new BaselineCeph
        {
            SNA = Find(ceph, "SNA"),
            SNB = Find(ceph, "SNB"),
            Wits = Find(ceph, "Wits"),
            Overjet = Find(ceph, "Overjet")
        };

        // Fallbacks for missing baseline values.
        if (baseline.SNA is null || baseline.SNB is null || baseline.Wits is null)
        {
            var diagnosis = await db.OrthoDiagnoses.AsNoTracking()
                .FirstOrDefaultAsync(d => d.OrthoCaseId == orthoCaseId);
            if (diagnosis is not null)
            {
                baseline.SNA ??= diagnosis.SNA;
                baseline.SNB ??= diagnosis.SNB;
                baseline.Wits ??= diagnosis.Wits;
            }
        }
        if (baseline.Overjet is null)
        {
            var exam = await db.OrthoClinicalExams.AsNoTracking()
                .Where(e => e.OrthoCaseId == orthoCaseId)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync();
            baseline.Overjet = exam?.Overjet;
        }

        return baseline;
    }

    // ── Predicted-measurement computation (Sprint A9) ────────────────────────────────
    // DOCUMENTED GEOMETRIC RELATIONSHIPS — NOT invented coefficients. Every rule has a cited
    // source. These are linear approximations of the standard orthognathic planning charts
    // (good to ~0.5°/0.5 mm in the typical ±10 mm movement range); they are a PLANNING AID,
    // not a surgical decision — the mandatory Arabic disclaimer must accompany every output.
    //
    // 1) Maxilla advancement +X mm → SNA increases ~1° per 2 mm.
    //    Source: Bishara S.E., "Textbook of Orthodontics", W.B. Saunders (2001), Ch. 23 —
    //    antero-posterior maxillary movement shifts point A roughly 1° per 2 mm along the N-A
    //    line at typical Sella-Nasion distances. Setback is the negative direction.
    //
    // 2) Mandible advancement +X mm → SNB increases ~1° per 2 mm (Bishara, same chapter).
    //    Mandibular setback reduces SNB at the same rate.
    //
    // 3) ANB = SNA − SNB (derived — Steiner's classic identity; always true by definition).
    //
    // 4) Wits shifts proportionally with jaw movement along the functional occlusal plane:
    //    maxillary advancement moves point A forward (Wits increases); mandibular advancement
    //    moves point B forward (Wits decreases). Coefficient ≈ 0.5 mm per 1 mm of AP jaw
    //    movement (i.e. half the movement is reflected in the Wits projection along the
    //    occlusal plane).
    //    Source: Jacobson A. "The 'Wits' appraisal of jaw disharmony." Am J Orthod 67(2):125-38,
    //    1975 — re-evaluated in Jacobson 1988 for occlusal-plane projection geometry.
    //
    // 5) Overjet decreases with maxillary advancement and mandibular advancement (per the
    //    A9 sprint spec — see docs/ortho-module/ORTHO_SURGICAL_A9_A11_HANDOFF.md §5). The
    //    1:1 mm coefficient reflects direct AP projection of incisor position with jaw movement.
    //
    // 6) Chin movement (genioplasty) and occlusal-plane rotation DO NOT change SNA/SNB/ANB/Wits
    //    (point B and pogonion are different landmarks; genioplasty moves only the chin segment).
    //    Rotation affects overjet only via the autorotation effect, which is not modeled here
    //    (would require documented ratios). These fields therefore do not contribute to the
    //    predicted cephalometric values — they are stored for the record only.
    private static (decimal? SNA, decimal? SNB, decimal? ANB, decimal? Wits, decimal? Overjet)
        ComputePredictedMeasurements(
            decimal? maxillaMoveMm, decimal? mandibleMoveMm,
            decimal? chinMoveMm, decimal? rotationDegree,
            BaselineCeph baseline)
    {
        // SNA — maxillary movement only.
        decimal? predictedSna = baseline.SNA;
        if (predictedSna is not null && maxillaMoveMm is not null)
            predictedSna = baseline.SNA + (maxillaMoveMm.Value / 2m);

        // SNB — mandibular movement only.
        decimal? predictedSnb = baseline.SNB;
        if (predictedSnb is not null && mandibleMoveMm is not null)
            predictedSnb = baseline.SNB + (mandibleMoveMm.Value / 2m);

        // ANB = SNA − SNB (derived). Requires both predicted SNA and SNB to be available.
        decimal? predictedAnb = null;
        if (predictedSna is not null && predictedSnb is not null)
            predictedAnb = predictedSna - predictedSnb;

        // Wits — maxillary advancement increases Wits; mandibular advancement decreases Wits
        // (point B moves forward, narrowing the A-B projection on the occlusal plane).
        decimal? predictedWits = baseline.Wits;
        if (predictedWits is not null)
        {
            var delta = 0m;
            if (maxillaMoveMm is not null) delta += maxillaMoveMm.Value * 0.5m;
            if (mandibleMoveMm is not null) delta -= mandibleMoveMm.Value * 0.5m;
            predictedWits = baseline.Wits + delta;
        }

        // Overjet — decreases with both maxillary and mandibular advancement (A9 spec §5).
        decimal? predictedOverjet = baseline.Overjet;
        if (predictedOverjet is not null)
        {
            var delta = 0m;
            if (maxillaMoveMm is not null) delta += maxillaMoveMm.Value;
            if (mandibleMoveMm is not null) delta += mandibleMoveMm.Value;
            predictedOverjet = baseline.Overjet - delta;
        }

        return (predictedSna, predictedSnb, predictedAnb, predictedWits, predictedOverjet);
    }

    private object BuildVtoDto(OrthoSurgicalVto v) => new
    {
        v.Id,
        v.OrthoSurgicalCaseId,
        v.CephAnalysisId,
        v.MaxillaMoveMm,
        v.MandibleMoveMm,
        v.ChinMoveMm,
        v.RotationDegree,
        v.PredictedSNA,
        v.PredictedSNB,
        v.PredictedANB,
        v.PredictedWits,
        v.PredictedOverjet,
        v.Notes,
        v.CreatedBy,
        v.IsApprovedByOrthodontist,
        v.ApprovedAt,
        v.ApprovedByUserId,
        CreatedAt = v.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        Disclaimer = VtoDisclaimerAr
    };

    // Plain baseline carrier used by ComputePredictedMeasurements — kept as a private nested
    // record so the math above reads as plain field arithmetic (no dictionary lookups in the
    // hot path) and the unit tests can construct one directly.
    private sealed class BaselineCeph
    {
        public decimal? SNA { get; set; }
        public decimal? SNB { get; set; }
        public decimal? Wits { get; set; }
        public decimal? Overjet { get; set; }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────
    private static string ResponsiblePartyLabel(OrthoSurgicalStatus status) => status switch
    {
        OrthoSurgicalStatus.DraftByOrthodontist => "أخصائي التقويم",
        OrthoSurgicalStatus.RecordsIncomplete => "أخصائي التقويم",
        OrthoSurgicalStatus.CephReady => "أخصائي التقويم",
        OrthoSurgicalStatus.VtoDraft => "أخصائي التقويم",
        OrthoSurgicalStatus.SentToSurgeon => "أخصائي الجراحة",
        OrthoSurgicalStatus.SurgeonReviewPending => "أخصائي الجراحة",
        OrthoSurgicalStatus.SurgeonRequestedChanges => "أخصائي التقويم",
        OrthoSurgicalStatus.JointPlanApproved => "الطرفان",
        OrthoSurgicalStatus.ReadyForSurgery => "أخصائي الجراحة",
        OrthoSurgicalStatus.SurgeryScheduled => "أخصائي الجراحة",
        OrthoSurgicalStatus.SurgeryDone => "أخصائي التقويم",
        OrthoSurgicalStatus.PostOpOrthodontics => "أخصائي التقويم",
        OrthoSurgicalStatus.Completed => "—",
        OrthoSurgicalStatus.NotSurgicalCandidate => "—",
        OrthoSurgicalStatus.Cancelled => "—",
        _ => "—"
    };

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: "23505" };
}
