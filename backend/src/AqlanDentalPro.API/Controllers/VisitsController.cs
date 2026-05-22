using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

// ─── Request DTOs ───────────────────────────────────────────────────────────────

public sealed class CreateVisitRequest
{
    public Guid PatientId { get; init; }
    public Guid? AppointmentId { get; init; }
    public string? VisitDate { get; init; }
    public string? VisitType { get; init; }
    public string? Specialty { get; init; }
    public Guid? DoctorId { get; init; }
    public Guid? ServiceId { get; init; }
    public string? ChiefComplaint { get; init; }
    public string? ClinicalNotes { get; init; }
    public string? TreatmentDone { get; init; }
    public string? Diagnosis { get; init; }
    public string? Instructions { get; init; }
    public string? NextVisitPlan { get; init; }
    public decimal? Cost { get; init; }
    public string? NextVisitDate { get; init; }
}

public sealed class UpdateVisitRequest
{
    public string? VisitDate { get; init; }
    public string? VisitType { get; init; }
    public string? Specialty { get; init; }
    public Guid? DoctorId { get; init; }
    public string? ChiefComplaint { get; init; }
    public string? ClinicalNotes { get; init; }
    public string? TreatmentDone { get; init; }
    public string? Diagnosis { get; init; }
    public string? Instructions { get; init; }
    public string? NextVisitPlan { get; init; }
    public decimal? Cost { get; init; }
    public string? NextVisitDate { get; init; }
}

// ─── Controller ─────────────────────────────────────────────────────────────────

