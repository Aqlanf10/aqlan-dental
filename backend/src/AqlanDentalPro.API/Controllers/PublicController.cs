using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api")]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    public PublicController(AppDbContext db) => _db = db;

    [HttpGet("public/queue")]
    [AllowAnonymous]
    public async Task<IActionResult> GetQueue()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var items = await _db.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == today && a.Status != AppointmentStatus.Cancelled)
            .OrderBy(a => a.StartTime)
            .Select(a => new
            {
                a.Id,
                PatientDisplayName = a.Patient.FirstName + " " + a.Patient.LastName.Substring(0, 1) + ".",
                AppointmentType = a.AppointmentType ?? "—",
                StartTime = a.StartTime.ToString(@"hh\:mm"),
                EndTime = (string?)a.EndTime.ToString(@"hh\:mm"),
                DoctorName = (string?)(a.Doctor != null ? a.Doctor.Name : null),
                DoctorColor = (string?)(a.Doctor != null ? a.Doctor.Color : null),
                Status = a.Status.ToString()
            })
            .ToListAsync();

        return Ok(items);
    }

}
