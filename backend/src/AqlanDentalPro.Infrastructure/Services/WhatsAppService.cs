using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Net.Http.Json;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services;

public class WhatsAppService(
    AppDbContext db,
    IConfiguration config,
    IHttpClientFactory httpClientFactory,
    ILogger<WhatsAppService> logger) : IWhatsAppService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("WhatsApp");

    public async Task<WhatsAppMessageDto> SendMessageAsync(SendMessageRequest request)
    {
        var patient = await db.Patients.FindAsync(request.PatientId);
        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var phone = patient.WhatsApp ?? patient.Phone;
        if (string.IsNullOrEmpty(phone))
            throw new InvalidOperationException("لا يوجد رقم هاتف أو واتساب للمريض");

        // Get template or use custom message
        string content;
        if (!string.IsNullOrEmpty(request.CustomMessage))
        {
            content = request.CustomMessage;
        }
        else
        {
            var template = await db.WhatsAppTemplates
                .FirstOrDefaultAsync(t => t.TemplateKey == request.TemplateType && t.IsTemplateActive);
            if (template == null)
                throw new InvalidOperationException($"القالب '{request.TemplateType}' غير موجود أو غير مفعّل");

            content = ReplacePlaceholders(template.ContentTemplate, request.Parameters ?? new());
        }

        var message = new WhatsAppMessage
        {
            PatientId = request.PatientId,
            PhoneNumber = NormalizePhone(phone),
            TemplateType = request.TemplateType,
            MessageContent = content,
            Status = "pending",
            RelatedEntityId = request.Parameters?.GetValueOrDefault("entityId") != null
                ? Guid.Parse(request.Parameters["entityId"])
                : null,
            RelatedEntityType = request.Parameters?.GetValueOrDefault("entityType")
        };

        db.WhatsAppMessages.Add(message);
        await db.SaveChangesAsync();

        // Try to send immediately
        await TrySendMessageAsync(message);

        return MapToDto(message, patient);
    }

    public async Task<List<WhatsAppMessageDto>> SendBulkAsync(BulkSendMessageRequest request)
    {
        var results = new List<WhatsAppMessageDto>();
        foreach (var patientId in request.PatientIds)
        {
            try
            {
                var msg = await SendMessageAsync(new SendMessageRequest
                {
                    PatientId = patientId,
                    TemplateType = request.TemplateType,
                    Parameters = request.CommonParameters
                });
                results.Add(msg);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send WhatsApp message to patient {PatientId}", patientId);
            }
        }
        return results;
    }

    public async Task<WhatsAppMessageDto?> SendAppointmentReminderAsync(SendAppointmentReminderRequest request)
    {
        var appointment = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId);

        if (appointment == null || appointment.Patient == null) return null;

        var phone = appointment.Patient.WhatsApp ?? appointment.Patient.Phone;
        if (string.IsNullOrEmpty(phone)) return null;

        var parameters = new Dictionary<string, string>
        {
            ["patient_name"] = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}".Trim(),
            ["doctor_name"] = appointment.Doctor?.Name ?? "",
            ["date"] = appointment.AppointmentDate.ToString("yyyy/MM/dd"),
            ["time"] = appointment.StartTime.ToString("HH:mm"),
            ["appointment_type"] = appointment.AppointmentType,
            ["entityId"] = appointment.Id.ToString(),
            ["entityType"] = "Appointment"
        };

        var result = await SendMessageAsync(new SendMessageRequest
        {
            PatientId = appointment.PatientId,
            TemplateType = "appointment_reminder",
            Parameters = parameters
        });

        appointment.WhatsAppReminderSentAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // ── YOLO-S1: Companion/Guardian WhatsApp ─────────────────────────────
        // If the appointment has a CompanionPhone set (e.g. parent/guardian of a
        // child patient), ALSO send the reminder to that number IN ADDITION to
        // the patient's own phone — never instead of. Best-effort: a companion
        // send failure must not fail the patient send (already persisted above).
        if (!string.IsNullOrWhiteSpace(appointment.CompanionPhone))
        {
            try
            {
                await SendCompanionReminderAsync(appointment, parameters);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to send companion WhatsApp reminder for appointment {Id} to {Phone}",
                    appointment.Id, appointment.CompanionPhone);
            }
        }

        return result;
    }

    /// <summary>
    /// YOLO-S1: Sends the appointment reminder WhatsApp to the companion/guardian
    /// phone stored on the appointment (in addition to the patient's own phone).
    /// Reuses the same template + parameters as the patient reminder, but addresses
    /// the companion phone. Linked to the patient FK for traceability (so the
    /// companion message appears in the patient's WhatsApp history). Best-effort.
    /// </summary>
    private async Task SendCompanionReminderAsync(
        Appointment appointment, Dictionary<string, string> parameters)
    {
        var template = await db.WhatsAppTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == "appointment_reminder" && t.IsTemplateActive);

        var content = template != null
            ? ReplacePlaceholders(template.ContentTemplate, parameters)
            : $"تذكير بموعد: {parameters.GetValueOrDefault("patient_name")} في {parameters.GetValueOrDefault("date")} الساعة {parameters.GetValueOrDefault("time")} لدى {parameters.GetValueOrDefault("doctor_name")}";

        var companionMessage = new WhatsAppMessage
        {
            PatientId = appointment.PatientId, // FK for traceability (companion belongs to this patient)
            PhoneNumber = NormalizePhone(appointment.CompanionPhone!),
            TemplateType = "appointment_reminder",
            MessageContent = content,
            Status = "pending",
            RelatedEntityId = appointment.Id,
            RelatedEntityType = "Appointment",
        };

        db.WhatsAppMessages.Add(companionMessage);
        await db.SaveChangesAsync();

        // Try to send immediately (same path as the patient message — demo mode
        // if no API configured, real Meta API otherwise).
        await TrySendMessageAsync(companionMessage);

        logger.LogInformation(
            "Companion WhatsApp reminder sent for appointment {Id} to {Phone}",
            appointment.Id, appointment.CompanionPhone);
    }

    public async Task<int> SendPendingRemindersAsync()
    {
        // CORE-APPT-007: DateTime.Today is the host/container day (UTC on Railway),
        // not the clinic day. Yemen is UTC+3, so between 21:00 and 00:00 UTC —
        // 00:00 to 03:00 clinic time — "tomorrow" resolved to the wrong date and
        // the whole run reminded the wrong day's patients.
        var tomorrow = ClinicTimeProvider.ClinicToday().AddDays(1);
        var appointments = await db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Where(a => a.AppointmentDate == tomorrow
                     && (a.Status == Domain.Enums.AppointmentStatus.Scheduled
                      || a.Status == Domain.Enums.AppointmentStatus.Confirmed)
                     && a.WhatsAppReminderSentAt == null)
            .ToListAsync();

        var count = 0;
        foreach (var appt in appointments)
        {
            try
            {
                await SendAppointmentReminderAsync(new SendAppointmentReminderRequest
                {
                    AppointmentId = appt.Id,
                    HoursBefore = 24
                });
                count++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send reminder for appointment {Id}", appt.Id);
            }
        }
        return count;
    }

    public async Task<WhatsAppDashboardDto> GetDashboardAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;

            return new WhatsAppDashboardDto
            {
                SentToday = await db.WhatsAppMessages.CountAsync(m => m.SentAt != null && m.SentAt.Value.Date == today),
                DeliveredToday = await db.WhatsAppMessages.CountAsync(m => m.DeliveredAt != null && m.DeliveredAt.Value.Date == today),
                FailedToday = await db.WhatsAppMessages.CountAsync(m => m.Status == "failed" && m.CreatedAt.Date == today),
                PendingCount = await db.WhatsAppMessages.CountAsync(m => m.Status == "pending"),
                RecentMessages = (await db.WhatsAppMessages
                    .Include(m => m.Patient)
                    .OrderByDescending(m => m.CreatedAt)
                    .Take(20)
                    .ToListAsync())
                    .Where(m => m.Patient != null)
                    .Select(m => MapToDto(m, m.Patient!))
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WhatsApp dashboard is using an empty read fallback");
            return new WhatsAppDashboardDto();
        }
    }

    public async Task<List<WhatsAppMessageDto>> GetMessageHistoryAsync(Guid? patientId = null, int page = 1, int pageSize = 20)
    {
        var query = db.WhatsAppMessages
            .Include(m => m.Patient)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(m => m.PatientId == patientId.Value);

        return (await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync())
            .Select(m => MapToDto(m, m.Patient!))
            .ToList();
    }

    public async Task<bool> RetryFailedMessageAsync(Guid messageId)
    {
        var message = await db.WhatsAppMessages.FindAsync(messageId);
        if (message == null || message.Status != "failed") return false;

        if (message.RetryCount >= 3) return false; // Max 3 retries

        message.Status = "pending";
        message.ErrorMessage = null;
        await db.SaveChangesAsync();

        await TrySendMessageAsync(message);
        return true;
    }

    public async Task<List<WhatsAppTemplateDto>> GetTemplatesAsync()
    {
        return await db.WhatsAppTemplates
            .OrderBy(t => t.Category).ThenBy(t => t.TemplateKey)
            .Select(t => new WhatsAppTemplateDto
            {
                Id = t.Id,
                TemplateKey = t.TemplateKey,
                NameAr = t.NameAr,
                ContentTemplate = t.ContentTemplate,
                IsActive = t.IsTemplateActive,
                Category = t.Category
            })
            .ToListAsync();
    }

    public async Task<WhatsAppTemplateDto?> UpdateTemplateAsync(Guid templateId, UpdateWhatsAppTemplateRequest request)
    {
        var template = await db.WhatsAppTemplates.FindAsync(templateId);
        if (template == null) return null;

        template.ContentTemplate = request.ContentTemplate;
        template.IsTemplateActive = request.IsActive;
        await db.SaveChangesAsync();

        return new WhatsAppTemplateDto
        {
            Id = template.Id,
            TemplateKey = template.TemplateKey,
            NameAr = template.NameAr,
            ContentTemplate = template.ContentTemplate,
            IsActive = template.IsTemplateActive,
            Category = template.Category
        };
    }

    public async Task<WhatsAppMessageDto?> GetMessageStatusAsync(Guid messageId)
    {
        var message = await db.WhatsAppMessages
            .Include(m => m.Patient)
            .FirstOrDefaultAsync(m => m.Id == messageId);

        return message == null ? null : MapToDto(message, message.Patient!);
    }

    // ── Private Helpers ──────────────────────────────────────────────────────

    private async Task TrySendMessageAsync(WhatsAppMessage message)
    {
        try
        {
            var apiUrl   = config["WhatsApp:ApiUrl"];
            var apiToken = config["WhatsApp:ApiToken"];

            if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiToken))
            {
                // Demo mode: simulate successful send
                message.Status     = "sent";
                message.SentAt     = DateTime.UtcNow;
                message.ExternalId = $"demo_{Guid.NewGuid():N}";
                logger.LogInformation("WhatsApp (demo mode): message queued for {Phone}", message.PhoneNumber);
                await db.SaveChangesAsync();
                return;
            }

            // Real Meta WhatsApp Cloud API — one automatic retry on transient errors
            HttpResponseMessage? response = null;
            string?              responseBody = null;
            const int maxAttempts = 2;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var payload = new
                {
                    messaging_product = "whatsapp",
                    to   = message.PhoneNumber,
                    type = "text",
                    text = new { body = message.MessageContent }
                };

                var req = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/messages")
                {
                    Headers  = { { "Authorization", $"Bearer {apiToken}" } },
                    Content  = JsonContent.Create(payload)
                };

                try
                {
                    response     = await _httpClient.SendAsync(req);
                    responseBody = await response.Content.ReadAsStringAsync();

                    // Only retry on 5xx server errors or request timeout
                    if (response.IsSuccessStatusCode
                        || (int)response.StatusCode < 500) break;
                }
                catch (HttpRequestException ex) when (attempt < maxAttempts)
                {
                    logger.LogWarning(ex,
                        "WhatsApp API attempt {Attempt} failed for {Phone}, retrying…",
                        attempt, message.PhoneNumber);
                }

                if (attempt < maxAttempts)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }

            if (response is null)
            {
                message.Status       = "failed";
                message.ErrorMessage = "لم يتم الاتصال بـ WhatsApp API بعد عدة محاولات";
                message.RetryCount++;
            }
            else if (response.IsSuccessStatusCode)
            {
                message.Status = "sent";
                message.SentAt = DateTime.UtcNow;
                using var doc  = JsonDocument.Parse(responseBody!);
                message.ExternalId = doc.RootElement
                    .GetProperty("messages")[0]
                    .GetProperty("id").GetString();
            }
            else
            {
                message.Status       = "failed";
                message.ErrorMessage = responseBody!.Length > 500 ? responseBody[..500] : responseBody;
                message.RetryCount++;
            }
        }
        catch (Exception ex)
        {
            message.Status       = "failed";
            message.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            message.RetryCount++;
            logger.LogError(ex, "Failed to send WhatsApp message to {Phone}", message.PhoneNumber);
        }

        await db.SaveChangesAsync();
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, string> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }
        return template;
    }

    private static string NormalizePhone(string phone)
    {
        var cleaned = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (cleaned.StartsWith("7") && cleaned.Length == 9)
            cleaned = "+967" + cleaned;
        else if (cleaned.StartsWith("0") && cleaned.Length == 10)
            cleaned = "+967" + cleaned[1..];
        // CORE-PAT-046: staff sometimes type the number with the country code
        // already but no "+" (e.g. "967770245745", 12 digits) — this fell
        // through both branches above unchanged, so the Meta Cloud API call
        // dialed a number missing the "+" the test suite's own contract
        // (WhatsAppReminderCompanionTests) asserts every stored PhoneNumber has.
        else if (cleaned.StartsWith("967") && cleaned.Length == 12)
            cleaned = "+" + cleaned;
        return cleaned;
    }

    private static WhatsAppMessageDto MapToDto(WhatsAppMessage m, Patient p) => new()
    {
        Id = m.Id,
        PatientId = m.PatientId,
        PatientName = $"{p.FirstName} {p.LastName}".Trim(),
        PhoneNumber = m.PhoneNumber,
        TemplateType = m.TemplateType,
        MessageContent = m.MessageContent,
        Status = m.Status,
        ErrorMessage = m.ErrorMessage,
        RetryCount = m.RetryCount,
        SentAt = m.SentAt,
        CreatedAt = m.CreatedAt,
        RelatedEntityId = m.RelatedEntityId,
        RelatedEntityType = m.RelatedEntityType
    };

    private static bool IsReadSchemaCompatibilityFailure(Exception ex)
    {
        var pg = ex.InnerException as PostgresException;
        return pg?.SqlState is "42P01" or "42703" or "42804" or "42883" or "22P02";
    }
}
