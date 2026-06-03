using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Sprint 2 — Daily Operations Report endpoint.
/// Aggregates appointments, queue, visits, payments, invoices, lab orders, and audit data
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

            // ── Appointments ──────────────────────────────────────────────
            var appointments = await db.Appointments
                .IgnoreQueryFilters()
                .Where(a => a.AppointmentDate == reportDate && a.IsActive)
                .ToListAsync();

            // ── Queue items ───────────────────────────────────────────────
            var queueItems = await db.ClinicQueueItems
                .IgnoreQueryFilters()
                .Where(q => q.QueueDate == reportDate && q.IsActive)
                .ToListAsync();

            // ── Visits (for checkout status) ──────────────────────────────
            var visits = await db.Visits
                .IgnoreQueryFilters()
                .Where(v => v.VisitDate == reportDate && v.IsActive)
                .ToListAsync();

            // ── Payments ──────────────────────────────────────────────────
            var payments = await db.Payments
                .Include(p => p.Invoice)
                .Where(p => p.PaymentDate == reportDate && p.IsActive)
                .ToListAsync();

            // ── Invoices ──────────────────────────────────────────────────
            var invoices = await db.Invoices
                .IgnoreQueryFilters()
                .Include(i => i.Payments.Where(p => p.IsActive))
                .Include(i => i.LineItems.Where(l => l.IsActive))
                .Where(i => i.IsActive && i.CreatedAt >= todayStart && i.CreatedAt <= todayEnd)
                .ToListAsync();

            // Filter inactive in-memory for InMemory provider compatibility
            foreach (var inv in invoices)
            {
                inv.Payments = inv.Payments.Where(p => p.IsActive).ToList();
                inv.LineItems = inv.LineItems.Where(l => l.IsActive).ToList();
            }

            // ── Lab orders ───────────────────────────────────────────────
            var labOrders = await db.LabOrders
                .Where(l => l.SentDate == reportDate || l.ExpectedDate == reportDate || l.ReceivedDate == reportDate)
                .ToListAsync();

            // ── Calculations ──────────────────────────────────────────────

            // ReadyForCheckout: visits with checkout status "ReadyForCheckout"
            var readyForCheckout = visits.Count(v => v.CheckoutStatus == "ReadyForCheckout");

            // Completed: appointments that reached Completed status
            var completed = appointments.Count(a => a.Status == AppointmentStatus.Completed);

            // LeftWithoutCompletion: InProgress appointments from a past date that were never completed
            // (For today's report, this is 0 since the day is not over yet)
            var leftWithoutCompletion = 0;

            // NewDebts: total of issued invoices minus total payments on those invoices
            var issuedInvoices = invoices.Where(i => i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.Paid).ToList();
            var totalInvoiced = issuedInvoices.Sum(i => i.TotalAmount);
            var totalPaidOnInvoices = issuedInvoices.Sum(i => i.Payments.Sum(p => p.Amount));
            var newDebts = Math.Max(0, totalInvoiced - totalPaidOnInvoices);

            // PartialPayments: payments where the invoice is NOT fully paid
            var partialPayments = payments.Count(p =>
                p.Invoice != null &&
                p.Invoice.TotalAmount > 0 &&
                p.Invoice.Payments.Sum(pay => pay.Amount) < p.Invoice.TotalAmount);

            // Discounts: sum of LineDiscountAmount across today's invoice line items
            var discounts = invoices.Sum(i => i.LineItems.Sum(l => l.LineDiscountAmount));

            // ManagerOverrides: audit logs for daily-operation-specific overrides
            var managerOverrides = await db.AuditLogs
                .CountAsync(a => a.Action == AuditAction.Approve
                    && a.Resource.StartsWith("Visit.")
                    && a.CreatedAt >= todayStart
                    && a.CreatedAt <= todayEnd);

            // Build report
            var report = new
            {
                Date = reportDate.ToString("yyyy-MM-dd"),
                PatientCounts = new
                {
                    Total = appointments.Count,
                    Waiting = queueItems.Count(q => q.Status == ClinicQueueStatus.Waiting),
                    InRoom = queueItems.Count(q => q.Status == ClinicQueueStatus.InRoom || q.Status == ClinicQueueStatus.InProgress),
                    ReadyForCheckout = readyForCheckout,
                    Completed = completed,
                    NoShow = appointments.Count(a => a.Status == AppointmentStatus.NoShow),
                    LeftWithoutCompletion = leftWithoutCompletion,
                    Emergency = appointments.Count(a => a.AppointmentType == "Emergency" || a.AppointmentType == "حالة إسعافية")
                },
                Financial = new
                {
                    TotalCollected = payments.Sum(p => p.Amount),
                    ByPaymentMethod = payments.GroupBy(p => p.PaymentMethod ?? "other")
                        .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() }),
                    NewDebts = newDebts,
                    PartialPayments = partialPayments,
                    DraftInvoices = invoices.Count(i => i.Status == InvoiceStatus.Draft),
                    Discounts = discounts
                },
                LabOrders = new
                {
                    Sent = labOrders.Count(l => l.Status == "sent"),
                    Received = labOrders.Count(l => l.Status == "received"),
                    Delivered = labOrders.Count(l => l.Status == "delivered")
                },
                ManagerOverrides = managerOverrides,
                TomorrowAppointments = await db.Appointments
                    .IgnoreQueryFilters()
                    .CountAsync(a => a.AppointmentDate == reportDate.AddDays(1) && a.IsActive)
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
