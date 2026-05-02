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
[Authorize]
public class VisitsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // ─── GET /api/visits?patientId={patientId} ────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetVisits([FromQuery] Guid? patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = db.Visits.Include(v => v.Doctor).AsQueryable();

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

        Specialty? specialty = null;
        if (!string.IsNullOrWhiteSpace(req.Specialty) && Enum.TryParse<Specialty>(req.Specialty, true, out var s))
            specialty = s;

        var visit = new Visit
        {
            PatientId = req.PatientId,
            AppointmentId = req.AppointmentId,
            VisitDate = !string.IsNullOrWhiteSpace(req.VisitDate)
                ? DateOnly.Parse(req.VisitDate)
                : DateOnly.FromDateTime(DateTime.Today),
            VisitType = req.VisitType,
            Specialty = specialty,
            DoctorId = req.DoctorId,
            ChiefComplaint = req.ChiefComplaint,
            ClinicalNotes = req.ClinicalNotes,
            TreatmentDone = req.TreatmentDone,
            Diagnosis = req.Diagnosis,
            Instructions = req.Instructions,
            NextVisitPlan = req.NextVisitPlan,
            Cost = req.Cost,
            NextVisitDate = !string.IsNullOrWhiteSpace(req.NextVisitDate)
                ? DateOnly.Parse(req.NextVisitDate)
                : null,
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

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
            visit.VisitDate = DateOnly.Parse(req.VisitDate);
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
            visit.NextVisitDate = string.IsNullOrWhiteSpace(req.NextVisitDate) ? null : DateOnly.Parse(req.NextVisitDate);

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
        await db.SaveChangesAsync();

        return Ok(new { message = "تم حذف الزيارة بنجاح" });
    }
}
