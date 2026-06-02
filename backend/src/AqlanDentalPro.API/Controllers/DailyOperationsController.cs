using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Sprint 2 — Daily Operations Report endpoint.
/// Aggregates appointments, queue, payments, lab orders, and audit data
/// for a given date to support the daily operations dashboard.
/// </summary>
[ApiController]
[Authorize(Policy = "StaffOnly")]
public class DailyOperationsController(AppDbContext db, ILogger<DailyOperationsController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the daily operations report for a given date (defaults to today).
    /// Includes patient counts, financial summary, lab order status, and audit overrides.
    /// </summary>
    [HttpGet("/api/daily-operations/report")]
    public async Task<IActionResult> GetDailyReport([FromQuery] string? date)
    {
        try
        {
            var reportDate = string.IsNullOrEmpty(date)
                ? DateOnly.FromDateTime(DateTime.Today)
                : DateOnly.Parse(date);

            var todayStart = DateTime.SpecifyKind(reportDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var todayEnd = DateTime.SpecifyKind(reportDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            var appointments = await db.Appointments
                .Include(a => a.Patient)
                .Where(a => a.AppointmentDate == reportDate && a.IsActive)
                .ToListAsync();

            var queueItems = await db.ClinicQueueItems
                .Where(q => q.CreatedAt >= todayStart && q.CreatedAt <= todayEnd && q.IsActive)
                .ToListAsync();

            var payments = await db.Payments
                .Include(p => p.Patient)
                .Where(p => p.PaymentDate == reportDate && p.IsActive)
                .ToListAsync();

            var labOrders = await db.LabOrders
                .Where(l => l.SentDate == reportDate || l.ExpectedDate == reportDate || l.ReceivedDate == reportDate)
                .ToListAsync();

            // Build report
            var report = new
            {
                Date = reportDate.ToString("yyyy-MM-dd"),
                PatientCounts = new
                {
                    Total = appointments.Count,
                    Waiting = queueItems.Count(q => q.Status == ClinicQueueStatus.Waiting),
                    InRoom = queueItems.Count(q => q.Status == ClinicQueueStatus.InRoom || q.Status == ClinicQueueStatus.InProgress),
                    ReadyForCheckout = appointments.Count(a => a.Status == AppointmentStatus.Completed),
                    Completed = appointments.Count(a => a.Status == AppointmentStatus.Completed),
                    NoShow = appointments.Count(a => a.Status == AppointmentStatus.NoShow),
                    LeftWithoutCompletion = 0, // Would need additional tracking
                    Emergency = appointments.Count(a => a.AppointmentType == "Emergency" || a.AppointmentType == "حالة إسعافية")
                },
                Financial = new
                {
                    TotalCollected = payments.Sum(p => p.Amount),
                    ByPaymentMethod = payments.GroupBy(p => p.PaymentMethod ?? "other")
                        .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() }),
                    NewDebts = 0, // Would calculate from invoices vs payments
                    PartialPayments = payments.Count(p => p.Amount > 0), // Simplified
                    DraftInvoices = await db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Draft && i.CreatedAt >= todayStart && i.CreatedAt <= todayEnd),
                    Discounts = 0m // Would need to calculate from invoice discounts
                },
                LabOrders = new
                {
                    Sent = labOrders.Count(l => l.Status == "sent"),
                    Received = labOrders.Count(l => l.Status == "received"),
                    Delivered = labOrders.Count(l => l.Status == "delivered")
                },
                ManagerOverrides = await db.AuditLogs.CountAsync(a => a.Action == AuditAction.Approve && a.CreatedAt >= todayStart && a.CreatedAt <= todayEnd),
                TomorrowAppointments = await db.Appointments.CountAsync(a => a.AppointmentDate == reportDate.AddDays(1) && a.IsActive)
            };

            return Ok(report);
        }
        catch (FormatException)
        {
            return BadRequest(new { message = "صيغة التاريخ غير صالحة. استخدم YYYY-MM-DD" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DailyOperations.GetDailyReport failed");
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل التقرير اليومي" });
        }
    }
}
