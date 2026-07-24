using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/search")]
[Authorize(Policy = "StaffOnly")]
public class SearchController(AppDbContext db, IPatientAccessService patientAccess) : ControllerBase
{
    // GET /api/search?q=ahmed&limit=5
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 5)
    {
        limit = Math.Max(1, Math.Min(limit, 100));
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(new { patients = Array.Empty<object>(), appointments = Array.Empty<object>(), orthoCases = Array.Empty<object>() });

        q = q.Trim();

        // CORE-PAT-011 (security): this was a second, unguarded patient search.
        // Doctors could find ANY patient here — with phone number — while
        // /api/patients deliberately scopes doctors to their linked patients and
        // strips the phone from clinical DTOs. Apply the same access set to all
        // three result groups, fail-closed (an error must not widen access).
        HashSet<Guid>? accessible = null;
        var isDoctor = patientAccess.IsDoctor;
        if (isDoctor)
        {
            try
            {
                accessible = await patientAccess.GetAccessiblePatientIdsAsync() ?? [];
            }
            catch
            {
                return StatusCode(500, new { message = "تعذر تنفيذ البحث حالياً" });
            }
        }

        var patientsQuery = db.Patients
            .Where(p => p.FirstName.Contains(q) || p.LastName.Contains(q) || p.PatientNumber.Contains(q) || (p.Phone != null && p.Phone.Contains(q)));
        if (accessible != null)
            patientsQuery = patientsQuery.Where(p => accessible.Contains(p.Id));
        var patientsRaw = await patientsQuery
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
            // Doctors never receive the phone — mirrors PatientClinicalDto.
            PhoneNumber = isDoctor ? null : p.Phone,
            Type = "patient",
            Url = $"/patients/{p.Id}"
        }).ToList();

        var apptsQuery = db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.Patient.FirstName.Contains(q) || a.Patient.LastName.Contains(q) || (a.AppointmentType != null && a.AppointmentType.Contains(q)));
        if (accessible != null)
            apptsQuery = apptsQuery.Where(a => accessible.Contains(a.PatientId));
        var apptsRaw = await apptsQuery
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

        var orthoQuery = db.OrthoCases
            .Include(c => c.Patient)
            .Where(c => c.Patient.FirstName.Contains(q) || c.Patient.LastName.Contains(q) || c.CaseNumber.Contains(q));
        if (accessible != null)
            orthoQuery = orthoQuery.Where(c => accessible.Contains(c.PatientId));
        var orthoRaw = await orthoQuery
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
