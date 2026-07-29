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

public sealed class CreateRadiologyOrderRequest
{
    public Guid PatientId { get; init; }
    public Guid? DoctorId { get; init; }
    public Guid? VisitId { get; init; }
    public string StudyType { get; init; } = string.Empty;
    public string? Region { get; init; }
    public string? ClinicalNotes { get; init; }
}

public sealed class CreateRadiologyOrderRequestValidator : AbstractValidator<CreateRadiologyOrderRequest>
{
    // Aligned with Radiograph.XrayType values so a later "attach result" feature maps 1:1.
    public static readonly string[] AllowedStudyTypes =
        ["panoramic", "cbct_3d", "lateral_ceph", "pa_ceph", "bitewing", "periapical", "other"];

    public CreateRadiologyOrderRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("المريض مطلوب");
        RuleFor(x => x.StudyType)
            .NotEmpty().WithMessage("نوع الأشعة مطلوب")
            .Must(t => AllowedStudyTypes.Contains(t))
            .WithMessage("نوع الأشعة غير صحيح");
        RuleFor(x => x.Region).MaximumLength(200).WithMessage("منطقة التصوير طويلة جدًا (200 حرف كحد أقصى)");
        RuleFor(x => x.ClinicalNotes).MaximumLength(1000).WithMessage("الملاحظات السريرية طويلة جدًا (1000 حرف كحد أقصى)");
    }
}

/// <summary>
/// RX (spec 010): outgoing imaging orders (OPG/CBCT/…) the patient carries to an
/// external radiology center. Mirrors PrescriptionsController (StaffOnly +
/// per-patient doctor access checks; Arabic error messages).
/// </summary>
[ApiController]
[Route("api/radiology-orders")]
[Authorize(Policy = "StaffOnly")]
[ServiceFilter(typeof(PatientAccessFilter))]
public class RadiologyOrdersController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    IAuditService audit) : ControllerBase
{
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "RadiologyOrder", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
            return StatusCode(403, new { message = "غير مصرح لك بعرض بيانات هذا المريض" });
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.RadiologyOrders
            .Include(o => o.Patient)
            .Include(o => o.Doctor)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(o => o.PatientId == patientId.Value);

        // Codex P1 (#676): PatientAccessFilter only enforces access when a patientId
        // is present in the route/query — a plain list request reached this query
        // unfiltered, letting any doctor read every patient's orders. Mirror the
        // PatientsController pattern: doctors see only their linked patients,
        // fail-closed (500, not silent-empty) if the accessible set cannot load.
        if (patientAccess.IsDoctor)
        {
            HashSet<Guid> accessible;
            try
            {
                accessible = await patientAccess.GetAccessiblePatientIdsAsync() ?? [];
            }
            catch
            {
                return StatusCode(500, new { message = "تعذر تحميل طلبات الأشعة حالياً" });
            }
            query = query.Where(o => accessible.Contains(o.PatientId));
        }

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.PatientId,
                PatientName = o.Patient.FirstName + " " + o.Patient.LastName,
                PatientNumber = o.Patient.PatientNumber,
                DoctorName = o.Doctor != null ? o.Doctor.Name : null,
                o.StudyType,
                o.Region,
                CreatedAt = o.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = orders, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await db.RadiologyOrders
            .Include(o => o.Patient)
            .Include(o => o.Doctor)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return NotFound(new { message = "طلب الأشعة غير موجود" });

        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        return Ok(new
        {
            order.Id,
            order.PatientId,
            PatientName = order.Patient.FirstName + " " + order.Patient.LastName,
            PatientNumber = order.Patient.PatientNumber,
            PatientDateOfBirth = order.Patient.DateOfBirth?.ToString("yyyy-MM-dd"),
            PatientGender = order.Patient.Gender != null ? order.Patient.Gender.ToString() : null,
            DoctorName = order.Doctor?.Name,
            order.StudyType,
            order.Region,
            order.ClinicalNotes,
            CreatedAt = order.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRadiologyOrderRequest req)
    {
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        var patientExists = await db.Patients.AnyAsync(p => p.Id == req.PatientId && p.IsActive);
        if (!patientExists)
            return BadRequest(new { message = "المريض غير موجود" });

        if (req.VisitId.HasValue)
        {
            var visitBelongsToPatient = await db.Visits
                .AnyAsync(v => v.Id == req.VisitId.Value && v.PatientId == req.PatientId && v.IsActive);
            if (!visitBelongsToPatient)
                return BadRequest(new { message = "الزيارة غير موجودة أو لا تخص هذا المريض" });
        }

        // RadiologyOrder.DoctorId references Doctors.Id, NOT Users.Id — resolve via
        // Doctors.UserId when omitted (same pattern as PrescriptionsController).
        Guid? resolvedDoctorId = req.DoctorId;
        if (!resolvedDoctorId.HasValue && currentUser.UserId.HasValue)
        {
            resolvedDoctorId = await db.Doctors
                .Where(d => d.UserId == currentUser.UserId.Value && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();
        }

        var order = new RadiologyOrder
        {
            PatientId     = req.PatientId,
            DoctorId      = resolvedDoctorId,
            VisitId       = req.VisitId,
            StudyType     = req.StudyType,
            Region        = req.Region,
            ClinicalNotes = req.ClinicalNotes
        };

        db.RadiologyOrders.Add(order);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { order.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var order = await db.RadiologyOrders.FindAsync(id);
        if (order is null) return NotFound(new { message = "طلب الأشعة غير موجود" });

        var denied = await DenyIfDoctorCannotAccess(order.PatientId);
        if (denied is not null) return denied;

        order.IsActive = false;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف طلب الأشعة" });
    }
}
