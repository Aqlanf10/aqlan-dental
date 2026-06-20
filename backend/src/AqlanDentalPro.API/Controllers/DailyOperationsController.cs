using AqlanDentalPro.Infrastructure.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Sprint 2 — Daily Operations Report endpoint.
/// Aggregates appointments, queue, visits, payments, invoices, lab orders, and audit data
/// for a given date to support the daily operations dashboard.
/// </summary>
[ApiController]
[Route("api/daily-operations")]
[Authorize(Policy = "StaffOnly")]
public class DailyOperationsController(AppDbContext db, ILogger<DailyOperationsController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the daily operations report for a given date (defaults to today).
    /// Includes patient counts, financial summary, lab order status, and audit overrides.
    /// </summary>
    [HttpGet("report")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetDailyReport([FromQuery] string? date)
    {
        try
        {
            var reportDate = string.IsNullOrEmpty(date)
                ? DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday())
                : DateOnly.Parse(date);

            var todayStart = DateTime.SpecifyKind(reportDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var todayEnd = DateTime.SpecifyKind(reportDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // CLIN-21: Partial data warning — set when a sub-query fails so the frontend can show a banner.
            string? partialDataWarning = null;

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
                .Where(p => p.PaymentDate == reportDate && p.IsActive)
                .ToListAsync();

            // ── Invoices (fully loaded with active payments and line items) ─
            // FIX: Wrap in try/catch — Invoices table may have stale schema in some environments
            // (missing LineItems navigation, missing AppointmentId column, etc.)
            var invoices = new List<Invoice>();
            try
            {
                invoices = await db.Invoices
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
            }
            catch (Exception invEx)
            {
                logger.LogWarning(invEx, "DailyOperations.GetDailyReport: Invoice query failed, using empty list");
                // CLIN-21 FIX: Flag partial data in the response so the frontend can show a warning.
                partialDataWarning = "تعذّر تحميل بيانات الفواتير — قد تكون الأرقام المالية غير مكتملة";
            }

            // ── Lab orders ───────────────────────────────────────────────
            var labOrders = await db.LabOrders
                .Where(l => l.SentDate == reportDate || l.ExpectedDate == reportDate || l.ReceivedDate == reportDate)
                .ToListAsync();

            // ── Calculations ──────────────────────────────────────────────

            // ReadyForCheckout: visits where CheckoutStatus is exactly "ReadyForCheckout"
            // (doctor has finished treatment; visit is awaiting billing/checkout at reception)
            var readyForCheckout = visits.Count(v => v.CheckoutStatus == "ReadyForCheckout");

            // Completed: appointments that reached Completed status
            var completed = appointments.Count(a => a.Status == AppointmentStatus.Completed);

            // LeftWithoutCompletion:
            //   Conservative formula: only count visits/appointments that carry an
            //   EXPLICIT terminal non-completed status indicating the patient left
            //   without finishing the visit (e.g. "LeftWithoutCompletion",
            //   "CancelledAfterArrival", "Incomplete", "Abandoned").
            //
            //   Currently the system does NOT define such statuses in AppointmentStatus
            //   or Visit.CheckoutStatus, so this count will be 0 until those statuses
            //   are introduced. This is intentional — a broad estimate inflates the count
            //   and misrepresents daily operations.
            //
            //   Excluded by design:
            //     - Scheduled/Confirmed (never arrived)
            //     - Cancelled (cancelled before arrival)
            //     - NoShow (never showed up)
            //     - Completed (finished normally)
            //     - Active flow (Arrived/Waiting/Called/InRoom/InProgress/ReadyForCheckout)
            //       — these are patients still in the clinic or mid-visit
            //     - Draft invoices without proof the patient actually left
            //
            //   When an explicit "LeftWithoutCompletion" (or equivalent) status is added
            //   to AppointmentStatus or Visit.CheckoutStatus, update the sets below.
            // Terminal CheckoutStatus values that mean "left without completing the visit"
            var leftWithoutCompletionCheckoutStatuses = new HashSet<string?>
            {
                "LeftWithoutCompletion",
                "CancelledAfterArrival",
                "Incomplete",
                "Abandoned",
            };

            // Terminal AppointmentStatus values that mean "left without completing"
            // (not Cancelled-before-arrival, not NoShow, not Completed)
            var leftWithoutCompletionAppointmentStatuses = new HashSet<AppointmentStatus>
            {
                // AppointmentStatus.LeftWithoutCompletion,  // uncomment when added
                // AppointmentStatus.CancelledAfterArrival,   // uncomment when added
                // AppointmentStatus.Incomplete,               // uncomment when added
                // AppointmentStatus.Abandoned,                // uncomment when added
            };

            var leftWithoutCompletion = appointments.Count(a => leftWithoutCompletionAppointmentStatuses.Contains(a.Status))
                + visits.Count(v => v.CheckoutStatus != null
                    && leftWithoutCompletionCheckoutStatuses.Contains(v.CheckoutStatus));

            var leftWithoutCompletionLogs = await db.AuditLogs
                .Where(a => a.Resource == "Visit.LeftWithoutCompletion"
                    && a.CreatedAt >= todayStart
                    && a.CreatedAt <= todayEnd)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            // NewDebts: outstanding balance on invoices with Issued status only.
            //   Paid invoices are excluded because they should have zero outstanding balance
            //   by definition; including them could mask data inconsistencies.
            //   Formula: Sum of (TotalAmount − Sum of active Payments) for Issued invoices,
            //   floored at 0 per-invoice to avoid negative debt from overpayment.
            var newDebts = invoices
                .Where(i => i.Status == InvoiceStatus.Issued)
                .Sum(i => Math.Max(0, i.TotalAmount - i.Payments.Sum(p => p.Amount)));

            // PartialPayments: count of invoices (created on reportDate) that have received
            //   at least one payment but are NOT fully paid.
            //   This uses the fully-loaded invoices collection (with all active payments),
            //   NOT the payments→invoice navigation which only loaded today's payments
            //   and could miss earlier payments on the same invoice.
            //   Formula: Count distinct invoices where Sum(ActivePayments) > 0
            //   AND Sum(ActivePayments) < Invoice.TotalAmount.
            var partialPayments = invoices.Count(i =>
                i.Payments.Count > 0 &&
                i.Payments.Sum(p => p.Amount) > 0 &&
                i.Payments.Sum(p => p.Amount) < i.TotalAmount);

            // Discounts: sum of LineDiscountAmount across today's active invoice line items
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
                LeftWithoutCompletionEvents = leftWithoutCompletionLogs.Select(a => new
                {
                    VisitId = a.ResourceId,
                    At = a.CreatedAt,
                    Reason = TryReadAuditString(a.NewData, "reason"),
                    Status = TryReadAuditString(a.NewData, "checkoutStatus")
                }),
                TomorrowAppointments = await db.Appointments
                    .IgnoreQueryFilters()
                    .CountAsync(a => a.AppointmentDate == reportDate.AddDays(1) && a.IsActive),
                // CLIN-21: Surface partial-data warning to the frontend.
                PartialDataWarning = partialDataWarning
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

    private static string? TryReadAuditString(JsonDocument? doc, string propertyName)
    {
        if (doc == null)
            return null;

        var root = doc.RootElement;
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
