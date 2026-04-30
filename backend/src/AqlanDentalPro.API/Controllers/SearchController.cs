using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/search")]
[Authorize]
public class SearchController(AppDbContext db) : ControllerBase
{
    // GET /api/search?q=ahmed&limit=5
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new { patients = Array.Empty<object>(), appointments = Array.Empty<object>(), orthoCases = Array.Empty<object>() });

        q = q.Trim();

        var patients = await db.Patients
            .Where(p => p.FullName.Contains(q) || p.PatientNumber.Contains(q) || (p.PhoneNumber != null && p.PhoneNumber.Contains(q)))
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.FullName,
                p.PatientNumber,
                p.PhoneNumber,
                Type = "patient",
                Url = $"/patients/{p.Id}"
            })
            .ToListAsync();

        var appointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Patient.FullName.Contains(q) || (a.AppointmentType != null && a.AppointmentType.Contains(q)))
            .OrderByDescending(a => a.AppointmentDate)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                PatientName = a.Patient.FullName,
                a.AppointmentType,
                AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                DoctorName = a.Doctor != null ? a.Doctor.Name : null,
                a.Status,
                Type = "appointment",
                Url = $"/appointments"
            })
            .ToListAsync();

        var orthoCases = await db.OrthoCases
            .Include(c => c.Patient)
            .Where(c => c.Patient.FullName.Contains(q) || c.CaseNumber.Contains(q))
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new
            {
                c.Id,
                c.CaseNumber,
                PatientName = c.Patient.FullName,
                c.Status,
                Type = "ortho",
                Url = $"/ortho/{c.Id}"
            })
            .ToListAsync();

        return Ok(new
        {
            query = q,
            patients,
            appointments,
            orthoCases,
            total = patients.Count + appointments.Count + orthoCases.Count
        });
    }
}