[ApiController]
[Route("api/visits")]
[Authorize(Policy = "StaffOnly")]
public class VisitsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // ─── GET /api/visits?patientId={patientId} ────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetVisits([FromQuery] Guid? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.Visits
            .Include(v => v.Doctor)
            .Include(v => v.Appointment)
                .ThenInclude(a => a!.Doctor)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(v => v.PatientId == patientId.Value);

        var total = await query.CountAsync();

        var visits = await query
            .OrderByDescending(v => v.VisitDate)
            .ThenByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new
            {
                v.Id,
                v.PatientId,
                v.AppointmentId,
                VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                v.VisitType,
                Specialty = v.Specialty != null ? v.Specialty.ToString() : null,
                v.DoctorId,
                DoctorName = v.Doctor != null ? v.Doctor.Name : null,
                v.ChiefComplaint,
                v.ClinicalNotes,
                v.TreatmentDone,
                v.Diagnosis,
                v.Instructions,
                v.NextVisitPlan,
                v.Cost,
                NextVisitDate = v.NextVisitDate != null ? v.NextVisitDate.Value.ToString("yyyy-MM-dd") : null,
                v.IsActive,
                CreatedAt = v.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = v.UpdatedAt.ToString("yyyy-MM-dd"),
                // Linked appointment details
                Appointment = v.Appointment != null ? new
                {
                    AppointmentDate = v.Appointment.AppointmentDate.ToString("yyyy-MM-dd"),
                    AppointmentTime = v.Appointment.StartTime.ToString("HH\\:mm"),
                    AppointmentType = v.Appointment.AppointmentType,
                    AppointmentStatus = v.Appointment.Status.ToString(),
                    DoctorName = v.Appointment.Doctor != null ? v.Appointment.Doctor.Name : null,
                } : null,
            })
            .ToListAsync();

        return Ok(new { data = visits, total, page, pageSize });
    }

    // ─── GET /api/visits/{id} ─────────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVisit(Guid id)
    {
        var visit = await db.Visits
            .Include(v => v.Doctor)
            .Include(v => v.Appointment)
                .ThenInclude(a => a!.Doctor)
            .Where(v => v.Id == id)
            .Select(v => new
            {
                v.Id,
                v.PatientId,
                v.AppointmentId,
                VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
                v.VisitType,
                Specialty = v.Specialty != null ? v.Specialty.ToString() : null,
                v.DoctorId,
                DoctorName = v.Doctor != null ? v.Doctor.Name : null,
                v.ChiefComplaint,
                v.ClinicalNotes,
                v.TreatmentDone,
                v.Diagnosis,
                v.Instructions,
                v.NextVisitPlan,
                v.Cost,
                NextVisitDate = v.NextVisitDate != null ? v.NextVisitDate.Value.ToString("yyyy-MM-dd") : null,
                v.IsActive,
                CreatedAt = v.CreatedAt.ToString("yyyy-MM-dd"),
                UpdatedAt = v.UpdatedAt.ToString("yyyy-MM-dd"),
                // Linked appointment details
                Appointment = v.Appointment != null ? new
                {
                    AppointmentDate = v.Appointment.AppointmentDate.ToString("yyyy-MM-dd"),
                    AppointmentTime = v.Appointment.StartTime.ToString("HH\\:mm"),
                    AppointmentType = v.Appointment.AppointmentType,
                    AppointmentStatus = v.Appointment.Status.ToString(),
                    DoctorName = v.Appointment.Doctor != null ? v.Appointment.Doctor.Name : null,
                } : null,
            })
            .FirstOrDefaultAsync();

        if (visit is null)
            return NotFound(new { message = "الزيارة غير موجودة" });

        return Ok(visit);
    }

    // ─── POST /api/visits ─────────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreateVisit([FromBody] CreateVisitRequest req)
    {
        if (req.PatientId == Guid.Empty)
            return BadRequest(new { message = "معرّف المريض مطلوب" });

        var patientExists = await db.Patients.AnyAsync(p => p.Id == req.PatientId);
        if (!patientExists)
            return BadRequest(new { message = "المريض غير موجود" });

        if (req.DoctorId.HasValue)
        {
            var doctorExists = await db.Doctors.AnyAsync(d => d.Id == req.DoctorId.Value);
            if (!doctorExists)
                return BadRequest(new { message = "الطبيب غير موجود" });
        }

        if (req.ServiceId.HasValue)
        {
            var serviceExists = await db.ClinicServices.AnyAsync(s => s.Id == req.ServiceId.Value);
            if (!serviceExists)
                return BadRequest(new { message = "الخدمة غير موجودة" });
        }

        Specialty? specialty = null;
        if (!string.IsNullOrWhiteSpace(req.Specialty) && Enum.TryParse<Specialty>(req.Specialty, true, out var s))
            specialty = s;

        var visit = new Visit
        {
            PatientId = req.PatientId,
            AppointmentId = req.AppointmentId,
            VisitDate = !string.IsNullOrWhiteSpace(req.VisitDate)
                ? DateOnly.TryParse(req.VisitDate, out var vd) ? vd : DateOnly.FromDateTime(DateTime.Today)
                : DateOnly.FromDateTime(DateTime.Today),
            VisitType = req.VisitType,
            Specialty = specialty,
            DoctorId = req.DoctorId,
            ServiceId = req.ServiceId,
            ChiefComplaint = req.ChiefComplaint,
            ClinicalNotes = req.ClinicalNotes,
            TreatmentDone = req.TreatmentDone,
            Diagnosis = req.Diagnosis,
            Instructions = req.Instructions,
            NextVisitPlan = req.NextVisitPlan,
            Cost = req.Cost,
            NextVisitDate = !string.IsNullOrWhiteSpace(req.NextVisitDate)
                ? DateOnly.TryParse(req.NextVisitDate, out var nvd) ? nvd : (DateOnly?)null
                : null,
        };

        db.Visits.Add(visit);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Log detailed error server-side only; never leak DB internals to the client
            return StatusCode(500, new { message = "فشل حفظ الزيارة — يرجى المحاولة مرة أخرى" });
        }

        await db.Entry(visit).Reference(v => v.Doctor).LoadAsync();

        return Ok(new
        {
            visit.Id,
            message = "تم إضافة الزيارة بنجاح"
        });
    }

    // ─── PUT /api/visits/{id} ─────────────────────────────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateVisit(Guid id, [FromBody] UpdateVisitRequest req)
    {
        var visit = await db.Visits.FindAsync(id);
        if (visit is null)
            return NotFound(new { message = "الزيارة غير موجودة" });

        if (!visit.IsActive)
            return BadRequest(new { message = "لا يمكن تعديل زيارة محذوفة" });

        if (req.VisitDate != null)
        {
            if (!DateOnly.TryParse(req.VisitDate, out var visitDate))
                return BadRequest(new { message = "تنسيق تاريخ الزيارة غير صالح. استخدم YYYY-MM-DD" });
            visit.VisitDate = visitDate;
        }
        if (req.VisitType != null)
            visit.VisitType = req.VisitType;
        if (req.Specialty != null)
        {
            if (Enum.TryParse<Specialty>(req.Specialty, true, out var s))
                visit.Specialty = s;
            else
                visit.Specialty = null;
        }
        if (req.DoctorId.HasValue)
            visit.DoctorId = req.DoctorId;
        if (req.ChiefComplaint != null)
            visit.ChiefComplaint = req.ChiefComplaint;
        if (req.ClinicalNotes != null)
            visit.ClinicalNotes = req.ClinicalNotes;
        if (req.TreatmentDone != null)
            visit.TreatmentDone = req.TreatmentDone;
        if (req.Diagnosis != null)
            visit.Diagnosis = req.Diagnosis;
        if (req.Instructions != null)
            visit.Instructions = req.Instructions;
        if (req.NextVisitPlan != null)
            visit.NextVisitPlan = req.NextVisitPlan;
        if (req.Cost.HasValue)
            visit.Cost = req.Cost;
        if (req.NextVisitDate != null)
        {
            if (!DateOnly.TryParse(req.NextVisitDate, out var nextVisitDate))
                return BadRequest(new { message = "تنسيق تاريخ الزيارة القادمة غير صالح. استخدم YYYY-MM-DD" });
            visit.NextVisitDate = string.IsNullOrWhiteSpace(req.NextVisitDate) ? null : nextVisitDate;
        }

        visit.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { message = "تم تحديث الزيارة بنجاح" });
    }

    // ─── DELETE /api/visits/{id} (soft-delete) ────────────────────────────────
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteVisit(Guid id)
    {
        var visit = await db.Visits.FindAsync(id);
        if (visit is null)
            return NotFound(new { message = "الزيارة غير موجودة" });

        if (!visit.IsActive)
            return BadRequest(new { message = "الزيارة محذوفة بالفعل" });

        visit.IsActive = false;
        visit.DeletedAt = DateTime.UtcNow;
        visit.DeletedBy = currentUser.UserId;

        // H6 FIX: Cascade soft-delete to linked prescriptions and general treatments.
        // Previously, soft-deleting a visit left orphaned prescriptions and treatments
        // that appeared in patient records with no valid visit context.
        var prescriptions = await db.Prescriptions
            .Where(p => p.VisitId == id && p.IsActive)
            .ToListAsync();
        foreach (var p in prescriptions)
        {
            p.IsActive = false;
            p.DeletedAt = DateTime.UtcNow;
        }

        var treatments = await db.GeneralTreatments
            .Where(t => t.VisitId == id && t.IsActive)
            .ToListAsync();
        foreach (var t in treatments)
        {
            t.IsActive = false;
            t.DeletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        var cascadedCount = prescriptions.Count + treatments.Count;
        var msg = cascadedCount > 0
            ? $"تم حذف الزيارة و{prescriptions.Count} وصفة طبية و{treatments.Count} علاج مرتبط"
            : "تم حذف الزيارة بنجاح";

        return Ok(new { message = msg });
    }
}
