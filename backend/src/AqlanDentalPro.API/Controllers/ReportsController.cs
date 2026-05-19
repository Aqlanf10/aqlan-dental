using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ReportsAccess")]
public class ReportsController(AppDbContext db, IPdfService pdfService, ILogger<ReportsController> logger) : ControllerBase
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
            .Where(c => c.DoctorId != null && doctorIds.Contains(c.DoctorId.Value) && c.Status == "active")
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

        return Ok(new { fromDate = fromDate.ToString("yyyy-MM-dd"), toDate = toDate.ToString("yyyy-MM-dd"), totalCollected, daily = payments, bySpecialty, byMethod });
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
    }
}
