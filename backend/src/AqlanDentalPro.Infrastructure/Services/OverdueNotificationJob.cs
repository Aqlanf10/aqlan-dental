using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>Daily background job: checks overdue contracts and notifies accountants.</summary>
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

        var activeContracts = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Where(c => c.Status == "active" && c.StartDate.HasValue && c.InstallmentsCount > 0)
            .ToListAsync();

        int count = 0;
        foreach (var c in activeContracts)
        {
            var monthsElapsed = ((today.Year - c.StartDate!.Value.Year) * 12)
                                + (today.Month - c.StartDate.Value.Month);
            var expectedPaid = c.DownPayment
                + (Math.Min(monthsElapsed, c.InstallmentsCount) * (c.InstallmentAmount ?? 0));
            var actualPaid = c.Payments.Sum(p => p.Amount);

            if (expectedPaid - actualPaid > 0)
            {
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

        logger.LogInformation("OverdueNotificationJob: checked {Count} overdue contracts", count);
    }
}
