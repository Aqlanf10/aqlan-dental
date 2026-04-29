using AqlanDentalPro.Application.DTOs.Surgery;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
}

public sealed class UpsertPostopRequest
{
    public string? Instructions { get; init; }
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

public sealed class UpsertOperativeReportRequestValidator : AbstractValidator<UpsertOperativeReportRequest>
{
    public UpsertOperativeReportRequestValidator()
    {
        RuleFor(x => x.SurgeryDateTime)
            .Must(d => DateTime.TryParse(d, out _)).WithMessage("تنسيق تاريخ ووقت الجراحة غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.SurgeryDateTime));

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(1, 600).WithMessage("مدة الجراحة يجب أن تكون بين 1 و 600 دقيقة")
            .When(x => x.DurationMinutes.HasValue);

        RuleFor(x => x.SuturesCount)
            .InclusiveBetween(0, 50).WithMessage("عدد الغرز يجب أن يكون بين 0 و 50")
            .When(x => x.SuturesCount.HasValue);
    }
}

public sealed class CreateHospitalReferralRequestValidator : AbstractValidator<CreateHospitalReferralRequest>
{
    public CreateHospitalReferralRequestValidator()
    {
        RuleFor(x => x.HospitalName)
            .NotEmpty().WithMessage("اسم المستشفى مطلوب")
            .MaximumLength(300).WithMessage("اسم المستشفى يجب ألا يتجاوز 300 حرف")
            .When(x => x.HospitalName is not null);

        RuleFor(x => x.ReferralDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق تاريخ الإحالة غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.ReferralDate));
    }
}

public sealed class UpdateHospitalReferralStatusRequestValidator : AbstractValidator<UpdateHospitalReferralStatusRequest>
{
    private static readonly HashSet<string> ValidStatuses =
        ["pending", "accepted", "scheduled", "completed", "cancelled"];

    public UpdateHospitalReferralStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("الحالة مطلوبة")
            .Must(s => ValidStatuses.Contains(s)).WithMessage("الحالة غير صالحة");
    }
}

[ApiController]
[Route("api/surgery-cases")]
[Authorize(Policy = "SurgeryAccess")]
public class SurgeryController(AppDbContext db, INotificationService notificationService) : ControllerBase
{
    // ── Surgery Cases CRUD ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? doctorId,
        [FromQuery] string? search,
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.SurgeryCases
            .Include(s => s.Patient)
            .Include(s => s.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status);
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
            .Include(s => s.PreopReport)
            .Include(s => s.OperativeReport).ThenInclude(o => o!.Doctor)
            .Include(s => s.PostopRecord)
            .Include(s => s.HospitalReferrals)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

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
            CreatedAt     = surgery.CreatedAt.ToString("yyyy-MM-dd"),

            // Preop report
            PreopReport = surgery.PreopReport != null ? new
            {
                surgery.PreopReport.Id,
                SurgeryDate     = surgery.PreopReport.SurgeryDate?.ToString("yyyy-MM-dd"),
                surgery.PreopReport.SurgeryLocation,
                surgery.PreopReport.AnesthesiaType,
                surgery.PreopReport.ConsentSigned,
                DoctorName      = surgery.PreopReport.Doctor?.Name,
                surgery.PreopReport.DoctorId,
            } : null,

            // Operative report
            OperativeReport = surgery.OperativeReport != null ? new
            {
                surgery.OperativeReport.Id,
                SurgeryDateTime = surgery.OperativeReport.SurgeryDateTime?.ToString("yyyy-MM-dd HH:mm"),
                surgery.OperativeReport.DurationMinutes,
                surgery.OperativeReport.AnesthesiaUsed,
                surgery.OperativeReport.Technique,
                surgery.OperativeReport.DetailedDescription,
                surgery.OperativeReport.Outcome,
                surgery.OperativeReport.Complications,
                surgery.OperativeReport.SuturesCount,
                surgery.OperativeReport.SpecimenSent,
                DoctorName       = surgery.OperativeReport.Doctor?.Name,
                surgery.OperativeReport.DoctorId,
                ApprovedAt       = surgery.OperativeReport.ApprovedAt?.ToString("yyyy-MM-dd HH:mm"),
            } : null,

            // Postop record
            PostopRecord = surgery.PostopRecord != null ? new
            {
                surgery.PostopRecord.Id,
                surgery.PostopRecord.Instructions,
            } : null,

