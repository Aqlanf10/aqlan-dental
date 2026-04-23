using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/surgery-cases")]
[Authorize]
public class SurgeryController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status = null, [FromQuery] Guid? doctorId = null)
    {
        var query = db.SurgeryCases
            .Include(s => s.Patient)
            .Include(s => s.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(s => s.Status == status);
        if (doctorId.HasValue) query = query.Where(s => s.DoctorId == doctorId);

        var cases = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(100)
            .Select(s => new
            {
                s.Id,
                s.CaseNumber,
                s.PatientId,
                PatientName = s.Patient.FirstName + " " + s.Patient.LastName,
                PatientNumber = s.Patient.PatientNumber,
                DoctorName = s.Doctor != null ? s.Doctor.Name : null,
                DoctorColor = s.Doctor != null ? s.Doctor.Color : null,
                s.SurgeryType,
                s.TeethInvolved,
                s.Status,
                CreatedAt = s.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();

        return Ok(cases);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSurgeryCaseRequest req)
    {
        var year = DateTime.UtcNow.Year;
        var count = await db.SurgeryCases.IgnoreQueryFilters()
            .CountAsync(c => c.CaseNumber.StartsWith($"SU-{year}-"));

        var surgery = new SurgeryCase
        {
            CaseNumber = $"SU-{year}-{(count + 1):D3}",
            PatientId = req.PatientId,
            DoctorId = req.DoctorId,
            SurgeryType = req.SurgeryType,
            TeethInvolved = req.TeethInvolved,
            Status = "scheduled"
        };

        db.SurgeryCases.Add(surgery);
        await db.SaveChangesAsync();

        return Ok(new { id = surgery.Id, caseNumber = surgery.CaseNumber });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSurgeryStatusRequest req)
    {
        var surgery = await db.SurgeryCases.FindAsync(id);
        if (surgery == null) return NotFound();

        surgery.Status = req.Status;
        await db.SaveChangesAsync();

        return Ok(new { id, status = req.Status });
    }
}

public class CreateSurgeryCaseRequest
{
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public string SurgeryType { get; set; } = string.Empty;
    public string? TeethInvolved { get; set; }
}

public class UpdateSurgeryStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
