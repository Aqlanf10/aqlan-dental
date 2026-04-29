using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/data-export")]
[Authorize(Policy = "AdminOnly")]
public class DataExportController(AppDbContext db) : ControllerBase
{
    // POST /api/data-export/patients
    [HttpPost("patients")]
    public async Task<IActionResult> ExportPatients([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var query = db.Patients.AsQueryable();

        if (from.HasValue)
            query = query.Where(p => p.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(p => p.CreatedAt <= to.Value);

        var patients = await query
            .OrderBy(p => p.PatientNumber)
            .Select(p => new
            {
                p.PatientNumber,
                p.FirstName,
                p.MiddleName,
                p.LastName,
                Gender = p.Gender != null ? p.Gender.ToString() : "",
                DateOfBirth = p.DateOfBirth.HasValue ? p.DateOfBirth.Value.ToString("yyyy-MM-dd") : "",
                p.Phone,
                p.WhatsApp,
                p.Address,
                p.Occupation,
                p.ReferralSource,
                PrimaryDoctor = p.PrimaryDoctor != null ? p.PrimaryDoctor.Name : "",
                Branch = p.Branch != null ? p.Branch.Name : "",
                IsActive = p.IsActive ? "نشط" : "غير نشط",
                CreatedAt = p.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .ToListAsync();

        var csv = BuildCsv(patients, new[]
        {
            "PatientNumber", "FirstName", "MiddleName", "LastName",
            "Gender", "DateOfBirth", "Phone", "WhatsApp",
            "Address", "Occupation", "ReferralSource", "PrimaryDoctor",
            "Branch", "IsActive", "CreatedAt"
        });

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"patients_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    // POST /api/data-export/appointments
    [HttpPost("appointments")]
    public async Task<IActionResult> ExportAppointments([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var query = db.Appointments.AsQueryable();

        if (from.HasValue)
            query = query.Where(a => a.AppointmentDate >= DateOnly.FromDateTime(from.Value));
        if (to.HasValue)
            query = query.Where(a => a.AppointmentDate <= DateOnly.FromDateTime(to.Value));

        var appointments = await query
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime)
            .Select(a => new
            {
                PatientName = a.Patient != null ? (a.Patient.FirstName + " " + a.Patient.LastName) : "",
                DoctorName = a.Doctor != null ? a.Doctor.Name : "",
                AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                StartTime = a.StartTime.ToString("HH:mm"),
                EndTime = a.EndTime.ToString("HH:mm"),
                DurationMinutes = a.DurationMinutes.ToString(),
                AppointmentType = a.AppointmentType,
                Specialty = a.Specialty != null ? a.Specialty.ToString() : "",
                Status = a.Status.ToString(),
                Branch = a.Branch != null ? a.Branch.Name : "",
                Notes = a.Notes ?? "",
            })
            .ToListAsync();

        var csv = BuildCsv(appointments, new[]
        {
            "PatientName", "DoctorName", "AppointmentDate", "StartTime",
            "EndTime", "DurationMinutes", "AppointmentType", "Specialty",
            "Status", "Branch", "Notes"
        });

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"appointments_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    // POST /api/data-export/finance
    [HttpPost("finance")]
    public async Task<IActionResult> ExportFinance([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        var sb = new StringBuilder();

        // ── Payments ─────────────────────────────────────────────────────────────
        var paymentQuery = db.Payments.AsQueryable();
        if (from.HasValue)
            paymentQuery = paymentQuery.Where(p => p.PaymentDate >= DateOnly.FromDateTime(from.Value));
        if (to.HasValue)
            paymentQuery = paymentQuery.Where(p => p.PaymentDate <= DateOnly.FromDateTime(to.Value));

        var payments = await paymentQuery
            .OrderBy(p => p.PaymentDate)
            .Select(p => new
            {
                Type = "دفع",
                Date = p.PaymentDate.ToString("yyyy-MM-dd"),
                PatientName = p.Patient != null ? (p.Patient.FirstName + " " + p.Patient.LastName) : "",
                Amount = p.Amount.ToString("F2"),
                PaymentMethod = p.PaymentMethod ?? "",
                Specialty = p.Specialty ?? "",
                ServiceDescription = p.ServiceDescription ?? "",
                DoctorName = p.Doctor != null ? p.Doctor.Name : "",
                Branch = p.Branch != null ? p.Branch.Name : "",
                Notes = p.Notes ?? "",
            })
            .ToListAsync();

        // ── Expenses ─────────────────────────────────────────────────────────────
        var expenseQuery = db.Expenses.AsQueryable();
        if (from.HasValue)
            expenseQuery = expenseQuery.Where(e => e.ExpenseDate >= DateOnly.FromDateTime(from.Value));
        if (to.HasValue)
            expenseQuery = expenseQuery.Where(e => e.ExpenseDate <= DateOnly.FromDateTime(to.Value));

        var expenses = await expenseQuery
            .OrderBy(e => e.ExpenseDate)
            .Select(e => new
            {
                Type = "مصروف",
                Date = e.ExpenseDate.ToString("yyyy-MM-dd"),
                PatientName = "",
                Amount = e.Amount.ToString("F2"),
                PaymentMethod = e.PaymentMethod ?? "",
                Specialty = "",
                ServiceDescription = e.Description,
                DoctorName = "",
                Branch = e.Branch != null ? e.Branch.Name : "",
                Notes = e.Notes ?? "",
            })
            .ToListAsync();

        var allRecords = payments.Concat(expenses)
            .OrderBy(r => r.Date)
            .ToList();

        var csv = BuildCsv(allRecords, new[]
        {
            "Type", "Date", "PatientName", "Amount",
            "PaymentMethod", "Specialty", "ServiceDescription",
            "DoctorName", "Branch", "Notes"
        });

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"finance_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    private static string BuildCsv<T>(List<T> records, string[] columns)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", columns.Select(EscapeCsv)));

        // Rows
        foreach (var record in records)
        {
            var type = typeof(T);
            var values = columns.Select(col =>
            {
                var prop = type.GetProperty(col);
                var val = prop?.GetValue(record)?.ToString() ?? "";
                return EscapeCsv(val);
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
