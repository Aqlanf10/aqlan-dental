using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

public sealed class DrugItem
{
    public string Name { get; init; } = string.Empty;
    public string Dose { get; init; } = string.Empty;
    public string Frequency { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class CreatePrescriptionRequest
{
    public Guid PatientId { get; init; }
    public Guid? DoctorId { get; init; }
    public Guid? VisitId { get; init; }
    public string? Diagnosis { get; init; }
    public List<DrugItem> Drugs { get; init; } = [];
    public string? Notes { get; init; }
}

public sealed class CreatePrescriptionRequestValidator : AbstractValidator<CreatePrescriptionRequest>
{
    public CreatePrescriptionRequestValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("المريض مطلوب");
        RuleFor(x => x.Drugs).NotEmpty().WithMessage("يجب إضافة دواء واحد على الأقل");
        RuleForEach(x => x.Drugs).ChildRules(drug =>
        {
            drug.RuleFor(d => d.Name).NotEmpty().WithMessage("اسم الدواء مطلوب");
            drug.RuleFor(d => d.Dose).NotEmpty().WithMessage("الجرعة مطلوبة");
            drug.RuleFor(d => d.Frequency).NotEmpty().WithMessage("تكرار الجرعة مطلوب");
            drug.RuleFor(d => d.Duration).NotEmpty().WithMessage("مدة العلاج مطلوبة");
        });
    }
}

[ApiController]
[Route("api/prescriptions")]
[Authorize(Policy = "StaffOnly")]
[ServiceFilter(typeof(PatientAccessFilter))]
public class PrescriptionsController(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    IAuditService audit) : ControllerBase
{
    // CLIN-01: Helper — denies access if the current doctor cannot access the patient.
    // Mirrors the DenyIfDoctorCannotAccess pattern in PatientsController.
    private async Task<IActionResult?> DenyIfDoctorCannotAccess(Guid patientId)
    {
        if (!patientAccess.IsDoctor) return null;
        if (!await patientAccess.CanAccessPatientAsync(patientId))
        {
            await audit.LogAsync(AuditAction.View, "Patient", patientId,
                newData: new { status = "denied", resource = "Prescription", role = currentUser.Role?.ToString(), userId = currentUser.UserId });
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
        var query = db.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .AsQueryable();

        if (patientId.HasValue) query = query.Where(p => p.PatientId == patientId.Value);

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
                return StatusCode(500, new { message = "تعذر تحميل الوصفات حالياً" });
            }
            query = query.Where(p => accessible.Contains(p.PatientId));
        }

        var total = await query.CountAsync();
        var prescriptions = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.PatientId,
                PatientName = p.Patient.FirstName + " " + p.Patient.LastName,
                PatientNumber = p.Patient.PatientNumber,
                DoctorName = p.Doctor != null ? p.Doctor.Name : null,
                p.Diagnosis,
                DrugCount = p.Drugs.RootElement.GetArrayLength(),
                p.Notes,
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(new { data = prescriptions, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var prescription = await db.Prescriptions
            .Include(p => p.Patient)
            .Include(p => p.Doctor)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prescription is null) return NotFound(new { message = "الوصفة الطبية غير موجودة" });

        // CLIN-01: Per-patient access check (the PatientAccessFilter cannot infer patientId
        // from the {id:guid} route, so we check here after loading the entity).
        var denied = await DenyIfDoctorCannotAccess(prescription.PatientId);
        if (denied is not null) return denied;

        var drugs = JsonSerializer.Deserialize<List<DrugItem>>(
            prescription.Drugs.RootElement.GetRawText()) ?? [];

        return Ok(new
        {
            prescription.Id,
            prescription.PatientId,
            PatientName = prescription.Patient.FirstName + " " + prescription.Patient.LastName,
            PatientNumber = prescription.Patient.PatientNumber,
            DoctorName = prescription.Doctor?.Name,
            prescription.Diagnosis,
            Drugs = drugs,
            prescription.Notes,
            CreatedAt = prescription.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePrescriptionRequest req)
    {
        // CLIN-01: Per-patient access check before creating (PatientId comes from the body,
        // not the route, so the class-level PatientAccessFilter cannot see it).
        var denied = await DenyIfDoctorCannotAccess(req.PatientId);
        if (denied is not null) return denied;

        // CLIN-23: Validate existence of PatientId + VisitId ownership.
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

        var drugsJson = JsonSerializer.SerializeToDocument(req.Drugs);

        // CLIN-23: Prescription.DoctorId references Doctors.Id, NOT Users.Id — when the
        // client omits doctorId, resolve the Doctor row of the current user (via
        // Doctors.UserId) instead of writing the UserId (which would violate the FK).
        // Mirrors the pattern established in LabOrdersController.Create.
        Guid? resolvedDoctorId = req.DoctorId;
        if (!resolvedDoctorId.HasValue && currentUser.UserId.HasValue)
        {
            resolvedDoctorId = await db.Doctors
                .Where(d => d.UserId == currentUser.UserId.Value && d.IsActive)
                .Select(d => (Guid?)d.Id)
                .FirstOrDefaultAsync();
        }

        var prescription = new Prescription
        {
            PatientId = req.PatientId,
            DoctorId  = resolvedDoctorId,
            VisitId   = req.VisitId,
            Diagnosis = req.Diagnosis,
            Drugs     = drugsJson,
            Notes     = req.Notes
        };

        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = prescription.Id },
            new { prescription.Id });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var prescription = await db.Prescriptions.FindAsync(id);
        if (prescription is null) return NotFound(new { message = "الوصفة الطبية غير موجودة" });

        // CLIN-01: Per-patient access check before deleting.
        var denied = await DenyIfDoctorCannotAccess(prescription.PatientId);
        if (denied is not null) return denied;

        prescription.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الوصفة بنجاح" });
    }
}
