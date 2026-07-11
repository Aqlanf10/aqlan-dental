using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Background job that automatically sends appointment reminders.
/// Reads the <c>appointment.reminder_hours</c> setting from DB (default: "24,2")
/// and sends reminders at each configured interval before the appointment.
///
/// Key behaviors (post reliability-sprint):
/// - Targets Scheduled AND Confirmed appointments only.
/// - Uses Asia/Aden (UTC+3) timezone for all timing calculations.
/// - Tracks email, SMS, and WhatsApp reminders separately (no cross-channel suppression).
/// - Supports multiple reminder windows (e.g. 24h and 2h) — each window sends at most once.
/// - Resolves patient email in priority order: PatientAccount→User, Patient.Email, BookingRequest.Email.
/// - SMS channel via Local Android SIM SMS Gateway (if enabled).
///
/// Runs every 30 minutes to catch appointments that fall within the reminder window.
/// </summary>
public class AppointmentReminderJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentReminderJob> _logger;
    private static readonly TimeZoneInfo ClinicTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Aden");

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
        }

        // ── 3. Check if SMS is enabled and configured ──
        ISmsService? smsService = null;
        bool smsEnabled = false;
        try
        {
            var smsSetting = await db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.enabled");
            var smsApptReminders = await db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.send_appointment_reminders");
            smsEnabled = smsSetting?.Value?.ToLower() == "true"
                && smsApptReminders?.Value?.ToLower() != "false";

            if (smsEnabled)
            {
                smsService = scope.ServiceProvider.GetService<ISmsService>();
                if (smsService == null)
                {
                    _logger.LogDebug("SMS service not registered in DI. Skipping SMS reminders.");
                    smsEnabled = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SMS service check failed. Skipping SMS reminders.");
        }

        // ── 4. Use clinic timezone (Asia/Aden, UTC+3) for all timing ──
        var nowClinic = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTz);
        var totalSent = 0;

        // ── 5. Target Scheduled AND Confirmed appointments ──
        var targetStatuses = new List<AppointmentStatus>
        {
            AppointmentStatus.Scheduled,
            AppointmentStatus.Confirmed
        };

        foreach (var hoursBefore in reminderHours)
        {
            // Calculate the time window in clinic timezone
            var windowEndClinic = nowClinic.AddHours(hoursBefore);

            // Look ahead a bit beyond the window to avoid edge-case misses
            var dbDateStart = DateOnly.FromDateTime(nowClinic.Date);
            var dbDateEnd = DateOnly.FromDateTime(windowEndClinic.Date);

            var upcomingAppointments = await db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.Service)
                .Where(a => targetStatuses.Contains(a.Status)
                         && a.Patient.IsActive
                         && a.AppointmentDate >= dbDateStart
                         && a.AppointmentDate <= dbDateEnd)
                .ToListAsync();

            // Filter in-memory for precise time comparison using clinic timezone
            var matchingAppointments = upcomingAppointments
                .Where(a =>
                {
                    // AppointmentDate + StartTime is interpreted as clinic local time
                    var apptDateTimeClinic = a.AppointmentDate.ToDateTime(a.StartTime);
                    var hoursUntilAppt = (apptDateTimeClinic - nowClinic).TotalHours;
                    // Window: appointment is within (hoursBefore-1, hoursBefore] hours from now
                    return hoursUntilAppt > 0 && hoursUntilAppt <= hoursBefore && hoursUntilAppt > hoursBefore - 1;
                })
                .ToList();

            foreach (var appt in matchingAppointments)
            {
                try
                {
                    // ── Check if this specific window already sent for email ──
                    var sentWindows = ParseSentWindows(appt.EmailReminderWindowsSent);
                    if (sentWindows.Contains(hoursBefore))
                    {
                        _logger.LogDebug(
                            "Email reminder for {Hours}h window already sent for appointment {Id}. Skipping email.",
                            hoursBefore, appt.Id);
                    }
                    else
                    {
                        // ── Channel 1: Email via linked User account ──
                        var patientEmail = await GetPatientEmailAsync(db, appt.PatientId, appt.Id);

                        if (emailConfigured && !string.IsNullOrWhiteSpace(patientEmail))
                        {
                            var patientName = $"{appt.Patient.FirstName} {appt.Patient.LastName}".Trim();
                            var doctorName = appt.Doctor.Name ?? "الطبيب";
                            var appointmentDate = appt.AppointmentDate.ToString("yyyy/MM/dd");
                            var appointmentTime = appt.StartTime.ToString("HH:mm");
                            var clinicService = appt.Service?.ArabicName;

                            var identity = await FinanceClinicIdentity.ResolveAsync(db);
                            var subject = $"تذكير بموعد {identity.Name} — {appointmentDate}";
                            var htmlBody = EmailService.BuildAppointmentReminderHtml(
                                patientName, doctorName, appointmentDate,
                                appointmentTime, clinicService, appt.Notes,
                                identity.Name, identity.Location);

                            var sent = await emailService.SendAppointmentReminderAsync(patientEmail, subject, htmlBody, appt.Id);

                            if (sent)
                            {
                                sentWindows.Add(hoursBefore);
                                appt.EmailReminderWindowsSent = JsonSerializer.Serialize(sentWindows);
                                appt.EmailReminderSentAt = DateTime.UtcNow;
                                totalSent++;
                                _logger.LogInformation(
                                    "Sent {Hours}h email reminder for appointment {Id} to {Email}",
                                    hoursBefore, appt.Id, MaskEmail(patientEmail));
                            }
                        }
                        else
                        {
                            _logger.LogDebug(
                                "No email on file for patient {PatientId}. Skipping email reminder for appointment {Id}.",
                                appt.PatientId, appt.Id);
                        }
                    }

                    // ── Channel 2: SMS via Local Android SIM Gateway ──
                    if (smsEnabled && smsService != null)
                    {
                        try
                        {
                            var smsSentWindows = ParseSentWindows(appt.SmsReminderWindowsSent);
                            if (!smsSentWindows.Contains(hoursBefore))
                            {
                                await smsService.SendAppointmentReminderAsync(
                                    new AqlanDentalPro.Application.DTOs.Sms.SendSmsAppointmentReminderRequest
                                    {
                                        AppointmentId = appt.Id,
                                        HoursBefore = hoursBefore
                                    });
                                smsSentWindows.Add(hoursBefore);
                                appt.SmsReminderWindowsSent = JsonSerializer.Serialize(smsSentWindows);
                                totalSent++;
                                _logger.LogInformation(
                                    "Sent {Hours}h SMS reminder for appointment {Id}",
                                    hoursBefore, appt.Id);
                            }
                        }
                        catch (Exception smsEx)
                        {
                            _logger.LogWarning(smsEx,
                                "Failed to send SMS reminder for appointment {Id}", appt.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send reminder for appointment {Id}", appt.Id);
                }
            }
        }

        // Save all tracking updates
        if (totalSent > 0)
        {
            await db.SaveChangesAsync();
        }

        _logger.LogInformation(
            "AppointmentReminderJob: sent {Count} reminders across all channels (checked {Hours} windows, tz=Asia/Aden)",
            totalSent, string.Join(",", reminderHours));
    }

    /// <summary>
    /// Gets the best available email address for a patient.
    /// Priority order:
    /// A) Linked User account email via PatientAccount
    /// B) Patient.Email field
    /// C) BookingRequest email (if appointment was created from a booking request)
    /// </summary>
    public static async Task<string?> GetPatientEmailAsync(AppDbContext db, Guid patientId, Guid? appointmentId = null)
    {
        // A) PatientAccount → linked User email
        var linkedUserEmail = await db.PatientAccounts
            .Where(pa => pa.PatientId == patientId && pa.LinkedUserId != null)
            .Join(db.Users, pa => pa.LinkedUserId, u => u.Id, (pa, u) => u.Email)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(linkedUserEmail))
            return linkedUserEmail;

        // B) Patient.Email field
        var patientEmail = await db.Patients
            .Where(p => p.Id == patientId)
            .Select(p => p.Email)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrWhiteSpace(patientEmail))
            return patientEmail;

        // C) BookingRequest email — if this appointment was created from a booking request
        if (appointmentId.HasValue)
        {
            var bookingEmail = await db.BookingRequests
                .Where(br => br.ConvertedToAppointmentId == appointmentId.Value)
                .Select(br => br.Email)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(bookingEmail))
                return bookingEmail;
        }

        return null;
    }

    /// <summary>Parses the JSON array of already-sent reminder window hours.</summary>
    public static List<int> ParseSentWindows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<int>();

        try
        {
            return JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
        }
        catch
        {
            return new List<int>();
        }
    }

    /// <summary>Masks email address for safe logging (e.g., "a****n@gmail.com").</summary>
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            return "***";

        var parts = email.Split('@');
        var local = parts[0];
        var domain = parts[1];

        if (local.Length <= 2)
            return $"**@{domain}";

        return $"{local[0]}{new string('*', local.Length - 2)}{local[^1]}@{domain}";
    }
}
