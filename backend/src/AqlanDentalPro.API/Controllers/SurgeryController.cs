using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateSurgeryCaseRequest
{
    public Guid PatientId { get; init; }
    public Guid? DoctorId { get; init; }
    public string SurgeryType { get; init; } = string.Empty;
    public string? TeethInvolved { get; init; }
    public string? ScheduledDate { get; init; }
}

public sealed class CreateSurgeryCaseRequestValidator : AbstractValidator<CreateSurgeryCaseRequest>
{
    public CreateSurgeryCaseRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.SurgeryType)
            .NotEmpty().WithMessage("نوع الجراحة مطلوب")
            .MaximumLength(200).WithMessage("نوع الجراحة يجب ألا يتجاوز 200 حرف");

        RuleFor(x => x.ScheduledDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الجراحة غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.ScheduledDate));
    }
}

public sealed class UpdateSurgeryStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed class UpsertPreopRequest
{
    public string? SurgeryDate { get; init; }
    public string? SurgeryLocation { get; init; }
    public string? AnesthesiaType { get; init; }
    public bool ConsentSigned { get; init; }
    public Guid? DoctorId { get; init; }
    public Dictionary<string, bool>? Checklist { get; init; }
    public List<string>? RequiredTests { get; init; }
}

public sealed class PrescriptionItemDto
{
    public string? Medicine { get; init; }
    public string? Dosage { get; init; }
    public string? Frequency { get; init; }
    public string? Duration { get; init; }
}

