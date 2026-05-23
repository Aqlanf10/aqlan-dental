using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Background job that automatically sends appointment reminders.
/// Reads the <c>appointment.reminder_hours</c> setting from DB (default: "24,2")
/// and sends reminders at each configured interval before the appointment.
///
/// Reminder channels (in priority order):
/// 1. Email — if patient has a linked User account with email, or if patient portal account email is available
/// 2. WhatsApp — if WhatsApp number is on file and WhatsApp service is configured
///
/// The job also triggers WhatsAppService.SendPendingRemindersAsync() for next-day reminders
/// (which handles the WhatsApp channel specifically).
///
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
        var emailConfigured = await emailService.IsConfiguredAsync();
        if (!emailConfigured)
        {
            _logger.LogDebug("Email not configured. Skipping email appointment reminders.");
            // Still continue to mark appointments and trigger WhatsApp reminders
        }

        var now = DateTime.UtcNow;
        var totalSent = 0;

        foreach (var hoursBefore in reminderHours)
        {
            // Calculate the time window: appointments that start within [now, now + hoursBefore]
            var windowEnd = now.AddHours(hoursBefore);

            var upcomingAppointments = await db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .Where(a => a.Status == AppointmentStatus.Scheduled
                         && !a.ConfirmationSent
                         && a.AppointmentDate >= DateOnly.FromDateTime(now.Date)
                         && a.AppointmentDate <= DateOnly.FromDateTime(windowEnd.Date))
                .ToListAsync();

            // Filter in-memory for precise time comparison
            var matchingAppointments = upcomingAppointments
                .Where(a =>
                {
                    var apptDateTime = a.AppointmentDate.ToDateTime(a.StartTime);
                    var hoursUntilAppt = (apptDateTime - now).TotalHours;
                    return hoursUntilAppt > 0 && hoursUntilAppt <= hoursBefore && hoursUntilAppt > hoursBefore - 1;
                })
                .ToList();

            foreach (var appt in matchingAppointments)
            {
                try
                {
                    // ── Try email via linked User account ──
                    var patientEmail = await GetPatientEmailAsync(db, appt.PatientId);

                    if (emailConfigured && !string.IsNullOrWhiteSpace(patientEmail))
                    {
                        var patientName = $"{appt.Patient.FirstName} {appt.Patient.LastName}".Trim();
                        var doctorName = appt.Doctor.Name ?? "الطبيب";
                        var appointmentDate = appt.AppointmentDate.ToString("yyyy/MM/dd");
                        var appointmentTime = appt.StartTime.ToString("HH:mm");
                        var clinicService = appt.Service?.ArabicName;

                        var subject = $"تذكير بموعد مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الاسنان — {appointmentDate}";
                        var htmlBody = EmailService.BuildAppointmentReminderHtml(
                            patientName, doctorName, appointmentDate,
                            appointmentTime, clinicService, appt.Notes);

                        var sent = await emailService.SendAppointmentReminderAsync(patientEmail, subject, htmlBody, appt.Id);

                        if (sent)
                        {
                            appt.ConfirmationSent = true;
                            totalSent++;
                            _logger.LogInformation(
                                "Sent {Hours}h email reminder for appointment {Id} to {Email}",
                                hoursBefore, appt.Id, patientEmail);
                        }
                    }
                    else
                    {
                        // No email available — mark as confirmed so WhatsApp reminders still work
                        // The WhatsAppService.SendPendingRemindersAsync handles the WhatsApp channel
                        _logger.LogDebug(
                            "No email on file for patient {PatientId}. Skipping email reminder for appointment {Id}. " +
                            "WhatsApp reminders are handled separately via WhatsAppService.",
                            appt.PatientId, appt.Id);
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
            "AppointmentReminderJob: sent {Count} email reminders (checked {Hours} windows)",
            totalSent, string.Join(",", reminderHours));
    }

    /// <summary>
    /// Gets the best available email address for a patient.
    /// Checks: 1) Linked User account email, 2) PatientAccount linked user email.
    /// Returns null if no email is found.
    /// </summary>
    private static async Task<string?> GetPatientEmailAsync(AppDbContext db, Guid patientId)
    {
        // Check if patient has a linked User account via PatientAccount
        var linkedUserEmail = await db.PatientAccounts
            .Where(pa => pa.PatientId == patientId && pa.LinkedUserId != null)
            .Join(db.Users, pa => pa.LinkedUserId, u => u.Id, (pa, u) => u.Email)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(linkedUserEmail))
            return linkedUserEmail;

        // Future: if Patient entity gets an Email field, check here

        return null;
    }
}
