using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ReportsAccess")]
public class ReportsController(AppDbContext db, IPdfService pdfService, ILogger<ReportsController> logger, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("center-summary")]
    public async Task<IActionResult> GetCenterSummary([FromQuery] string? from, [FromQuery] string? to)
    {
        // ERR-01 FIX: Safe date parsing
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var totalPatients = await db.Patients.CountAsync();
        var newPatients = await db.Patients.CountAsync(p => DateOnly.FromDateTime(p.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(p.CreatedAt.Date) <= toDate);
        var totalAppointments = await db.Appointments.CountAsync(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate);
        var completedAppointments = await db.Appointments.CountAsync(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate && a.Status == Domain.Enums.AppointmentStatus.Completed);
        var activeOrthoCases = await db.OrthoCases.CountAsync(c => c.Status == OrthoCaseStatus.Active);
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
        // ERR-01 FIX: Safe date parsing
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var doctorIds = await db.Doctors.Where(d => d.IsActive).Select(d => d.Id).ToListAsync();

        // Batch all 5 metrics in single GROUP BY queries instead of N×5 round-trips
        var appointmentStats = await db.Appointments
            .Where(a => doctorIds.Contains(a.DoctorId) && a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate)
            .GroupBy(a => a.DoctorId)
            .Select(g => new { DoctorId = g.Key, Count = g.Count(), Completed = g.Count(a => a.Status == Domain.Enums.AppointmentStatus.Completed) })
            .ToListAsync();

        var orthoStats = await db.OrthoCases
            .Where(c => c.DoctorId != null && doctorIds.Contains(c.DoctorId.Value) && c.Status == OrthoCaseStatus.Active)
            .GroupBy(c => c.DoctorId!.Value)
            .Select(g => new { DoctorId = g.Key, Count = g.Count() })
            .ToListAsync();

        var treatmentStats = await db.GeneralTreatments
            .Where(t => t.DoctorId != null && doctorIds.Contains(t.DoctorId.Value) && DateOnly.FromDateTime(t.CreatedAt.Date) >= fromDate)
            .GroupBy(t => t.DoctorId!.Value)
            .Select(g => new { DoctorId = g.Key, Count = g.Count() })
            .ToListAsync();

        var revenueStats = await db.Payments
            .Where(p => p.DoctorId != null && doctorIds.Contains(p.DoctorId.Value) && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .GroupBy(p => p.DoctorId!.Value)
            .Select(g => new { DoctorId = g.Key, Revenue = g.Sum(p => p.Amount) })
            .ToListAsync();

        var doctors = await db.Doctors.Where(d => d.IsActive).ToListAsync();

        var performance = doctors.Select(d => new
        {
            doctorId = d.Id,
            name = d.Name,
            color = d.Color,
            specialty = d.Specialty,
            appointmentCount = appointmentStats.FirstOrDefault(s => s.DoctorId == d.Id)?.Count ?? 0,
            completedCount = appointmentStats.FirstOrDefault(s => s.DoctorId == d.Id)?.Completed ?? 0,
            orthoCasesCount = orthoStats.FirstOrDefault(s => s.DoctorId == d.Id)?.Count ?? 0,
            treatmentsCount = treatmentStats.FirstOrDefault(s => s.DoctorId == d.Id)?.Count ?? 0,
            revenue = revenueStats.FirstOrDefault(s => s.DoctorId == d.Id)?.Revenue ?? 0
        });

        return Ok(performance);
    }

    [HttpGet("financial")]
    public async Task<IActionResult> GetFinancialReport([FromQuery] string? from, [FromQuery] string? to)
    {
        // ERR-01 FIX: Safe date parsing
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

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

        // Phase 0B: Include CashFlowTransaction-based expenses, refunds, and supplier payments
        // for a complete financial picture (not just patient payments)
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        
        var totalExpenses = await db.CashFlowTransactions
            .Where(t => t.Type == TransactionType.Outflow && t.IsActive
                && t.TransactionDate >= fromDate && t.TransactionDate <= toDate
                && t.Category == FinancialCategory.OperationalExpense)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
            
        var totalRefunds = await db.CashFlowTransactions
            .Where(t => t.Type == TransactionType.Outflow && t.IsActive
                && t.TransactionDate >= fromDate && t.TransactionDate <= toDate
                && t.Category == FinancialCategory.Refund)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
            
        var totalSupplierPayments = await db.CashFlowTransactions
            .Where(t => t.Type == TransactionType.Outflow && t.IsActive
                && t.TransactionDate >= fromDate && t.TransactionDate <= toDate
                && t.Category == FinancialCategory.SupplierPayment)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;
            
        var totalSalaryAdvances = await db.CashFlowTransactions
            .Where(t => t.Type == TransactionType.Outflow && t.IsActive
                && t.TransactionDate >= fromDate && t.TransactionDate <= toDate
                && t.Category == FinancialCategory.SalaryAdvance)
            .SumAsync(t => (decimal?)t.Amount) ?? 0;

        var netProfit = totalCollected - totalExpenses - totalRefunds - totalSupplierPayments - totalSalaryAdvances;

        return Ok(new { fromDate = fromDate.ToString("yyyy-MM-dd"), toDate = toDate.ToString("yyyy-MM-dd"), totalCollected, totalExpenses, totalRefunds, totalSupplierPayments, totalSalaryAdvances, netProfit, daily = payments, bySpecialty, byMethod });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 11 New Report Endpoints
    // ─────────────────────────────────────────────────────────────────────────────

    // 1. GET /api/reports/patient-demographics
    [HttpGet("patient-demographics")]
    public async Task<IActionResult> GetPatientDemographics([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-365)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Age distribution
        var patients = await db.Patients
            .Where(p => p.IsActive && p.DateOfBirth != null)
            .Select(p => new { p.Id, p.DateOfBirth, p.Gender, p.ReferralSource, p.CreatedAt })
            .ToListAsync();

        var ageBuckets = new Dictionary<string, int>
        {
            ["أقل من 18"] = 0, // under-18
            ["18-30"] = 0,
            ["31-45"] = 0,
            ["46-60"] = 0,
            ["أكثر من 60"] = 0  // 60+
        };

        foreach (var p in patients)
        {
            if (p.DateOfBirth == null) continue;
            var age = today.Year - p.DateOfBirth.Value.Year;
            if (p.DateOfBirth.Value > today.AddYears(-age)) age--;

            if (age < 18) ageBuckets["أقل من 18"]++;
            else if (age <= 30) ageBuckets["18-30"]++;
            else if (age <= 45) ageBuckets["31-45"]++;
            else if (age <= 60) ageBuckets["46-60"]++;
            else ageBuckets["أكثر من 60"]++;
        }

        var ageDistribution = ageBuckets.Select(kv => new { bucket = kv.Key, count = kv.Value }).ToList();

        // Gender split
        var totalWithGender = patients.Count(p => p.Gender != null);
        var maleCount = patients.Count(p => p.Gender == Domain.Enums.Gender.Male);
        var femaleCount = patients.Count(p => p.Gender == Domain.Enums.Gender.Female);
        var genderSplit = new[]
        {
            new { label = "ذكر", count = maleCount, percentage = totalWithGender > 0 ? Math.Round((double)maleCount / totalWithGender * 100, 1) : 0 },
            new { label = "أنثى", count = femaleCount, percentage = totalWithGender > 0 ? Math.Round((double)femaleCount / totalWithGender * 100, 1) : 0 }
        };

        // Referral sources breakdown
        var referralSources = patients
            .Where(p => !string.IsNullOrEmpty(p.ReferralSource))
            .GroupBy(p => p.ReferralSource!)
            .Select(g => new { source = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // New patients per month (last 12 months)
        var twelveMonthsAgo = today.AddMonths(-12);
        var newPatientsRaw = await db.Patients
            .Where(p => p.IsActive && DateOnly.FromDateTime(p.CreatedAt.Date) >= twelveMonthsAgo)
            .GroupBy(p => new { DateOnly.FromDateTime(p.CreatedAt.Date).Year, DateOnly.FromDateTime(p.CreatedAt.Date).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var newPatientsPerMonth = newPatientsRaw
            .Select(x => new { month = $"{x.Year}-{x.Month:D2}", count = x.Count })
            .OrderBy(x => x.month)
            .ToList();

        // Active patients (at least 1 appointment in period)
        var activePatientIds = await db.Appointments
            .Where(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate)
            .Select(a => a.PatientId)
            .Distinct()
            .CountAsync();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            ageDistribution,
            genderSplit,
            referralSources,
            newPatientsPerMonth,
            activePatients = activePatientIds
        });
    }

    // 2. GET /api/reports/appointment-analytics
    [HttpGet("appointment-analytics")]
    public async Task<IActionResult> GetAppointmentAnalytics([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var appointments = await db.Appointments
            .Where(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate)
            .Select(a => new { a.Status, a.StartTime, a.AppointmentDate, a.AppointmentType })
            .ToListAsync();

        var total = appointments.Count;
        if (total == 0)
        {
            return Ok(new
            {
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                totalAppointments = 0,
                statusDistribution = Array.Empty<object>(),
                peakHours = Array.Empty<object>(),
                averagePerDay = 0m,
                noShowRate = 0m,
                cancellationRate = 0m,
                completionRate = 0m,
                byType = Array.Empty<object>()
            });
        }

        // Status distribution
        var statusDist = appointments
            .GroupBy(a => a.Status.ToString())
            .Select(g => new { status = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // Peak hours distribution
        var peakHours = appointments
            .GroupBy(a => a.StartTime.Hour)
            .Select(g => new { hour = g.Key, label = $"{g.Key:D2}:00", count = g.Count() })
            .OrderBy(x => x.hour)
            .ToList();

        // Average per day
        var distinctDays = appointments.Select(a => a.AppointmentDate).Distinct().Count();
        var avgPerDay = distinctDays > 0 ? Math.Round((decimal)total / distinctDays, 1) : 0;

        // Rates
        var noShowCount = appointments.Count(a => a.Status == Domain.Enums.AppointmentStatus.NoShow);
        var cancelledCount = appointments.Count(a => a.Status == Domain.Enums.AppointmentStatus.Cancelled);
        var completedCount = appointments.Count(a => a.Status == Domain.Enums.AppointmentStatus.Completed);
        var noShowRate = Math.Round((decimal)noShowCount / total * 100, 1);
        var cancellationRate = Math.Round((decimal)cancelledCount / total * 100, 1);
        var completionRate = Math.Round((decimal)completedCount / total * 100, 1);

        // By type
        var byType = appointments
            .Where(a => !string.IsNullOrEmpty(a.AppointmentType))
            .GroupBy(a => a.AppointmentType!)
            .Select(g => new { type = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            totalAppointments = total,
            statusDistribution = statusDist,
            peakHours,
            averagePerDay = avgPerDay,
            noShowRate,
            cancellationRate,
            completionRate,
            byType
        });
    }

    // 3. GET /api/reports/overdue-contracts
    [HttpGet("overdue-contracts")]
    public async Task<IActionResult> GetOverdueContracts([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Active contracts where paid < total
        var contracts = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.IsActive && c.Status == ContractStatus.Active && c.TotalAmount > 0)
            .ToListAsync();

        var overdueList = new List<object>();
        var aging1_30 = 0m;
        var aging31_60 = 0m;
        var aging61_90 = 0m;
        var aging90Plus = 0m;
        var totalOverdue = 0m;

        foreach (var c in contracts)
        {
            var paid = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            var remaining = c.TotalAmount - paid;
            if (remaining <= 0) continue;

            // Phase 0B: Calculate days overdue based on expected installment schedule.
            // A contract is "overdue" if the expected payments exceed actual payments,
            // and the overdue period starts from when the expected payment was due.
            var daysOverdue = 0;
            if (c.StartDate.HasValue && c.InstallmentAmount > 0)
            {
                var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12) + (today.Month - c.StartDate.Value.Month);
                if (monthsElapsed > 0)
                {
                    var expectedPaid = c.DownPayment + Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0);
                    if (expectedPaid > paid)
                    {
                        // The overdue starts from when the first missed installment was due
                        var monthsPaidOnSchedule = Math.Max(0, (int)Math.Floor((paid - c.DownPayment) / (c.InstallmentAmount ?? 1)));
                        var firstOverdueMonth = monthsPaidOnSchedule + 1;
                        var dueDateOfFirstOverdue = c.StartDate.Value.AddMonths(firstOverdueMonth);
                        daysOverdue = Math.Max(0, today.DayNumber - dueDateOfFirstOverdue.DayNumber);
                    }
                }
            }

            overdueList.Add(new
            {
                contractId = c.Id,
                patientName = $"{c.Patient.FirstName} {c.Patient.LastName}".Trim(),
                totalAmount = c.TotalAmount,
                paidAmount = paid,
                remaining,
                daysOverdue
            });

            totalOverdue += remaining;

            if (daysOverdue <= 30) aging1_30 += remaining;
            else if (daysOverdue <= 60) aging31_60 += remaining;
            else if (daysOverdue <= 90) aging61_90 += remaining;
            else aging90Plus += remaining;
        }

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            overdueContracts = overdueList,
            agingBuckets = new[]
            {
                new { bucket = "1-30 يوم", amount = aging1_30 },
                new { bucket = "31-60 يوم", amount = aging31_60 },
                new { bucket = "61-90 يوم", amount = aging61_90 },
                new { bucket = "أكثر من 90 يوم", amount = aging90Plus }
            },
            totalOverdueAmount = totalOverdue
        });
    }

    // 4. GET /api/reports/ortho-progress
    [HttpGet("ortho-progress")]
    public async Task<IActionResult> GetOrthoProgress([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        // Cases by status
        var casesByStatus = await db.OrthoCases
            .Where(c => c.IsActive)
            .GroupBy(c => c.Status.ToString())
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        // Average treatment duration for completed cases
        var completedCases = await db.OrthoCases
            .Where(c => c.IsActive && c.Status == OrthoCaseStatus.Completed && c.StartDate.HasValue)
            .Select(c => new { c.StartDate, c.CreatedAt })
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var avgDuration = completedCases.Count > 0
            ? Math.Round(completedCases.Average(c => (today.DayNumber - c.StartDate!.Value.DayNumber)) / 30.0, 1)
            : 0;

        // Cases per doctor
        var casesPerDoctor = await db.OrthoCases
            .Where(c => c.IsActive && c.DoctorId != null)
            .GroupBy(c => c.DoctorId!.Value)
            .Select(g => new { doctorId = g.Key, count = g.Count() })
            .ToListAsync();

        var doctorIds = casesPerDoctor.Select(x => x.doctorId).ToList();
        var doctors = await db.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        var casesPerDoctorNamed = casesPerDoctor.Select(x => new
        {
            doctorId = x.doctorId,
            doctorName = doctors.FirstOrDefault(d => d.Id == x.doctorId)?.Name ?? "غير معروف",
            count = x.count
        }).ToList();

        // Revenue from ortho payments
        var orthoRevenue = await db.Payments
            .Where(p => p.IsActive && p.Specialty == "Orthodontics" && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            casesByStatus,
            averageTreatmentDurationMonths = avgDuration,
            casesPerDoctor = casesPerDoctorNamed,
            orthoRevenue
        });
    }

    // 5. GET /api/reports/surgery-stats
    [HttpGet("surgery-stats")]
    public async Task<IActionResult> GetSurgeryStats([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        // Cases by type
        var casesByType = await db.SurgeryCases
            .Where(c => c.IsActive && DateOnly.FromDateTime(c.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(c.CreatedAt.Date) <= toDate)
            .GroupBy(c => c.SurgeryType)
            .Select(g => new { type = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToListAsync();

        // Cases per doctor
        var casesPerDoctorRaw = await db.SurgeryCases
            .Where(c => c.IsActive && c.DoctorId != null && DateOnly.FromDateTime(c.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(c.CreatedAt.Date) <= toDate)
            .GroupBy(c => c.DoctorId!.Value)
            .Select(g => new { doctorId = g.Key, count = g.Count() })
            .ToListAsync();

        var doctorIds = casesPerDoctorRaw.Select(x => x.doctorId).ToList();
        var doctors = await db.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        var casesPerDoctor = casesPerDoctorRaw.Select(x => new
        {
            doctorId = x.doctorId,
            doctorName = doctors.FirstOrDefault(d => d.Id == x.doctorId)?.Name ?? "غير معروف",
            count = x.count
        }).ToList();

        // Monthly trend
        var monthlyRaw = await db.SurgeryCases
            .Where(c => c.IsActive && DateOnly.FromDateTime(c.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(c.CreatedAt.Date) <= toDate)
            .GroupBy(c => new { DateOnly.FromDateTime(c.CreatedAt.Date).Year, DateOnly.FromDateTime(c.CreatedAt.Date).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, count = g.Count() })
            .ToListAsync();

        var monthlyTrend = monthlyRaw
            .Select(x => new { month = $"{x.Year}-{x.Month:D2}", count = x.count })
            .OrderBy(x => x.month)
            .ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            casesByType,
            casesPerDoctor,
            monthlyTrend
        });
    }

    // 6. GET /api/reports/revenue-comparison
    [HttpGet("revenue-comparison")]
    public async Task<IActionResult> GetRevenueComparison([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        // Current period
        var currentTotal = await db.Payments
            .Where(p => p.IsActive && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        // Previous period (same duration before `from`)
        var periodDays = toDate.DayNumber - fromDate.DayNumber;
        if (periodDays < 1) periodDays = 1;
        var prevFromDate = fromDate.AddDays(-periodDays);
        var prevToDate = fromDate.AddDays(-1);

        var previousTotal = await db.Payments
            .Where(p => p.IsActive && p.PaymentDate >= prevFromDate && p.PaymentDate <= prevToDate)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        var percentageChange = previousTotal > 0
            ? Math.Round((double)((currentTotal - previousTotal) / previousTotal) * 100, 1)
            : (currentTotal > 0 ? 100.0 : 0.0);

        // Month-over-month breakdown
        var currentMonthly = await db.Payments
            .Where(p => p.IsActive && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var previousMonthly = await db.Payments
            .Where(p => p.IsActive && p.PaymentDate >= prevFromDate && p.PaymentDate <= prevToDate)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var monthlyComparison = currentMonthly
            .Select(c => new
            {
                month = $"{c.Year}-{c.Month:D2}",
                currentPeriod = c.total,
                previousPeriod = previousMonthly.FirstOrDefault(p => p.Year == c.Year && p.Month == c.Month)?.total ?? 0
            })
            .OrderBy(x => x.month)
            .ToList();

        // By specialty comparison
        var currentBySpecialty = await db.Payments
            .Where(p => p.IsActive && p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .GroupBy(p => p.Specialty ?? "أخرى")
            .Select(g => new { specialty = g.Key, total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var previousBySpecialty = await db.Payments
            .Where(p => p.IsActive && p.PaymentDate >= prevFromDate && p.PaymentDate <= prevToDate)
            .GroupBy(p => p.Specialty ?? "أخرى")
            .Select(g => new { specialty = g.Key, total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var specialtyComparison = currentBySpecialty
            .Select(c => new
            {
                specialty = c.specialty,
                currentPeriod = c.total,
                previousPeriod = previousBySpecialty.FirstOrDefault(p => p.specialty == c.specialty)?.total ?? 0
            })
            .OrderByDescending(x => x.currentPeriod)
            .ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            previousPeriod = new { from = prevFromDate.ToString("yyyy-MM-dd"), to = prevToDate.ToString("yyyy-MM-dd") },
            currentPeriodTotal = currentTotal,
            previousPeriodTotal = previousTotal,
            percentageChange,
            monthlyComparison,
            specialtyComparison
        });
    }

    // 7. GET /api/reports/treatment-plan-completion
    [HttpGet("treatment-plan-completion")]
    public async Task<IActionResult> GetTreatmentPlanCompletion([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var steps = await db.PatientTreatmentPlanSteps
            .Where(s => s.IsActive && DateOnly.FromDateTime(s.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(s.CreatedAt.Date) <= toDate)
            .Select(s => new { s.Status, s.ResponsibleDoctorId })
            .ToListAsync();

        var total = steps.Count;
        if (total == 0)
        {
            return Ok(new
            {
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                totalSteps = 0,
                stepsByStatus = Array.Empty<object>(),
                completionRate = 0m,
                perDoctor = Array.Empty<object>()
            });
        }

        // Steps by status
        var stepsByStatus = steps
            .GroupBy(s => s.Status.ToString())
            .Select(g => new { status = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // Completion rate
        var completedCount = steps.Count(s => s.Status == Domain.Enums.TreatmentStepStatus.Completed);
        var completionRate = Math.Round((decimal)completedCount / total * 100, 1);

        // Per-doctor breakdown
        var perDoctorRaw = steps
            .Where(s => s.ResponsibleDoctorId != null)
            .GroupBy(s => s.ResponsibleDoctorId!.Value)
            .Select(g => new
            {
                doctorId = g.Key,
                total = g.Count(),
                completed = g.Count(s => s.Status == Domain.Enums.TreatmentStepStatus.Completed)
            })
            .ToList();

        var doctorIds = perDoctorRaw.Select(x => x.doctorId).ToList();
        var doctors = await db.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        var perDoctor = perDoctorRaw.Select(x => new
        {
            doctorId = x.doctorId,
            doctorName = doctors.FirstOrDefault(d => d.Id == x.doctorId)?.Name ?? "غير معروف",
            totalSteps = x.total,
            completedSteps = x.completed,
            completionRate = Math.Round((decimal)x.completed / x.total * 100, 1)
        }).ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            totalSteps = total,
            stepsByStatus,
            completionRate,
            perDoctor
        });
    }

    // 8. GET /api/reports/lab-orders
    [HttpGet("lab-orders")]
    public async Task<IActionResult> GetLabOrders([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Orders by status
        var ordersByStatus = await db.LabOrders
            .Where(o => o.IsActive && DateOnly.FromDateTime(o.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(o.CreatedAt.Date) <= toDate)
            .GroupBy(o => o.Status ?? "unknown")
            .Select(g => new { status = g.Key, count = g.Count() })
            .ToListAsync();

        // Per-doctor count
        var perDoctorRaw = await db.LabOrders
            .Where(o => o.IsActive && o.DoctorId != null && DateOnly.FromDateTime(o.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(o.CreatedAt.Date) <= toDate)
            .GroupBy(o => o.DoctorId!.Value)
            .Select(g => new { doctorId = g.Key, count = g.Count() })
            .ToListAsync();

        var doctorIds = perDoctorRaw.Select(x => x.doctorId).ToList();
        var doctors = await db.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        var perDoctor = perDoctorRaw.Select(x => new
        {
            doctorId = x.doctorId,
            doctorName = doctors.FirstOrDefault(d => d.Id == x.doctorId)?.Name ?? "غير معروف",
            count = x.count
        }).ToList();

        // Overdue orders: ExpectedDate < today AND not received
        var overdueCount = await db.LabOrders
            .Where(o => o.IsActive && o.ExpectedDate != null && o.ExpectedDate < today && o.ReceivedDate == null && o.Status != "received" && o.Status != "cancelled")
            .CountAsync();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            ordersByStatus,
            perDoctor,
            overdueOrdersCount = overdueCount
        });
    }

    // 9. GET /api/reports/prescription-analytics
    [HttpGet("prescription-analytics")]
    public async Task<IActionResult> GetPrescriptionAnalytics([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var prescriptions = await db.Prescriptions
            .Where(p => p.IsActive && DateOnly.FromDateTime(p.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(p.CreatedAt.Date) <= toDate)
            .Select(p => new { p.DoctorId, p.Drugs, p.CreatedAt })
            .ToListAsync();

        // Most prescribed items — parse Drugs JsonDocument in memory
        var drugCounts = new Dictionary<string, int>();
        foreach (var p in prescriptions)
        {
            try
            {
                var root = p.Drugs.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var name = item.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                        if (!string.IsNullOrEmpty(name))
                        {
                            drugCounts.TryGetValue(name, out var current);
                            drugCounts[name] = current + 1;
                        }
                    }
                }
            }
            catch
            {
                // Skip malformed JSON
            }
        }

        var mostPrescribed = drugCounts
            .OrderByDescending(x => x.Value)
            .Take(20)
            .Select(x => new { drugName = x.Key, count = x.Value })
            .ToList();

        // Per-doctor count
        var perDoctorRaw = prescriptions
            .Where(p => p.DoctorId != null)
            .GroupBy(p => p.DoctorId!.Value)
            .Select(g => new { doctorId = g.Key, count = g.Count() })
            .ToList();

        var doctorIds = perDoctorRaw.Select(x => x.doctorId).ToList();
        var doctors = await db.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Name })
            .ToListAsync();

        var perDoctor = perDoctorRaw.Select(x => new
        {
            doctorId = x.doctorId,
            doctorName = doctors.FirstOrDefault(d => d.Id == x.doctorId)?.Name ?? "غير معروف",
            count = x.count
        }).ToList();

        // Monthly trend
        var monthlyTrend = prescriptions
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { month = $"{g.Key.Year}-{g.Key.Month:D2}", count = g.Count() })
            .OrderBy(x => x.month)
            .ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            mostPrescribed,
            perDoctor,
            monthlyTrend
        });
    }

    // 10. GET /api/reports/patient-retention
    [HttpGet("patient-retention")]
    public async Task<IActionResult> GetPatientRetention([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-90)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        // New vs returning patients in period
        var patientFirstAppointment = await db.Appointments
            .Where(a => a.IsActive)
            .GroupBy(a => a.PatientId)
            .Select(g => new { PatientId = g.Key, FirstDate = g.Min(a => a.AppointmentDate) })
            .ToListAsync();

        var patientsInPeriod = await db.Appointments
            .Where(a => a.IsActive && a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate)
            .Select(a => a.PatientId)
            .Distinct()
            .ToListAsync();

        var newPatients = 0;
        var returningPatients = 0;
        foreach (var pid in patientsInPeriod)
        {
            var firstDate = patientFirstAppointment.FirstOrDefault(x => x.PatientId == pid)?.FirstDate;
            if (firstDate == null || firstDate >= fromDate) newPatients++;
            else returningPatients++;
        }

        // Average days between visits
        var visitDates = await db.Appointments
            .Where(a => a.IsActive && a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate && a.Status == Domain.Enums.AppointmentStatus.Completed)
            .GroupBy(a => a.PatientId)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Select(a => a.AppointmentDate).OrderBy(d => d).ToList())
            .ToListAsync();

        var avgDaysBetweenVisits = 0m;
        if (visitDates.Count > 0)
        {
            var allGaps = new List<int>();
            foreach (var dates in visitDates)
            {
                for (int i = 1; i < dates.Count; i++)
                {
                    allGaps.Add(dates[i].DayNumber - dates[i - 1].DayNumber);
                }
            }
            if (allGaps.Count > 0)
            {
                avgDaysBetweenVisits = Math.Round((decimal)allGaps.Average(), 1);
            }
        }

        // Churn risk: patients with no visit in 90+ days
        var today = DateOnly.FromDateTime(DateTime.Today);
        var ninetyDaysAgo = today.AddDays(-90);

        var recentPatientIds = await db.Appointments
            .Where(a => a.IsActive && a.AppointmentDate >= ninetyDaysAgo)
            .Select(a => a.PatientId)
            .Distinct()
            .ToListAsync();

        var churnRiskCount = await db.Patients
            .Where(p => p.IsActive && !recentPatientIds.Contains(p.Id))
            .CountAsync();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            newPatients,
            returningPatients,
            averageDaysBetweenVisits = avgDaysBetweenVisits,
            churnRiskPatients = churnRiskCount
        });
    }

    // 11. GET /api/reports/booking-funnel
    [HttpGet("booking-funnel")]
    public async Task<IActionResult> GetBookingFunnel([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var bookings = await db.BookingRequests
            .Where(b => b.IsActive && DateOnly.FromDateTime(b.CreatedAt.Date) >= fromDate && DateOnly.FromDateTime(b.CreatedAt.Date) <= toDate)
            .Select(b => new { b.Status, b.ServiceType, b.ConvertedToAppointmentId })
            .ToListAsync();

        var total = bookings.Count;
        if (total == 0)
        {
            return Ok(new
            {
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                totalRequests = 0,
                byStatus = Array.Empty<object>(),
                conversionRate = 0m,
                appointmentCreationRate = 0m,
                perService = Array.Empty<object>()
            });
        }

        // By status
        var byStatus = bookings
            .GroupBy(b => b.Status.ToString())
            .Select(g => new { status = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // Conversion rate (Confirmed / Total)
        var confirmedCount = bookings.Count(b => b.Status == BookingRequestStatus.Confirmed);
        var conversionRate = Math.Round((decimal)confirmedCount / total * 100, 1);

        // Appointment creation rate (convertedToAppointmentId != null / Confirmed)
        var convertedToAppointment = bookings.Count(b => b.Status == BookingRequestStatus.Confirmed && b.ConvertedToAppointmentId != null);
        var appointmentCreationRate = confirmedCount > 0
            ? Math.Round((decimal)convertedToAppointment / confirmedCount * 100, 1)
            : 0m;

        // Per-service breakdown
        var perService = bookings
            .Where(b => !string.IsNullOrEmpty(b.ServiceType))
            .GroupBy(b => b.ServiceType!)
            .Select(g => new
            {
                service = g.Key,
                total = g.Count(),
                confirmed = g.Count(b => b.Status == BookingRequestStatus.Confirmed),
                converted = g.Count(b => b.Status == BookingRequestStatus.Confirmed && b.ConvertedToAppointmentId != null)
            })
            .OrderByDescending(x => x.total)
            .ToList();

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            totalRequests = total,
            byStatus,
            conversionRate,
            appointmentCreationRate,
            perService
        });
    }

    // GET /api/reports/export/patients  — CSV تصدير المرضى
    [HttpGet("export/patients")]
    public async Task<IActionResult> ExportPatients()
    {
        var patients = await db.Patients
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.PatientNumber,
                p.FirstName,
                p.LastName,
                p.DateOfBirth,
                p.Gender,
                p.Phone,
                p.Address,
                p.CreatedAt
            })
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("رقم المريض,الاسم الكامل,تاريخ الميلاد,الجنس,رقم الهاتف,العنوان,تاريخ التسجيل");
        foreach (var p in patients)
        {
            var fullName = $"{p.FirstName} {p.LastName}".Trim();
            csv.AppendLine($"{p.PatientNumber},{Esc(fullName)},{p.DateOfBirth?.ToString("yyyy-MM-dd") ?? ""},{p.Gender},{Esc(p.Phone ?? "")},{Esc(p.Address ?? "")},{p.CreatedAt:yyyy-MM-dd}");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"patients_{DateTime.Today:yyyyMMdd}.csv");
    }

    // GET /api/reports/export/payments?from=&to=  — CSV تصدير الدفعات
    [HttpGet("export/payments")]
    public async Task<IActionResult> ExportPayments([FromQuery] string? from, [FromQuery] string? to)
    {
        // ERR-01 FIX: Safe date parsing
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var payments = await db.Payments
            .Include(p => p.Patient)
            .Where(p => p.PaymentDate >= fromDate && p.PaymentDate <= toDate)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new
            {
                p.ReceiptNumber,
                FirstName     = p.Patient.FirstName,
                LastName      = p.Patient.LastName,
                PatientNumber = p.Patient.PatientNumber,
                p.Amount,
                p.PaymentMethod,
                p.PaymentDate,
                p.ServiceDescription,
                p.Specialty,
                p.Notes
            })
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("رقم السند,المريض,رقم المريض,المبلغ,طريقة الدفع,التاريخ,الوصف,التخصص,ملاحظات");
        foreach (var p in payments)
        {
            var patientName = $"{p.FirstName} {p.LastName}".Trim();
            csv.AppendLine($"{Esc(p.ReceiptNumber ?? "")},{Esc(patientName)},{p.PatientNumber},{p.Amount},{p.PaymentMethod},{p.PaymentDate:yyyy-MM-dd},{Esc(p.ServiceDescription ?? "")},{Esc(p.Specialty ?? "")},{Esc(p.Notes ?? "")}");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"payments_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
    }

    // GET /api/reports/export/appointments?from=&to=
    [HttpGet("export/appointments")]
    public async Task<IActionResult> ExportAppointments([FromQuery] string? from, [FromQuery] string? to)
    {
        // ERR-01 FIX: Safe date parsing
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        var appts = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate)
            .OrderByDescending(a => a.AppointmentDate)
            .Select(a => new
            {
                FirstName      = a.Patient.FirstName,
                LastName       = a.Patient.LastName,
                PatientNumber  = a.Patient.PatientNumber,
                DoctorName     = a.Doctor != null ? a.Doctor.Name : "",
                AppointmentDate = a.AppointmentDate,
                a.StartTime,
                a.AppointmentType,
                a.Status,
                a.Notes
            })
            .ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("المريض,رقم المريض,الطبيب,التاريخ,الوقت,نوع الموعد,الحالة,ملاحظات");
        foreach (var a in appts)
        {
            var patientName = $"{a.FirstName} {a.LastName}".Trim();
            csv.AppendLine($"{Esc(patientName)},{a.PatientNumber},{Esc(a.DoctorName)},{a.AppointmentDate:yyyy-MM-dd},{a.StartTime},{Esc(a.AppointmentType ?? "")},{a.Status},{Esc(a.Notes ?? "")}");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"appointments_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv");
    }

    private static string Esc(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    [HttpGet("pdf/financial-statement/{patientId:guid}")]
    public async Task<IActionResult> GetFinancialStatementPdf(Guid patientId)
    {
        try
        {
            var pdfBytes = await pdfService.GenerateFinancialStatementAsync(patientId);
            return File(pdfBytes, "application/pdf", $"financial-statement-{patientId}.pdf");
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Financial statement PDF generation failed for patient {PatientId}", patientId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error generating financial statement PDF for patient {PatientId}", patientId);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء كشف الحساب" });
        }
    }

    // ─── GET /api/reports/profit-loss — Consolidated P&L Statement ──────────
    // Sprint 4: Now reads from JournalEntry/JournalLine (Finance V3 canonical source)
    // instead of CashFlowTransaction (transitional/legacy) to avoid schema mismatch 500s.
    // Response shape preserved for frontend compatibility.
    [HttpGet("profit-loss")]
    [Authorize(Policy = "ReportsAccess")] // Admin + Accountant only
    public async Task<IActionResult> GetProfitLossReport([FromQuery] string? from, [FromQuery] string? to)
    {
        var (fromDate, fromErr) = DateParsingHelper.TryParseDateOrDefault(from, DateOnly.FromDateTime(DateTime.Today.AddDays(-30)), "تاريخ البداية");
        if (fromErr != null) return fromErr;
        var (toDate, toErr) = DateParsingHelper.TryParseDateOrDefault(to, DateOnly.FromDateTime(DateTime.Today), "تاريخ النهاية");
        if (toErr != null) return toErr;

        // Branch isolation: admin sees consolidated, non-admin scoped to their branch
        Guid? branchId = null;
        if (!currentUser.IsAdmin && currentUser.BranchId.HasValue && currentUser.BranchId.Value != Guid.Empty)
            branchId = currentUser.BranchId.Value;

        try
        {
            // ── Revenue from Treasury JournalLines (canonical source) ──
            // Treasury Debit from Payment entries (non-reversal) = cash collected from patients
            var grossRevenue = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Debit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.Payment
                    && !l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            // Refunds: Treasury Credit from Refund entries (non-reversal)
            var totalRefunds = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Credit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.Refund
                    && !l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            // Payment reversals (deleted payments): Treasury Credit from PaymentDeletion reversal entries
            var paymentReversals = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Credit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.PaymentDeletion
                    && l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            var netRevenue = grossRevenue - totalRefunds - paymentReversals;

            // ── Direct Costs (COGS) from Expense JournalLines ──
            // OperationalExpense outflows net of reversals
            var operatingExpenses = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Credit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.Expense
                    && !l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            var operatingExpenseReversals = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Debit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.Expense
                    && l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Debit) ?? 0;

            var totalDirectCosts = operatingExpenses - operatingExpenseReversals;
            var labFees = totalDirectCosts; // Legacy field: all operational expenses treated as direct costs
            var clinicSupplies = 0m; // Not separately tracked in JournalLine; consolidated into labFees
            var grossProfit = netRevenue - totalDirectCosts;

            // ── Operating Expenses (OPEX) from Treasury JournalLines ──
            // Salary payments: net of reversals
            var salariesPaid = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Credit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.SalaryPayment
                    && !l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            var salariesReversals = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Debit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.SalaryPayment
                    && l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Debit) ?? 0;
            salariesPaid -= salariesReversals;

            // Commission payments: net of reversals
            var commissionsPaid = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Credit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.CommissionPayment
                    && !l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            // C4 FIX: salaryAdvances are NOT an expense — they're a temporary cash outflow
            // that gets recovered from salaries (محصلة من الرواتب). Including them in OPEX
            // double-counts because they are also deducted from SalaryRecord.Advances.
            var salaryAdvances = 0m; // AdvancePayment now tracked via JournalLine; not an OPEX expense

            // Supplier payments: net of reversals
            var supplierPayments = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Credit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.SupplierPayment
                    && !l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Credit) ?? 0;

            var supplierReversals = await db.JournalLines
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.Debit > 0
                    && l.JournalEntry.FinancialDocumentType == FinancialDocumentType.SupplierPayment
                    && l.JournalEntry.IsReversal
                    && l.JournalEntry.EntryDate >= fromDate && l.JournalEntry.EntryDate <= toDate
                    && l.JournalEntry.IsPosted
                    && (!branchId.HasValue || l.BranchId == branchId.Value))
                .SumAsync(l => (decimal?)l.Debit) ?? 0;
            supplierPayments -= supplierReversals;

            // Sub-categories within OperationalExpense are not separately tracked in JournalLine.
            // They are consolidated under the parent OPEX figure for the legacy response shape.
            var rent = 0m;
            var utilities = 0m;
            var marketing = 0m;
            var maintenance = 0m;
            var taxes = 0m;
            var opexMiscellaneous = 0m;

            // C4 FIX: salaryAdvances removed from OPEX total — not an expense, just a temporary outflow
            var totalOpex = salariesPaid + commissionsPaid + supplierPayments;
            var netProfit = grossProfit - totalOpex;

            return Ok(new
            {
                fromDate = fromDate.ToString("yyyy-MM-dd"),
                toDate = toDate.ToString("yyyy-MM-dd"),
                
                // Revenues
                grossRevenue,
                totalRefunds,
                netRevenue,

                // Direct Costs
                labFees,
                clinicSupplies,
                totalDirectCosts,
                grossProfit,

                // Operating Expenses (OPEX)
                salariesPaid,
                commissionsPaid,
                supplierPayments,
                rent,
                utilities,
                marketing,
                maintenance,
                taxes,
                opexMiscellaneous,
                totalOpex,

                // C4 FIX: Salary Advances shown separately — NOT in OPEX
                // These are temporary cash outflows recovered from salaries (محصلة من الرواتب)
                salaryAdvances,
                salaryAdvancesNote = "محصلة من الرواتب",

                // Net Bottom Line
                netProfit
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reports.GetProfitLossReport failed for period {From}-{To}", fromDate, toDate);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل تقرير الأرباح والخسائر" });
        }
    }

    // ─── H4: Daily Cash Summary Report ────────────────────────────────────
    // Sprint 4: Now reads from JournalEntry/JournalLine (Finance V3 canonical source)
    // instead of CashFlowTransaction (transitional/legacy) to avoid schema mismatch errors.
    // Admin branch fallback added: if admin has no branch, falls back to first active branch.
    // Response shape preserved for frontend compatibility.
    [HttpGet("daily-cash-summary")]
    public async Task<IActionResult> GetDailyCashSummary([FromQuery] string? date, [FromQuery] Guid? branchId)
    {
        var (reportDate, dateErr) = DateParsingHelper.TryParseDateOrDefault(date, DateOnly.FromDateTime(DateTime.Today), "التاريخ");
        if (dateErr != null) return dateErr;

        // Determine branch scope
        Guid? effectiveBranchId = branchId ?? currentUser.BranchId;
        if (!currentUser.IsAdmin && currentUser.BranchId.HasValue)
            effectiveBranchId = currentUser.BranchId.Value;

        // Sprint 4: Admin without branch — fallback to first active branch instead of 400
        if (effectiveBranchId == null || effectiveBranchId == Guid.Empty)
        {
            if (currentUser.IsAdmin)
            {
                var firstBranch = await db.Branches
                    .Where(b => b.IsActive)
                    .OrderBy(b => b.Name)
                    .Select(b => (Guid?)b.Id)
                    .FirstOrDefaultAsync();
                effectiveBranchId = firstBranch;
            }
            if (effectiveBranchId == null || effectiveBranchId == Guid.Empty)
                return BadRequest(new { message = "يجب تحديد الفرع" });
        }

        // Branch name
        var branchName = await db.Branches
            .Where(b => b.Id == effectiveBranchId.Value)
            .Select(b => b.Name)
            .FirstOrDefaultAsync() ?? "غير معروف";

        // Opening balance: sum of OpeningBalance from sessions opened on this date for this branch
        var openingBalance = await db.CashierSessions
            .Where(s => s.BranchId == effectiveBranchId.Value && s.IsActive
                && DateOnly.FromDateTime(s.OpeningTime.Date) == reportDate)
            .SumAsync(s => (decimal?)s.OpeningBalance) ?? 0m;

        try
        {
            // ── Read from JournalLine (Treasury account type) — canonical source of truth ──
            var treasuryLines = await db.JournalLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountType == JournalAccountType.Treasury
                    && l.JournalEntry.EntryDate == reportDate
                    && l.JournalEntry.IsPosted
                    && l.BranchId == effectiveBranchId.Value)
                .Select(l => new
                {
                    l.Id,
                    l.Debit,
                    l.Credit,
                    l.JournalEntryId,
                    l.JournalEntry.FinancialDocumentType,
                    l.JournalEntry.IsReversal,
                    TreasuryId = l.AccountId
                })
                .ToListAsync();

            // Load treasury types for PaymentMethod mapping: Vault → "cash", Bank → "bank_transfer"
            var treasuryIds = treasuryLines.Select(l => l.TreasuryId).Distinct().ToList();
            var treasuryTypes = await db.Treasuries
                .Where(t => treasuryIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => (TreasuryType?)t.Type);

            // ── Build Inflows ──
            // Treasury Debit = Inflow (cash received into treasury)
            var inflowLines = treasuryLines.Where(l => l.Debit > 0).ToList();
            var inflowTransactions = inflowLines
                .GroupBy(l =>
                {
                    var tType = treasuryTypes.GetValueOrDefault(l.TreasuryId);
                    return tType == TreasuryType.Bank ? "bank_transfer" : "cash";
                })
                .Select(g => new
                {
                    PaymentMethod = g.Key,
                    Total = g.Sum(l => l.Debit),
                    Categories = g.GroupBy(l => MapDocumentTypeToCategory(l.FinancialDocumentType))
                        .Select(cg => new { Category = cg.Key, Amount = cg.Sum(l => l.Debit) })
                        .ToList()
                })
                .ToList();

            // ── Build Outflows ──
            // Treasury Credit = Outflow (cash paid out from treasury)
            var outflowLines = treasuryLines.Where(l => l.Credit > 0).ToList();
            var outflowTransactions = outflowLines
                .GroupBy(l =>
                {
                    var tType = treasuryTypes.GetValueOrDefault(l.TreasuryId);
                    return tType == TreasuryType.Bank ? "bank_transfer" : "cash";
                })
                .Select(g => new
                {
                    PaymentMethod = g.Key,
                    Total = g.Sum(l => l.Credit),
                    Categories = g.GroupBy(l => MapDocumentTypeToCategory(l.FinancialDocumentType))
                        .Select(cg => new { Category = cg.Key, Amount = cg.Sum(l => l.Credit) })
                        .ToList()
                })
                .ToList();

            var totalInflows = inflowTransactions.Sum(i => i.Total);
            var totalOutflows = outflowTransactions.Sum(o => o.Total);
            var closingBalance = openingBalance + totalInflows - totalOutflows;

            // Session summary for the day
            var sessions = await db.CashierSessions
                .Include(s => s.Cashier)
                .Where(s => s.BranchId == effectiveBranchId.Value && s.IsActive
                    && DateOnly.FromDateTime(s.OpeningTime.Date) == reportDate)
                .Select(s => new
                {
                    s.Id,
                    s.SessionNumber,
                    CashierName = s.Cashier.Username,
                    s.OpeningBalance,
                    s.ShortageOrSurplus,
                    Status = s.Status.ToString()
                })
                .ToListAsync();

            return Ok(new
            {
                Date = reportDate.ToString("yyyy-MM-dd"),
                BranchId = effectiveBranchId.Value,
                BranchName = branchName,
                OpeningBalance = openingBalance,
                Inflows = inflowTransactions,
                Outflows = outflowTransactions,
                TotalInflows = totalInflows,
                TotalOutflows = totalOutflows,
                NetCashFlow = totalInflows - totalOutflows,
                ClosingBalance = closingBalance,
                Sessions = sessions
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reports.GetDailyCashSummary failed for date {Date} branch {BranchId}", reportDate, effectiveBranchId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل ملخص النقد اليومي" });
        }
    }

    // ─── Helper: Map FinancialDocumentType to legacy Category strings ───
    // Mirrors FinanceV3Controller.Helpers.MapDocumentTypeToCategory for use in legacy reports.
    private static string MapDocumentTypeToCategory(FinancialDocumentType docType) => docType switch
    {
        FinancialDocumentType.Payment => "PatientPayment",
        FinancialDocumentType.Refund => "Refund",
        FinancialDocumentType.Expense => "OperationalExpense",
        FinancialDocumentType.SalaryPayment => "SalaryPayment",
        FinancialDocumentType.CommissionPayment => "DoctorCommission",
        FinancialDocumentType.SupplierPayment => "SupplierPayment",
        FinancialDocumentType.VaultTransfer => "InternalTransfer",
        FinancialDocumentType.ContractCancellation => "Reversal",
        FinancialDocumentType.PaymentDeletion => "Reversal",
        FinancialDocumentType.CreditNoteRefund => "Refund",
        FinancialDocumentType.Invoice => "Revenue",
        FinancialDocumentType.AdvancePayment => "AdvancePayment",
        _ => "Other"
    };
}
