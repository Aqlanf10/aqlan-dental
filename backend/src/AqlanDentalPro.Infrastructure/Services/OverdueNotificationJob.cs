using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>Daily background job: checks overdue contracts AND overdue lab orders, notifies relevant roles.</summary>
public class OverdueNotificationJob(IServiceScopeFactory scopeFactory, ILogger<OverdueNotificationJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once shortly after startup, then every 24 hours
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOverdueAsync();
                await CheckOverdueLabOrdersAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OverdueNotificationJob failed");
            }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CheckOverdueAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db            = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStart = DateTime.UtcNow.Date;   // بداية اليوم بالتوقيت العالمي
        var todayEnd = todayStart.AddDays(1);     // نهاية اليوم

        var activeContracts = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.Status == ContractStatus.Active && c.StartDate.HasValue && c.InstallmentsCount > 0)
            .ToListAsync();

        // ─── منع التكرار: جلب الإشعارات التي أُرسلت اليوم لنفس العقود ──────────
        var todayOverdueContractIds = await db.Notifications
            .Where(n => n.Type == "payment"
                     && n.Title == "قسط متأخر"
                     && n.RelatedEntity == "Contract"
                     && n.CreatedAt >= todayStart && n.CreatedAt < todayEnd)
            .Select(n => n.RelatedId)
            .Distinct()
            .ToListAsync();
        var todayOverdueContractIdSet = todayOverdueContractIds.ToHashSet();

        int count = 0;
        foreach (var c in activeContracts)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12)
                                + (today.Month - c.StartDate.Value.Month);
            var expectedPaid = c.DownPayment
                + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid = c.Payments.Where(p => p.IsActive).Sum(p => p.Amount);

            if (expectedPaid - actualPaid > 0)
            {
                // ─── تخطي العقود التي سبق إرسال إشعار عنها اليوم ──────────────
                if (todayOverdueContractIdSet.Contains(c.Id))
                    continue;

                count++;
                var overdueAmt = expectedPaid - actualPaid;
                var patientName = c.Patient != null
                    ? $"{c.Patient.FirstName} {c.Patient.LastName}".Trim()
                    : "مريض";
                var msg = $"عقد {patientName}: متأخر {overdueAmt:N0} ر.ي";
                await notifications.NotifyRoleAsync("Accountant", "payment", "قسط متأخر", msg, "Contract", c.Id);
            }
        }

        if (count > 0)
        {
            await notifications.NotifyRoleAsync(
                "Admin", "payment", $"{count} قسط متأخر",
                $"يوجد {count} عقد نشط بأقساط متأخرة — راجع صفحة المتأخرات",
                null, null);
        }

        logger.LogInformation("OverdueNotificationJob: checked {Count} overdue contracts (deduped from {Total} active)", count, activeContracts.Count);
    }

    /// <summary>
    /// CLIN-20: Checks lab orders where ExpectedDate < today and status is not delivered/cancelled.
    /// Notifies Reception + Admin so overdue lab orders don't sit forgotten.
    /// Deduplicates by checking if a notification was already sent today for the same order.
    /// </summary>
    private async Task CheckOverdueLabOrdersAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        // Active lab orders that are overdue (ExpectedDate < today, not delivered/cancelled)
        var overdueOrders = await db.LabOrders
            .Include(l => l.Patient)
            .Include(l => l.Lab)
            .Where(l => l.IsActive
                     && l.ExpectedDate.HasValue
                     && l.ExpectedDate.Value < today
                     && l.Status != "delivered"
                     && l.Status != "cancelled")
            .ToListAsync();

        if (overdueOrders.Count == 0)
        {
            logger.LogDebug("CheckOverdueLabOrdersAsync: no overdue lab orders found");
            return;
        }

        // Deduplicate: skip orders already notified today
        var todayNotifiedIds = await db.Notifications
            .Where(n => n.Type == "lab"
                     && n.Title == "طلب معمل متأخر"
                     && n.RelatedEntity == "LabOrder"
                     && n.CreatedAt >= todayStart && n.CreatedAt < todayEnd)
            .Select(n => n.RelatedId)
            .Distinct()
            .ToListAsync();
        var todayNotifiedSet = todayNotifiedIds.ToHashSet();

        int count = 0;
        foreach (var order in overdueOrders)
        {
            if (todayNotifiedSet.Contains(order.Id))
                continue;

            count++;
            var patientName = order.Patient != null
                ? $"{order.Patient.FirstName} {order.Patient.LastName}".Trim()
                : "مريض";
            var labName = order.Lab?.Name ?? "معمل غير محدد";
            var daysOverdue = today.DayNumber - order.ExpectedDate!.Value.DayNumber;
            var msg = $"طلب معمل {order.OrderNumber} ({patientName} — {labName}): متأخر {daysOverdue} يوم";

            await notifications.NotifyRoleAsync("Reception", "lab", "طلب معمل متأخر", msg, "LabOrder", order.Id);
        }

        if (count > 0)
        {
            await notifications.NotifyRoleAsync(
                "Admin", "lab", $"{count} طلب معمل متأخر",
                $"يوجد {count} طلب معمل متأخر عن موعد التسليم المتوقع — راجع صفحة المتأخرات",
                null, null);
        }

        logger.LogInformation("CheckOverdueLabOrdersAsync: found {Count} overdue lab orders (deduped from {Total} total)", count, overdueOrders.Count);
    }
}
