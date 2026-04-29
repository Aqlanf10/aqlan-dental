using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ReportsAccess")]
public class ReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("center-summary")]
    public async Task<IActionResult> GetCenterSummary([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var totalPatients = await db.Patients.CountAsync();
        var newPatients = await db.Patients.CountAsync(p => DateOnly.FromDateTime(p.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(p.CreatedAt.Date) <= toDate);
        var totalAppointments = await db.Appointments.CountAsync(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate);
        var completedAppointments = await db.Appointments.CountAsync(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate && a.Status == Domain.Enums.AppointmentStatus.Completed);
        var activeOrthoCases = await db.OrthoCases.CountAsync(c => c.Status == "active");
        var totalRevenue = await db.Payments.Where(p => p.PaymentDate >= fromDate && p.PaymentDate <= toDate).SumAsync(p => (decimal?)p.Amount) ?? 0;

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            totalPatients,
            newPatients,
            totalAppointments,
            completedAppointments,
            activeOrthoCases,
            totalRevenue
        });
    }

    [HttpGet("doctor-performance")]
    public async Task<IActionResult> GetDoctorPerformance([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var doctors = await db.Doctors.Where(d => d.IsActive).ToListAsync();

        var performance = new List<object>();
        foreach (var d in doctors)
        {
            var appointmentCount = await db.Appointments.CountAsync(a => a.DoctorId == d.Id && a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate);
            var completedCount = await db.Appointments.CountAsync(a => a.DoctorId == d.Id && a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate && a.Status == Domain.Enums.AppointmentStatus.Completed);
            var orthoCasesCount = await db.OrthoCases.CountAsync(c => c.DoctorId == d.Id && c.Status == "active");
            var treatmentsCount = await db.GeneralTreatments.CountAsync(t => t.DoctorId == d.Id && DateOnly.FromDateTime(t.CreatedAt.Date) >= fromDate);
            var revenue = await db.Payments.Where(p => p.DoctorId == d.Id && p.PaymentDate >= fromDate && p.PaymentDate <= toDate).SumAsync(p => (decimal?)p.Amount) ?? 0;

            performance.Add(new
            {
                doctorId = d.Id,
                name = d.Name,
                color = d.Color,
                specialty = d.Specialty,
                appointmentCount,
                completedCount,
                orthoCasesCount,
                treatmentsCount,
                revenue
            });
        }

        return Ok(performance);
    }

    [HttpGet("financial")]
    public async Task<IActionResult> GetFinancialReport([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        // Fetch raw groups then format DateOnly in memory (EF can't translate DateOnly.ToString)
        var paymentsRaw = await db.Payments
            .Where(p => p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .GroupBy(p => p.PaymentDate)
            .Select(g => new { date = g.Key, total = g.Sum(p => p.Amount), count = g.Count() })
            .OrderBy(x => x.date)
            .ToListAsync();
        var payments = paymentsRaw.Select(x => new { date = x.date.ToString("yyyy-MM-dd"), x.total, x.count }).ToList();

        var bySpecialty = await db.Payments
            .Where(p => p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .GroupBy(p => p.Specialty ?? "other")
            .Select(g => new { specialty = g.Key, total = g.Sum(p => p.Amount), count = g.Count() })
            .ToListAsync();

        var byMethod = await db.Payments
            .Where(p => p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .GroupBy(p => p.PaymentMethod ?? "cash")
            .Select(g => new { method = g.Key, total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var totalCollected = payments.Sum(p => p.total);

        return Ok(new { fromDate = fromDate.ToString("yyyy-MM-dd"), toDate = toDate.ToString("yyyy-MM-dd"), totalCollected, daily = payments, bySpecialty, byMethod });
    }
}
