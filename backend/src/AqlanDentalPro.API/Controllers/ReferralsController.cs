using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/referrals")]
[Authorize]
public class ReferralsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status = null)
    {
        var query = db.InternalReferrals
            .Include(r => r.Patient)
            .Include(r => r.FromDoctor)
            .Include(r => r.ToDoctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var referrals = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .Select(r => new
            {
                r.Id,
                r.PatientId,
                PatientName = r.Patient.FirstName + " " + r.Patient.LastName,
                PatientNumber = r.Patient.PatientNumber,
                FromDoctorName = r.FromDoctor.Name,
                FromDoctorColor = r.FromDoctor.Color,
                ToDoctorName = r.ToDoctor.Name,
                ToDoctorColor = r.ToDoctor.Color,
                r.Reason,
                r.Priority,
                r.Notes,
                r.Status,
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd"),
                AcceptedAt = r.AcceptedAt != null ? r.AcceptedAt.Value.ToString("yyyy-MM-dd") : null
            })
            .ToListAsync();

        return Ok(referrals);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReferralRequest req)
    {
        var referral = new InternalReferral
        {
            PatientId = req.PatientId,
            FromDoctorId = req.FromDoctorId,
            ToDoctorId = req.ToDoctorId,
            Reason = req.Reason,
            Priority = req.Priority ?? "normal",
            Notes = req.Notes,
            Status = "pending"
        };

        db.InternalReferrals.Add(referral);
        await db.SaveChangesAsync();

        return Ok(new { id = referral.Id });
    }

    [HttpPut("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id)
    {
        var referral = await db.InternalReferrals.FindAsync(id);
        if (referral == null) return NotFound();

        referral.Status = "accepted";
        referral.AcceptedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { id, status = "accepted" });
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var referral = await db.InternalReferrals.FindAsync(id);
        if (referral == null) return NotFound();

        referral.Status = "completed";
        await db.SaveChangesAsync();

        return Ok(new { id, status = "completed" });
    }
}

public class CreateReferralRequest
{
    public Guid PatientId { get; set; }
    public Guid FromDoctorId { get; set; }
    public Guid ToDoctorId { get; set; }
    public string? Reason { get; set; }
    public string? Priority { get; set; }
    public string? Notes { get; set; }
}
