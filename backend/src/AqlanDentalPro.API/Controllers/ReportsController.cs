using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ReportsAccess")]
public class ReportsController(AppDbContext db, FinanceService financeService) : ControllerBase
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

    // ──────────────────── New Reports Endpoints ────────────────────

    [HttpGet("ortho-cases")]
    public async Task<IActionResult> GetOrthoCasesStats([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-365));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var query = db.OrthoCases.Where(c => c.IsActive);

        var totalCount = await query.CountAsync();
        var activeCount = await query.CountAsync(c => c.Status == "active");
        var completedCount = await query.CountAsync(c => c.Status == "completed");
        var onHoldCount = await query.CountAsync(c => c.Status == "on_hold");
        var retentionCount = await query.CountAsync(c => c.Status == "retention");

        // Average treatment duration for completed cases
        var completedCases = await query
            .Where(c => c.Status == "completed" && c.StartDate.HasValue)
            .ToListAsync();

        double? avgDuration = null;
        if (completedCases.Count > 0)
        {
            avgDuration = completedCases.Average(c =>
            {
                var endDate = c.UpdatedAt;
                var startDate = c.StartDate!.Value.ToDateTime(TimeOnly.MinValue);
                return (endDate - startDate).TotalDays / 30.0;
            });
        }

        // Extraction rate
        var casesWithExtraction = await query
            .Where(c => c.ExtractionDecision != null && c.ExtractionDecision.Decision == "extraction")
            .CountAsync();

        var totalWithDecision = await query
            .Where(c => c.ExtractionDecision != null)
            .CountAsync();

        double? extractionRate = totalWithDecision > 0 ? (double)casesWithExtraction / totalWithDecision * 100 : null;

        // By appliance type
        var byApplianceType = await query
            .GroupBy(c => c.ApplianceType ?? "other")
            .Select(g => new { applianceType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync();

        // New cases in date range
        var newCasesInRange = await query
            .CountAsync(c => c.StartDate >= fromDate && c.StartDate <= toDate);

        return Ok(new
        {
            totalCount,
            activeCount,
            completedCount,
            onHoldCount,
            retentionCount,
            avgTreatmentDurationMonths = avgDuration != null ? Math.Round(avgDuration.Value, 1) : (double?)null,
            extractionRate = extractionRate != null ? Math.Round(extractionRate.Value, 1) : (double?)null,
            extractionCases = casesWithExtraction,
            totalWithExtractionDecision = totalWithDecision,
            byApplianceType,
            newCasesInRange,
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd")
        });
    }

    [HttpGet("general-dentistry")]
    public async Task<IActionResult> GetGeneralDentistryStats([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var treatments = await db.GeneralTreatments
            .Include(t => t.Doctor)
            .Where(t => t.IsActive && DateOnly.FromDateTime(t.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(t.CreatedAt.Date) <= toDate)
            .ToListAsync();

        var totalCount = treatments.Count;
        var totalCost = treatments.Sum(t => t.Cost ?? 0);
        var avgCost = totalCount > 0 ? Math.Round(totalCost / totalCount, 2) : 0;

        // By treatment type
        var byType = treatments
            .GroupBy(t => t.TreatmentType)
            .Select(g => new
            {
                treatmentType = g.Key,
                count = g.Count(),
                totalCost = g.Sum(t => t.Cost ?? 0),
                avgCost = g.Count() > 0 ? Math.Round(g.Sum(t => t.Cost ?? 0) / g.Count(), 2) : 0
            })
            .OrderByDescending(x => x.count)
            .ToList();

        // Top 10 procedures
        var topProcedures = byType.Take(10).ToList();

        // By doctor
        var byDoctor = treatments
            .Where(t => t.Doctor != null)
            .GroupBy(t => new { t.DoctorId, DoctorName = t.Doctor!.Name })
            .Select(g => new
            {
                doctorId = g.Key.DoctorId,
                doctorName = g.Key.DoctorName,
                count = g.Count(),
                totalCost = g.Sum(t => t.Cost ?? 0)
            })
            .OrderByDescending(x => x.count)
            .ToList();

        // By tooth (most treated)
        var byTooth = treatments
            .Where(t => !string.IsNullOrEmpty(t.ToothNumber))
            .GroupBy(t => t.ToothNumber!)
            .Select(g => new { tooth = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(10)
            .ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            totalCount,
            totalCost,
            avgCost,
            byType,
            topProcedures,
            byDoctor,
            byTooth
        });
    }

    [HttpGet("surgery")]
    public async Task<IActionResult> GetSurgeryStats([FromQuery] string? from, [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var fromDateTime = fromDate.ToDateTime(TimeOnly.MinValue);
        var toDateTime = toDate.ToDateTime(TimeOnly.MaxValue);

        var surgeries = await db.SurgeryCases
            .Include(s => s.OperativeReport)
            .Include(s => s.Doctor)
            .Where(s => s.IsActive)
            .ToListAsync();

        var totalCount = surgeries.Count;

        // By type
        var byType = surgeries
            .GroupBy(s => s.SurgeryType)
            .Select(g => new { surgeryType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // By status
        var byStatus = surgeries
            .GroupBy(s => s.Status)
            .Select(g => new { status = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // Average duration (from operative reports)
        var withDuration = surgeries
            .Where(s => s.OperativeReport?.DurationMinutes.HasValue == true)
            .ToList();

        double? avgDuration = withDuration.Count > 0
            ? Math.Round(withDuration.Average(s => s.OperativeReport!.DurationMinutes!.Value), 1)
            : null;

        // By doctor
        var byDoctor = surgeries
            .Where(s => s.Doctor != null)
            .GroupBy(s => new { s.DoctorId, DoctorName = s.Doctor!.Name })
            .Select(g => new
            {
                doctorId = g.Key.DoctorId,
                doctorName = g.Key.DoctorName,
                count = g.Count()
            })
            .OrderByDescending(x => x.count)
            .ToList();

        // Completed surgeries in date range
        var completedInRange = surgeries
            .Where(s => s.OperativeReport?.SurgeryDateTime >= fromDateTime && s.OperativeReport?.SurgeryDateTime <= toDateTime)
            .ToList();

        // Complications
        var withComplications = surgeries
            .Count(s => s.OperativeReport?.Complications != null && !string.IsNullOrWhiteSpace(s.OperativeReport.Complications));

        // Hospital referrals
        var hospitalReferrals = await db.HospitalReferrals
            .CountAsync(h => h.CreatedAt >= fromDateTime && h.CreatedAt <= toDateTime);

        return Ok(new
        {
            totalCount,
            byType,
            byStatus,
            avgDurationMinutes = avgDuration,
            byDoctor,
            completedInRange = completedInRange.Count,
            withComplications,
            hospitalReferrals,
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd")
        });
    }

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdueContracts()
    {
        var result = await financeService.GetOverdueContractsAsync();
        return Ok(result);
    }

    [HttpGet("growth-analysis")]
    public async Task<IActionResult> GetGrowthAnalysis()
    {
        var today = DateTime.Today;
        var twelveMonthsAgo = today.AddMonths(-12);

        var patients = await db.Patients
            .Where(p => p.CreatedAt >= twelveMonthsAgo)
            .ToListAsync();

        var monthlyData = new List<object>();
        for (var i = 11; i >= 0; i--)
        {
            var monthDate = today.AddMonths(-i);
            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            var newPatients = patients.Count(p => p.CreatedAt >= monthStart && p.CreatedAt <= monthEnd);

            var arabicMonths = new[]
            {
                "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
                "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
            };

            monthlyData.Add(new
            {
                year = monthDate.Year,
                month = monthDate.Month,
                monthName = arabicMonths[monthDate.Month - 1],
                newPatients,
                label = $"{arabicMonths[monthDate.Month - 1]} {monthDate.Year}"
            });
        }

        var totalPatients = await db.Patients.CountAsync();
        var activePatients = await db.Patients.CountAsync(p => p.IsActive);

        // Specialty distribution
        var orthoPatients = await db.OrthoCases.Select(c => c.PatientId).Distinct().CountAsync();
        var surgeryPatients = await db.SurgeryCases.Select(c => c.PatientId).Distinct().CountAsync();
        var generalPatients = await db.GeneralTreatments.Select(t => t.PatientId).Distinct().CountAsync();

        return Ok(new
        {
            totalPatients,
            activePatients,
            monthly = monthlyData,
            specialtyDistribution = new
            {
                orthodontics = orthoPatients,
                oralSurgery = surgeryPatients,
                generalDentistry = generalPatients
            }
        });
    }
}
