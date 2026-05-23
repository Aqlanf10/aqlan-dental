using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Background job that automatically sends appointment reminder emails.
/// Reads the <c>appointment.reminder_hours</c> setting from DB (default: "24,2")
/// and sends reminders at each configured interval before the appointment.
/// Runs every 30 minutes to catch appointments that fall within the reminder window.
/// </summary>
public class AppointmentReminderJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderJob> _logger;

    public AppointmentReminderJob(IServiceScopeFactory scopeFactory, ILogger<AppointmentReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 3 minutes after startup before first run (let app stabilize)
        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendRemindersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AppointmentReminderJob failed");
            }

            // Run every 30 minutes
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task SendRemindersAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // ── 1. Read reminder_hours setting from DB ──
        var reminderHoursSetting = await db.Settings
            .Where(s => s.Key == "appointment.reminder_hours")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(reminderHoursSetting))
            reminderHoursSetting = "24,2"; // Default

        var reminderHours = reminderHoursSetting
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s =>
            {
                int.TryParse(s, out var h);
                return h;
            })
            .Where(h => h > 0)
            .OrderBy(h => h)
            .ToList();

        if (reminderHours.Count == 0)
        {
            _logger.LogDebug("No valid reminder_hours configured. Skipping.");
            return;
        }

        // ── 2. Check if email is configured ──
        if (!await emailService.IsConfiguredAsync())
        {
            _logger.LogDebug("Email not configured. Skipping appointment reminders.");
            return;
        }

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var totalSent = 0;

        foreach (var hoursBefore in reminderHours)
        {
            // Calculate the time window: appointments that start within [now, now + hoursBefore]
            // and haven't had a reminder sent yet.
            var windowStart = now;
            var windowEnd = now.AddHours(hoursBefore);

            var upcomingAppointments = await db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .Where(a => a.Status == AppointmentStatus.Scheduled
                         && !a.ConfirmationSent
                         && a.Patient.Email != null
                         && a.AppointmentDate >= DateOnly.FromDateTime(windowStart.Date)
                         && a.AppointmentDate <= DateOnly.FromDateTime(windowEnd.Date))
                .ToListAsync();

            // Filter in-memory for precise time comparison (DateOnly + TimeOnly → DateTime)
            var matchingAppointments = upcomingAppointments
                .Where(a =>
                {
                    var apptDateTime = a.AppointmentDate.ToDateTime(a.StartTime);
                    // Send reminder if appointment is within the window for this hoursBefore
                    // e.g., for hoursBefore=24: send if appointment is 23-24 hours away
                    var hoursUntilAppt = (apptDateTime - now).TotalHours;
                    return hoursUntilAppt > 0 && hoursUntilAppt <= hoursBefore && hoursUntilAppt > hoursBefore - 1;
                })
                .ToList();

            foreach (var appt in matchingAppointments)
            {
                try
                {
                    var patientName = $"{appt.Patient.FirstName} {appt.Patient.LastName}".Trim();
                    var doctorName = appt.Doctor.Name ?? "الطبيب";
                    var appointmentDate = appt.AppointmentDate.ToString("yyyy/MM/dd");
                    var appointmentTime = appt.StartTime.ToString("HH:mm");
                    var clinicService = appt.Service?.Name;

                    var subject = $"تذكير بموعدكم في مركز د. عقلان الكامل — {appointmentDate}";
                    var htmlBody = EmailService.BuildAppointmentReminderHtml(
                        patientName, doctorName, appointmentDate,
                        appointmentTime, clinicService, appt.Notes);

                    var sent = await emailService.SendEmailAsync(appt.Patient.Email!, subject, htmlBody);

                    if (sent)
                    {
                        appt.ConfirmationSent = true;
                        totalSent++;
                        _logger.LogInformation(
                            "Sent {Hours}h reminder for appointment {Id} to {Patient}",
                            hoursBefore, appt.Id, patientName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send reminder for appointment {Id}", appt.Id);
                }
            }
        }

        // Save all ConfirmationSent updates
        if (totalSent > 0)
        {
            await db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "AppointmentReminderJob: sent {Count} reminders (checked {Hours} windows)",
            totalSent, string.Join(",", reminderHours));
    }
}
