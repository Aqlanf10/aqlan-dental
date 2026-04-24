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

[ApiController]
[Route("api/surgery-cases")]
[Authorize]
public class SurgeryController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? doctorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = db.SurgeryCases
            .Include(s => s.Patient)
            .Include(s => s.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status);
        if (doctorId.HasValue) query = query.Where(s => s.DoctorId == doctorId);

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
}
