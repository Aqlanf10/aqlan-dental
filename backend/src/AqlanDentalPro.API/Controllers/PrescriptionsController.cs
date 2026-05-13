using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
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
public class PrescriptionsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
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
        var drugsJson = JsonSerializer.SerializeToDocument(req.Drugs);

        var prescription = new Prescription
        {
            PatientId = req.PatientId,
            DoctorId  = req.DoctorId ?? currentUser.UserId,
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

        prescription.IsActive = false;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الوصفة بنجاح" });
    }
}
