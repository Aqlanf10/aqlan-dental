using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreateReferralRequest
{
    public Guid PatientId { get; init; }
    public Guid FromDoctorId { get; init; }
    public Guid ToDoctorId { get; init; }
    public string? Reason { get; init; }
    public string? Priority { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateReferralRequestValidator : AbstractValidator<CreateReferralRequest>
{
    private static readonly HashSet<string> ValidPriorities = ["urgent", "normal", "low"];

    public CreateReferralRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.FromDoctorId)
            .NotEmpty().WithMessage("الطبيب المُحيل مطلوب");

        RuleFor(x => x.ToDoctorId)
            .NotEmpty().WithMessage("الطبيب المستقبِل مطلوب")
            .NotEqual(x => x.FromDoctorId).WithMessage("لا يمكن إحالة المريض إلى نفس الطبيب");

        RuleFor(x => x.Priority)
            .Must(p => ValidPriorities.Contains(p!)).WithMessage("الأولوية غير صالحة")
            .When(x => !string.IsNullOrWhiteSpace(x.Priority));
    }
}

[ApiController]
[Route("api/referrals")]
[Authorize(Policy = "StaffOnly")]
[ServiceFilter(typeof(PatientAccessFilter))]
public class ReferralsController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    IAuditService audit) : ControllerBase
{
    // CLIN-01: Per-patient access check for actions where patientId is in body or inferred.
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "Referral", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.InternalReferrals
            .Include(r => r.Patient)
            .Include(r => r.FromDoctor)
            .Include(r => r.ToDoctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        if (patientId.HasValue) query = query.Where(r => r.PatientId == patientId.Value);

        var total = await query.CountAsync();
        var referrals = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.PatientId,
                PatientName    = r.Patient.FirstName + " " + r.Patient.LastName,
                PatientNumber  = r.Patient.PatientNumber,
                FromDoctorName = r.FromDoctor.Name,
                FromDoctorColor = r.FromDoctor.Color,
                ToDoctorName   = r.ToDoctor.Name,
                ToDoctorColor  = r.ToDoctor.Color,
                r.Reason,
                r.Priority,
                r.Notes,
                r.Status,
                CreatedAt      = r.CreatedAt.ToString("yyyy-MM-dd"),
                AcceptedAt     = r.AcceptedAt != null ? r.AcceptedAt.Value.ToString("yyyy-MM-dd") : null
            })
            .ToListAsync();

        return Ok(new { data = referrals, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var referral = await db.InternalReferrals
            .Include(r => r.Patient)
            .Include(r => r.FromDoctor)
            .Include(r => r.ToDoctor)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (referral is null) return NotFound(new { message = "الإحالة غير موجودة" });

        // CLIN-01: per-patient check after loading the entity.
        var denied = await DenyIfDoctorCannotAccess(referral.PatientId);
        if (denied is not null) return denied;

        return Ok(new
        {
            referral.Id,
            referral.PatientId,
            PatientName    = referral.Patient.FirstName + " " + referral.Patient.LastName,
            PatientNumber  = referral.Patient.PatientNumber,
            FromDoctorName = referral.FromDoctor.Name,
            FromDoctorColor = referral.FromDoctor.Color,
            ToDoctorName   = referral.ToDoctor.Name,
            ToDoctorColor  = referral.ToDoctor.Color,
            referral.Reason,
            referral.Priority,
            referral.Notes,
            referral.Status,
            CreatedAt      = referral.CreatedAt.ToString("yyyy-MM-dd"),
            AcceptedAt     = referral.AcceptedAt?.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReferralRequest req)
    {
        // CLIN-01: per-patient check before creating.
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        var referral = new InternalReferral
        {
            PatientId    = req.PatientId,
            FromDoctorId = req.FromDoctorId,
            ToDoctorId   = req.ToDoctorId,
            Reason       = req.Reason,
            Priority     = req.Priority ?? "normal",
            Notes        = req.Notes,
            Status       = "pending"
        };

        db.InternalReferrals.Add(referral);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = referral.Id }, new { referral.Id });
    }

    [HttpPut("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var referral = await db.InternalReferrals.FindAsync(id);
        if (referral is null) return NotFound(new { message = "الإحالة غير موجودة" });

        // CLIN-01: per-patient check.
        var denied = await DenyIfDoctorCannotAccess(referral.PatientId);
        if (denied is not null) return denied;

        if (referral.Status != "pending")
            return BadRequest(new { message = "يمكن قبول الإحالات المعلّقة فقط" });

        referral.Status     = "accepted";
        referral.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { id, status = "accepted" });
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var referral = await db.InternalReferrals.FindAsync(id);
        if (referral is null) return NotFound(new { message = "الإحالة غير موجودة" });

        // CLIN-01: per-patient check.
        var denied = await DenyIfDoctorCannotAccess(referral.PatientId);
        if (denied is not null) return denied;

        if (referral.Status != "accepted")
            return BadRequest(new { message = "يمكن إكمال الإحالات المقبولة فقط" });

        referral.Status = "completed";
        await db.SaveChangesAsync();

        return Ok(new { id, status = "completed" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var referral = await db.InternalReferrals.FindAsync(id);
        if (referral is null) return NotFound(new { message = "الإحالة غير موجودة" });

        // CLIN-01: per-patient check before deleting.
        var denied = await DenyIfDoctorCannotAccess(referral.PatientId);
        if (denied is not null) return denied;

        referral.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الإحالة بنجاح" });
    }
}
