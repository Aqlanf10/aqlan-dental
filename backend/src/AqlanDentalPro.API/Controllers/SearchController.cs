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

        var patientsRaw = await db.Patients
            .Where(p => p.FirstName.Contains(q) || p.LastName.Contains(q) || p.PatientNumber.Contains(q) || (p.Phone != null && p.Phone.Contains(q)))
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.FirstName,
                p.LastName,
                p.PatientNumber,
                p.Phone,
            })
            .ToListAsync();

        var patients = patientsRaw.Select(p => new
        {
            p.Id,
            FullName = $"{p.FirstName} {p.LastName}".Trim(),
            p.PatientNumber,
            PhoneNumber = p.Phone,
            Type = "patient",
            Url = $"/patients/{p.Id}"
        }).ToList();

        var apptsRaw = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Patient.FirstName.Contains(q) || a.Patient.LastName.Contains(q) || (a.AppointmentType != null && a.AppointmentType.Contains(q)))
            .OrderByDescending(a => a.AppointmentDate)
            .Take(limit)
            .Select(a => new
            {
                a.Id,
                a.Patient.FirstName,
                a.Patient.LastName,
                a.AppointmentType,
                AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                DoctorName = a.Doctor != null ? a.Doctor.Name : null,
                a.Status,
            })
            .ToListAsync();

        var appointments = apptsRaw.Select(a => new
        {
            a.Id,
            PatientName = $"{a.FirstName} {a.LastName}".Trim(),
            a.AppointmentType,
            a.AppointmentDate,
            a.DoctorName,
            Status = a.Status.ToString(),
            Type = "appointment",
            Url = "/appointments"
        }).ToList();

        var orthoRaw = await db.OrthoCases
            .Include(c => c.Patient)
            .Where(c => c.Patient.FirstName.Contains(q) || c.Patient.LastName.Contains(q) || c.CaseNumber.Contains(q))
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new
            {
                c.Id,
                c.CaseNumber,
                c.Patient.FirstName,
                c.Patient.LastName,
                c.Status,
            })
            .ToListAsync();

        var orthoCases = orthoRaw.Select(c => new
        {
            c.Id,
            c.CaseNumber,
            PatientName = $"{c.FirstName} {c.LastName}".Trim(),
            c.Status,
            Type = "ortho",
            Url = $"/ortho/{c.Id}"
        }).ToList();

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