            // Hospital referrals
            HospitalReferrals = surgery.HospitalReferrals.Select(r => new
            {
                r.Id,
                r.HospitalName,
                r.Reason,
                ReferralDate = r.ReferralDate?.ToString("yyyy-MM-dd"),
                r.Status,
                r.Notes,
                CreatedAt     = r.CreatedAt.ToString("yyyy-MM-dd"),
            }).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSurgeryCaseRequest req)
    {
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
            Status        = "scheduled"
        };

        db.SurgeryCases.Add(surgery);
        await db.SaveChangesAsync();

        await notificationService.NotifyUserAsync(req.DoctorId ?? Guid.Empty, "surgery", "حالة جراحية جديدة", $"تم إنشاء حالة جراحية جديدة: {surgery.CaseNumber}", "surgery-cases", surgery.Id);

        return CreatedAtAction(nameof(GetById), new { id = surgery.Id },
            new { surgery.Id, surgery.CaseNumber });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSurgeryStatusRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        surgery.Status = req.Status;
        await db.SaveChangesAsync();
        return Ok(new { id, status = req.Status });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        surgery.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الحالة بنجاح" });
    }

    // ── Preop Report ────────────────────────────────────────────────────────────

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
        existing.ConsentSigned   = req.ConsentSigned;
        existing.DoctorId        = req.DoctorId;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم الحفظ" });
    }

    // ── Operative Report ────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/operative-report")]
    public async Task<IActionResult> GetOperativeReport(Guid id)
    {
        var report = await db.OperativeReports
            .Include(o => o.Doctor)
            .FirstOrDefaultAsync(o => o.SurgeryCaseId == id);

        if (report is null) return Ok(null);

        return Ok(new OperativeReportDto
        {
            Id = report.Id,
            SurgeryCaseId = report.SurgeryCaseId,
            SurgeryDateTime = report.SurgeryDateTime?.ToString("yyyy-MM-dd HH:mm"),
            DurationMinutes = report.DurationMinutes,
            AnesthesiaUsed = report.AnesthesiaUsed,
            Technique = report.Technique,
            DetailedDescription = report.DetailedDescription,
            Outcome = report.Outcome,
            Complications = report.Complications,
            SuturesCount = report.SuturesCount,
            SpecimenSent = report.SpecimenSent,
            DoctorName = report.Doctor?.Name,
            DoctorId = report.DoctorId,
            ApprovedAt = report.ApprovedAt?.ToString("yyyy-MM-dd HH:mm"),
            CreatedAt = report.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost("{id:guid}/operative-report")]
    public async Task<IActionResult> CreateOperativeReport(
        Guid id,
        [FromBody] UpsertOperativeReportRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        var existing = await db.OperativeReports.FirstOrDefaultAsync(o => o.SurgeryCaseId == id);
        if (existing is not null)
            return BadRequest(new { message = "يوجد تقرير جراحي بالفعل، استخدم التحديث بدلاً من الإنشاء" });

        var report = new OperativeReport
        {
            SurgeryCaseId = id,
            SurgeryDateTime = req.SurgeryDateTime != null ? DateTime.Parse(req.SurgeryDateTime) : null,
            DurationMinutes = req.DurationMinutes,
            AnesthesiaUsed = req.AnesthesiaUsed,
            Technique = req.Technique,
            DetailedDescription = req.DetailedDescription,
            Outcome = req.Outcome,
            Complications = req.Complications,
            SuturesCount = req.SuturesCount,
            SpecimenSent = req.SpecimenSent ?? false,
            DoctorId = req.DoctorId
        };

        db.OperativeReports.Add(report);
        await db.SaveChangesAsync();

        await db.Entry(report).Reference(o => o.Doctor).LoadAsync();

        return Ok(new OperativeReportDto
        {
            Id = report.Id,
            SurgeryCaseId = report.SurgeryCaseId,
            SurgeryDateTime = report.SurgeryDateTime?.ToString("yyyy-MM-dd HH:mm"),
            DurationMinutes = report.DurationMinutes,
            AnesthesiaUsed = report.AnesthesiaUsed,
            Technique = report.Technique,
            DetailedDescription = report.DetailedDescription,
            Outcome = report.Outcome,
            Complications = report.Complications,
            SuturesCount = report.SuturesCount,
            SpecimenSent = report.SpecimenSent,
            DoctorName = report.Doctor?.Name,
            DoctorId = report.DoctorId,
            CreatedAt = report.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPut("{id:guid}/operative-report")]
    public async Task<IActionResult> UpdateOperativeReport(
        Guid id,
        [FromBody] UpsertOperativeReportRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        var report = await db.OperativeReports
            .Include(o => o.Doctor)
            .FirstOrDefaultAsync(o => o.SurgeryCaseId == id);

        if (report is null)
            return NotFound(new { message = "التقرير الجراحي غير موجود، استخدم الإنشاء أولاً" });

        if (report.ApprovedAt.HasValue)
            return BadRequest(new { message = "لا يمكن تعديل تقرير جراحي معتمد" });

        if (req.SurgeryDateTime is not null)
            report.SurgeryDateTime = DateTime.Parse(req.SurgeryDateTime);
        if (req.DurationMinutes.HasValue) report.DurationMinutes = req.DurationMinutes;
        if (req.AnesthesiaUsed is not null) report.AnesthesiaUsed = req.AnesthesiaUsed;
        if (req.Technique is not null) report.Technique = req.Technique;
        if (req.DetailedDescription is not null) report.DetailedDescription = req.DetailedDescription;
        if (req.Outcome is not null) report.Outcome = req.Outcome;
        if (req.Complications is not null) report.Complications = req.Complications;
        if (req.SuturesCount.HasValue) report.SuturesCount = req.SuturesCount;
        if (req.SpecimenSent.HasValue) report.SpecimenSent = req.SpecimenSent.Value;
        if (req.DoctorId.HasValue) report.DoctorId = req.DoctorId;

        await db.SaveChangesAsync();

        await db.Entry(report).Reference(o => o.Doctor).LoadAsync();

        return Ok(new OperativeReportDto
        {
            Id = report.Id,
            SurgeryCaseId = report.SurgeryCaseId,
            SurgeryDateTime = report.SurgeryDateTime?.ToString("yyyy-MM-dd HH:mm"),
            DurationMinutes = report.DurationMinutes,
            AnesthesiaUsed = report.AnesthesiaUsed,
            Technique = report.Technique,
            DetailedDescription = report.DetailedDescription,
            Outcome = report.Outcome,
            Complications = report.Complications,
            SuturesCount = report.SuturesCount,
            SpecimenSent = report.SpecimenSent,
            DoctorName = report.Doctor?.Name,
            DoctorId = report.DoctorId,
            ApprovedAt = report.ApprovedAt?.ToString("yyyy-MM-dd HH:mm"),
            CreatedAt = report.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPut("{id:guid}/operative-report/approve")]
    public async Task<IActionResult> ApproveOperativeReport(Guid id)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        var report = await db.OperativeReports.FirstOrDefaultAsync(o => o.SurgeryCaseId == id);
        if (report is null)
            return NotFound(new { message = "التقرير الجراحي غير موجود" });

        if (report.ApprovedAt.HasValue)
            return BadRequest(new { message = "التقرير الجراحي معتمد بالفعل" });

        report.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await notificationService.NotifyUserAsync(surgery.DoctorId ?? Guid.Empty, "surgery", "اعتماد تقرير جراحي", "تم اعتماد التقرير الجراحي", "surgery-cases", surgery.Id);

        return Ok(new { report.Id, ApprovedAt = report.ApprovedAt.Value.ToString("yyyy-MM-dd HH:mm"), message = "تم اعتماد التقرير الجراحي" });
    }

    // ── Postop Record ───────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/postop")]
    public async Task<IActionResult> GetPostop(Guid id)
    {
        var record = await db.PostopRecords.FirstOrDefaultAsync(p => p.SurgeryCaseId == id);
        if (record is null) return Ok(null);
        return Ok(new {
            record.Id,
            record.Instructions,
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
        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم الحفظ" });
    }

    // ── Hospital Referrals ──────────────────────────────────────────────────────

    [HttpGet("{id:guid}/hospital-referrals")]
    public async Task<IActionResult> GetHospitalReferrals(Guid id)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        var referrals = await db.HospitalReferrals
            .Where(r => r.SurgeryCaseId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new HospitalReferralDto
            {
                Id = r.Id,
                SurgeryCaseId = r.SurgeryCaseId,
                HospitalName = r.HospitalName,
                Reason = r.Reason,
                ReferralDate = r.ReferralDate != null ? r.ReferralDate.Value.ToString("yyyy-MM-dd") : null,
                Status = r.Status,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(referrals);
    }

    [HttpPost("{id:guid}/hospital-referrals")]
    public async Task<IActionResult> CreateHospitalReferral(
        Guid id,
        [FromBody] CreateHospitalReferralRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery is null) return NotFound(new { message = "الحالة الجراحية غير موجودة" });

        var referral = new HospitalReferral
        {
            SurgeryCaseId = id,
            HospitalName = req.HospitalName,
            Reason = req.Reason,
            ReferralDate = req.ReferralDate != null ? DateOnly.Parse(req.ReferralDate) : null,
            Status = "pending",
            Notes = req.Notes
        };

        db.HospitalReferrals.Add(referral);
        await db.SaveChangesAsync();

        return Ok(new HospitalReferralDto
        {
            Id = referral.Id,
            SurgeryCaseId = referral.SurgeryCaseId,
            HospitalName = referral.HospitalName,
            Reason = referral.Reason,
            ReferralDate = referral.ReferralDate?.ToString("yyyy-MM-dd"),
            Status = referral.Status,
            Notes = referral.Notes,
            CreatedAt = referral.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPut("~/api/hospital-referrals/{id:guid}/status")]
    public async Task<IActionResult> UpdateHospitalReferralStatus(
        Guid id,
        [FromBody] UpdateHospitalReferralStatusRequest req)
    {
        var referral = await db.HospitalReferrals.FindAsync(id);
        if (referral is null) return NotFound(new { message = "إحالة المستشفى غير موجودة" });

        referral.Status = req.Status;
        if (req.Notes is not null) referral.Notes = req.Notes;

        await db.SaveChangesAsync();

        return Ok(new HospitalReferralDto
        {
            Id = referral.Id,
            SurgeryCaseId = referral.SurgeryCaseId,
            HospitalName = referral.HospitalName,
            Reason = referral.Reason,
            ReferralDate = referral.ReferralDate?.ToString("yyyy-MM-dd"),
            Status = referral.Status,
            Notes = referral.Notes,
            CreatedAt = referral.CreatedAt.ToString("yyyy-MM-dd")
        });
    }
}
