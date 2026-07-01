using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
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
        if (c.OrthodontistApprovedAt is not null && c.SurgeonApprovedAt is not null)
        {
            if (c.JointPlan is not null && c.JointPlan.LockedAt is null)
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