public sealed class FollowupItemDto
{
    public string? Date { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpsertPostopRequest
{
    public string? Instructions { get; init; }
    public List<PrescriptionItemDto>? Prescription { get; init; }
    public List<FollowupItemDto>? FollowupSchedule { get; init; }
}

public sealed class UpdateSurgeryStatusRequestValidator : AbstractValidator<UpdateSurgeryStatusRequest>
{
    private static readonly HashSet<string> ValidStatuses =
        ["scheduled", "in_progress", "completed", "cancelled", "postponed"];

    public UpdateSurgeryStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("الحالة مطلوبة")
            .Must(s => ValidStatuses.Contains(s)).WithMessage("الحالة غير صالحة");
    }
}

public sealed class UpsertOperativeReportRequest
{
    public string? SurgeryDateTime { get; init; }
    public int? DurationMinutes { get; init; }
    public string? AnesthesiaUsed { get; init; }
    public string? Technique { get; init; }
    public string? DetailedDescription { get; init; }
    public string? Outcome { get; init; }
    public string? Complications { get; init; }
    public int? SuturesCount { get; init; }
    public bool SpecimenSent { get; init; }
    public Guid? DoctorId { get; init; }
}

public sealed class UpsertOperativeReportRequestValidator : AbstractValidator<UpsertOperativeReportRequest>
{
    public UpsertOperativeReportRequestValidator()
    {
        RuleFor(x => x.DurationMinutes)
            .GreaterThanOrEqualTo(0).WithMessage("المدة يجب أن تكون قيمة موجبة")
            .When(x => x.DurationMinutes.HasValue);
        RuleFor(x => x.SuturesCount)
            .GreaterThanOrEqualTo(0).WithMessage("عدد الغرز يجب أن يكون قيمة موجبة")
            .When(x => x.SuturesCount.HasValue);
    }
}

public sealed class CreateHospitalReferralRequest
{
    public string? HospitalName { get; init; }
    public string? Reason { get; init; }
    public string? ReferralDate { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateHospitalReferralRequest
{
    public string? HospitalName { get; init; }
    public string? Reason { get; init; }
    public string? ReferralDate { get; init; }
    public string? Status { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateHospitalReferralRequestValidator : AbstractValidator<UpdateHospitalReferralRequest>
{
    private static readonly HashSet<string> ValidStatuses = ["pending", "in_progress", "completed", "cancelled"];
    public UpdateHospitalReferralRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => string.IsNullOrEmpty(s) || ValidStatuses.Contains(s))
            .WithMessage("حالة الإحالة غير صالحة");
    }
}

public sealed class UpdateSurgeryCaseRequest
{
    public Guid? DoctorId { get; init; }
    public string? SurgeryType { get; init; }
    public string? TeethInvolved { get; init; }
}

public sealed class UpdateSurgeryCaseRequestValidator : AbstractValidator<UpdateSurgeryCaseRequest>
{
    public UpdateSurgeryCaseRequestValidator()
    {
        RuleFor(x => x.SurgeryType)
            .MaximumLength(200).WithMessage("نوع الجراحة يجب ألا يتجاوز 200 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.SurgeryType));
    }
}

[ApiController]
[Route("api/surgery-cases")]
[Authorize(Policy = "SurgeryAccess")]
[ServiceFilter(typeof(PatientAccessFilter))]
public class SurgeryController(
    AppDbContext db,
    ILogger<SurgeryController> logger,
    IPatientAccessService patientAccess,
    IAuditService audit,
    ICurrentUserService currentUser) : ControllerBase
{
    // CLIN-01: Per-patient access check for actions where patientId is in body or inferred.
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "SurgeryCase", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? doctorId,
        [FromQuery] string? search,
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.SurgeryCases
            .Include(s => s.Patient)
            .Include(s => s.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SurgeryCaseStatus>(status, true, out var surgeryStatus))
            query = query.Where(s => s.Status == surgeryStatus);
        if (doctorId.HasValue) query = query.Where(s => s.DoctorId == doctorId);
        if (patientId.HasValue) query = query.Where(s => s.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(s =>
                s.CaseNumber.ToLower().Contains(term) ||
                (s.Patient.FirstName + " " + s.Patient.LastName).ToLower().Contains(term) ||
                s.Patient.PatientNumber.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var cases = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.CaseNumber,
                s.PatientId,
                PatientName    = s.Patient.FirstName + " " + s.Patient.LastName,
                PatientNumber  = s.Patient.PatientNumber,
                DoctorName     = s.Doctor != null ? s.Doctor.Name : null,
                DoctorColor    = s.Doctor != null ? s.Doctor.Color : null,
                s.SurgeryType,
                s.TeethInvolved,
                s.Status,
                CreatedAt      = s.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = cases, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var surgery = await db.SurgeryCases
            .Include(s => s.Patient)
            .Include(s => s.Doctor)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        // CLIN-01: per-patient check after loading the entity.
        var denied = await DenyIfDoctorCannotAccess(surgery.PatientId);
        if (denied is not null) return denied;

        return Ok(new
        {
            surgery.Id,
            surgery.CaseNumber,
            surgery.PatientId,
            PatientName   = surgery.Patient.FirstName + " " + surgery.Patient.LastName,
            PatientNumber = surgery.Patient.PatientNumber,
            DoctorName    = surgery.Doctor?.Name,
            DoctorColor   = surgery.Doctor?.Color,
            surgery.SurgeryType,
            surgery.TeethInvolved,
            surgery.Status,
            CreatedAt     = surgery.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSurgeryCaseRequest req)
    {
        // CLIN-01: per-patient check before creating.
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        const int maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // CON FIX: Advisory lock to prevent race condition on case number generation
                var lockKey = Math.Abs("SurgeryCaseNumber".GetHashCode()) % 100000;
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

                var year  = DateTime.UtcNow.Year;
                var count = await db.SurgeryCases.IgnoreQueryFilters()
                    .CountAsync(c => c.CaseNumber.StartsWith($"SU-{year}-"));

                var surgery = new SurgeryCase
                {
                    CaseNumber    = $"SU-{year}-{(count + 1):D3}",
                    PatientId     = req.PatientId,
                    DoctorId      = req.DoctorId,
                    SurgeryType   = req.SurgeryType,
                    TeethInvolved = req.TeethInvolved,
                    Status        = SurgeryCaseStatus.Scheduled
                };

                db.SurgeryCases.Add(surgery);

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

                return CreatedAtAction(nameof(GetById), new { id = surgery.Id },
                    new { surgery.Id, surgery.CaseNumber });
            }
            catch (Exception ex) when (ex is not DbUpdateException)
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        return StatusCode(500, new { message = "فشل إنشاء حالة الجراحة بعد عدة محاولات" });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSurgeryStatusRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        // CLIN-01: per-patient check before updating status.
        var denied = await DenyIfDoctorCannotAccess(surgery.PatientId);
        if (denied is not null) return denied;

        if (!Enum.TryParse<SurgeryCaseStatus>(req.Status, true, out var surgeryStatus))
            return BadRequest(new { message = "حالة الجراحة غير صالحة" });

        // CLIN-03: Validate the transition before applying. Previously any status could
        // jump to any other (e.g., Completed → Scheduled, Cancelled → InProgress), corrupting
        // the audit trail and leaving postop reports attached to a "scheduled" case.
        var transitionError = SurgeryCaseStatusTransitions.GetValidationError(surgery.Status, surgeryStatus);
        if (transitionError is not null)
            return BadRequest(new { message = transitionError });

        surgery.Status = surgeryStatus;
        await db.SaveChangesAsync();
        return Ok(new { id, status = req.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        // CLIN-01: per-patient check before deleting.
        var denied = await DenyIfDoctorCannotAccess(surgery.PatientId);
        if (denied is not null) return denied;

        surgery.IsActive = false;
        surgery.DeletedAt = DateTime.UtcNow;

        // M3 FIX: Cascade soft-delete to child records
        var preop = await db.PreopReports.FirstOrDefaultAsync(r => r.SurgeryCaseId == id && r.IsActive);
        if (preop != null) { preop.IsActive = false; preop.DeletedAt = DateTime.UtcNow; }

        var operative = await db.OperativeReports.FirstOrDefaultAsync(r => r.SurgeryCaseId == id && r.IsActive);
        if (operative != null) { operative.IsActive = false; operative.DeletedAt = DateTime.UtcNow; }

        var postop = await db.PostopRecords.FirstOrDefaultAsync(r => r.SurgeryCaseId == id && r.IsActive);
        if (postop != null) { postop.IsActive = false; postop.DeletedAt = DateTime.UtcNow; }

        var referrals = await db.HospitalReferrals.Where(r => r.SurgeryCaseId == id && r.IsActive).ToListAsync();
        foreach (var r in referrals) { r.IsActive = false; r.DeletedAt = DateTime.UtcNow; }

        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الحالة والسجلات المرتبطة بنجاح" });
    }

    [HttpGet("{id:guid}/preop")]
    public async Task<IActionResult> GetPreop(Guid id)
    {
        var report = await db.PreopReports
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.SurgeryCaseId == id);
        if (report is null)
            return Ok(null);
        return Ok(new {
            report.Id,
            SurgeryDate     = report.SurgeryDate?.ToString("yyyy-MM-dd"),
            report.SurgeryLocation,
            report.AnesthesiaType,
            report.Checklist,
            report.RequiredTests,
            report.ConsentSigned,
            DoctorName      = report.Doctor?.Name,
            report.DoctorId,
        });
    }

    [HttpPut("{id:guid}/preop")]
    public async Task<IActionResult> UpsertPreop(Guid id, [FromBody] UpsertPreopRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.PreopReports.FirstOrDefaultAsync(p => p.SurgeryCaseId == id);
        if (existing is null)
        {
            existing = new PreopReport { SurgeryCaseId = id };
            db.PreopReports.Add(existing);
        }

        existing.SurgeryDate     = req.SurgeryDate != null ? DateOnly.Parse(req.SurgeryDate) : null;
        existing.SurgeryLocation = req.SurgeryLocation;
        existing.AnesthesiaType  = req.AnesthesiaType;
        existing.Checklist = req.Checklist != null
            ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(req.Checklist))
            : null;
        existing.RequiredTests = req.RequiredTests != null
            ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(req.RequiredTests))
            : null;
        existing.ConsentSigned   = req.ConsentSigned;
        existing.DoctorId        = req.DoctorId;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم الحفظ" });
    }

    [HttpGet("{id:guid}/postop")]
    public async Task<IActionResult> GetPostop(Guid id)
    {
        var record = await db.PostopRecords.FirstOrDefaultAsync(p => p.SurgeryCaseId == id);
        if (record is null) return Ok(null);
        return Ok(new {
            record.Id,
            record.Instructions,
            record.Prescription,
            record.FollowupSchedule,
        });
    }

    [HttpPut("{id:guid}/postop")]
    public async Task<IActionResult> UpsertPostop(Guid id, [FromBody] UpsertPostopRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.PostopRecords.FirstOrDefaultAsync(p => p.SurgeryCaseId == id);
        if (existing is null)
        {
            existing = new PostopRecord { SurgeryCaseId = id };
            db.PostopRecords.Add(existing);
        }
        existing.Instructions = req.Instructions;
        existing.Prescription = req.Prescription != null
            ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(req.Prescription))
            : null;
        existing.FollowupSchedule = req.FollowupSchedule != null
            ? JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(req.FollowupSchedule))
            : null;
        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم الحفظ" });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSurgeryCaseRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        if (req.DoctorId.HasValue) surgery.DoctorId = req.DoctorId;
        if (req.SurgeryType is not null) surgery.SurgeryType = req.SurgeryType;
        if (req.TeethInvolved is not null) surgery.TeethInvolved = req.TeethInvolved;

        await db.SaveChangesAsync();
        return Ok(new { id = surgery.Id, message = "تم التحديث" });
    }

    [HttpGet("{id:guid}/operative")]
    public async Task<IActionResult> GetOperativeReport(Guid id)
    {
        var report = await db.OperativeReports
            .Include(r => r.Doctor)
            .FirstOrDefaultAsync(r => r.SurgeryCaseId == id);
        if (report is null) return Ok(null);
        return Ok(new {
            report.Id,
            SurgeryDateTime = report.SurgeryDateTime?.ToString("yyyy-MM-ddTHH:mm"),
            report.DurationMinutes,
            report.AnesthesiaUsed,
            report.Technique,
            report.DetailedDescription,
            report.Outcome,
            report.Complications,
            report.SuturesCount,
            report.SpecimenSent,
            DoctorName = report.Doctor?.Name,
            report.DoctorId,
            ApprovedAt = report.ApprovedAt?.ToString("yyyy-MM-ddTHH:mm"),
        });
    }

    [HttpPut("{id:guid}/operative")]
    public async Task<IActionResult> UpsertOperativeReport(Guid id, [FromBody] UpsertOperativeReportRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.OperativeReports.FirstOrDefaultAsync(r => r.SurgeryCaseId == id);
        if (existing is null)
        {
            existing = new OperativeReport { SurgeryCaseId = id };
            db.OperativeReports.Add(existing);
        }

        existing.SurgeryDateTime = !string.IsNullOrEmpty(req.SurgeryDateTime) ? DateTime.Parse(req.SurgeryDateTime) : null;
        existing.DurationMinutes = req.DurationMinutes;
        existing.AnesthesiaUsed = req.AnesthesiaUsed;
        existing.Technique = req.Technique;
        existing.DetailedDescription = req.DetailedDescription;
        existing.Outcome = req.Outcome;
        existing.Complications = req.Complications;
        existing.SuturesCount = req.SuturesCount;
        existing.SpecimenSent = req.SpecimenSent;
        existing.DoctorId = req.DoctorId;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم الحفظ" });
    }

    [HttpPut("{id:guid}/operative/approve")]
    public async Task<IActionResult> ApproveOperativeReport(Guid id)
    {
        var report = await db.OperativeReports.FirstOrDefaultAsync(r => r.SurgeryCaseId == id);
        if (report is null) return NotFound(new { message = "التقرير الجراحي غير موجود" });

        report.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { report.Id, ApprovedAt = report.ApprovedAt.Value.ToString("yyyy-MM-ddTHH:mm"), message = "تم اعتماد التقرير" });
    }

    [HttpGet("{id:guid}/referrals")]
    public async Task<IActionResult> GetReferrals(Guid id)
    {
        var referrals = await db.HospitalReferrals
            .Where(r => r.SurgeryCaseId == id)
            .OrderByDescending(r => r.ReferralDate)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new {
                r.Id,
                r.HospitalName,
                r.Reason,
                ReferralDate = r.ReferralDate != null ? r.ReferralDate.Value.ToString("yyyy-MM-dd") : null,
                r.Status,
                r.Notes,
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();
        return Ok(referrals);
    }

    [HttpPost("{id:guid}/referrals")]
    public async Task<IActionResult> CreateReferral(Guid id, [FromBody] CreateHospitalReferralRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة غير موجودة" });

        var referral = new HospitalReferral
        {
            SurgeryCaseId = id,
            HospitalName = req.HospitalName,
            Reason = req.Reason,
            ReferralDate = !string.IsNullOrEmpty(req.ReferralDate) ? DateOnly.Parse(req.ReferralDate) : null,
            Status = "pending",
            Notes = req.Notes,
        };

        db.HospitalReferrals.Add(referral);
        await db.SaveChangesAsync();
        return Ok(new { referral.Id, message = "تم إنشاء الإحالة" });
    }

    [HttpPut("{id:guid}/referrals/{referralId:guid}")]
    public async Task<IActionResult> UpdateReferral(Guid id, Guid referralId, [FromBody] UpdateHospitalReferralRequest req)
    {
        var referral = await db.HospitalReferrals.FirstOrDefaultAsync(r => r.Id == referralId && r.SurgeryCaseId == id);
        if (referral is null) return NotFound(new { message = "الإحالة غير موجودة" });

        if (req.HospitalName is not null) referral.HospitalName = req.HospitalName;
        if (req.Reason is not null) referral.Reason = req.Reason;
        if (req.ReferralDate is not null) referral.ReferralDate = DateOnly.Parse(req.ReferralDate);
        if (req.Status is not null) referral.Status = req.Status;
        if (req.Notes is not null) referral.Notes = req.Notes;

        await db.SaveChangesAsync();
        return Ok(new { referral.Id, message = "تم تحديث الإحالة" });
    }

    [HttpDelete("{id:guid}/referrals/{referralId:guid}")]
    public async Task<IActionResult> DeleteReferral(Guid id, Guid referralId)
    {
        var referral = await db.HospitalReferrals.FirstOrDefaultAsync(r => r.Id == referralId && r.SurgeryCaseId == id);
        if (referral is null) return NotFound(new { message = "الإحالة غير موجودة" });

        referral.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الإحالة" });
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is NpgsqlException pgEx && pgEx.SqlState == "23505";
}
