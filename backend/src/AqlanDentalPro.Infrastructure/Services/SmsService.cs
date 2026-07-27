using AqlanDentalPro.Application.DTOs.Sms;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// SMS Service — Local Android SIM SMS Gateway integration.
/// Sends SMS messages through an Android phone running an SMS Gateway API app.
/// Settings are stored in the DB Settings table and cached in-memory for 5 minutes.
/// Environment variables (Sms:ApiUrl, Sms:ApiKey, Sms:SenderName) serve as fallback.
/// </summary>
public class SmsService : ISmsService
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsService> _logger;

    // Cache for DB-stored settings (refreshed every 5 minutes)
    private string? _cachedApiUrl;
    private string? _cachedApiKey;
    private string? _cachedSenderName;
    private string? _cachedGatewayMode;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    // DB values take priority over env vars; env vars serve as fallback
    private string? ApiUrl => _cachedApiUrl ?? _configuration["Sms:ApiUrl"];
    private string? ApiKey => _cachedApiKey ?? _configuration["Sms:ApiKey"];
    private string? SenderName => _cachedSenderName ?? _configuration["Sms:SenderName"];
    /// <summary>Gateway mode: "local_android" (base URL + /sms/send + /status) or "cloud_api" (full send endpoint URL).</summary>
    private string GatewayMode => _cachedGatewayMode ?? _configuration["Sms:GatewayMode"] ?? "local_android";
    private bool IsCloudApi => GatewayMode == "cloud_api";
    private bool IsConfigured => !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(ApiKey);

    public SmsService(
        AppDbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SmsService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Ensures DB-cached settings are loaded (env vars serve as fallback).
    /// </summary>
    private async Task EnsureSettingsCacheAsync()
    {
        if (DateTime.UtcNow < _cacheExpiry) return;

        var urlSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.api_url");
        var keySetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.api_key");
        var senderSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.sender_name");
        var modeSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.gateway_mode");

        _cachedApiUrl = urlSetting?.Value;
        _cachedApiKey = keySetting?.Value;
        _cachedSenderName = senderSetting?.Value;
        _cachedGatewayMode = modeSetting?.Value;
        _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
    }

    // ─── Send Single SMS ──────────────────────────────────────────────────
    public async Task<SmsMessageDto> SendSmsAsync(SendSmsRequest request)
    {
        await EnsureSettingsCacheAsync();

        // Get patient phone
        var patient = await _db.Patients.FindAsync(request.PatientId);
        if (patient == null) throw new InvalidOperationException("المريض غير موجود");

        var phone = NormalizePhone(patient.Phone ?? patient.WhatsApp);
        if (string.IsNullOrWhiteSpace(phone)) throw new InvalidOperationException("رقم هاتف المريض غير متوفر");

        // Build message content
        string content;
        if (!string.IsNullOrWhiteSpace(request.CustomMessage))
        {
            content = request.CustomMessage;
        }
        else
        {
            var template = await GetActiveTemplate(request.TemplateType);
            content = template != null
                ? ReplacePlaceholders(template.ContentTemplate, request.Parameters ?? new())
                : "تذكير من عيادة أقلان لطب الأسنان";
        }

        // Calculate segments
        var (charCount, segmentCount) = CalculateSegments(content);

        // Create message record
        var smsMessage = new SmsMessage
        {
            PatientId = request.PatientId,
            PhoneNumber = phone,
            TemplateType = request.TemplateType,
            MessageContent = content,
            Status = "pending",
            Gateway = "local_android",
            CharacterCount = charCount,
            SegmentCount = segmentCount,
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = request.RelatedEntityType
        };

        _db.SmsMessages.Add(smsMessage);
        await _db.SaveChangesAsync();

        // Send via gateway
        await SendViaGatewayAsync(smsMessage);

        return MapToDto(smsMessage, patient);
    }

    // ─── Send Bulk SMS ────────────────────────────────────────────────────
    public async Task<List<SmsMessageDto>> SendBulkAsync(BulkSendSmsRequest request)
    {
        await EnsureSettingsCacheAsync();

        var results = new List<SmsMessageDto>();
        var patients = await _db.Patients
            .Where(p => request.PatientIds.Contains(p.Id))
            .ToListAsync();

        foreach (var patient in patients)
        {
            try
            {
                var singleRequest = new SendSmsRequest
                {
                    PatientId = patient.Id,
                    TemplateType = request.TemplateType,
                    Parameters = request.CommonParameters
                };
                var result = await SendSmsAsync(singleRequest);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SMS to patient {PatientId}", patient.Id);
            }
        }

        return results;
    }

    // ─── Send Appointment Reminder ────────────────────────────────────────
    public async Task<SmsMessageDto> SendAppointmentReminderAsync(SendSmsAppointmentReminderRequest request)
    {
        await EnsureSettingsCacheAsync();

        var appointment = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId);

        if (appointment == null) throw new InvalidOperationException("الموعد غير موجود");
        if (appointment.Patient == null) throw new InvalidOperationException("المريض غير مرتبط بالموعد");

        var parameters = new Dictionary<string, string>
        {
            ["patient_name"] = BuildPatientDisplayName(appointment.Patient),
            ["doctor_name"] = appointment.Doctor?.Name ?? "الطبيب",
            ["date"] = appointment.AppointmentDate.ToString("yyyy/MM/dd"),
            ["time"] = appointment.StartTime.ToString("HH:mm"),
            ["hours"] = request.HoursBefore.ToString(),
            ["clinic_name"] = await GetClinicNameAsync()
        };

        var smsRequest = new SendSmsRequest
        {
            PatientId = appointment.PatientId,
            TemplateType = "appointment_reminder",
            Parameters = parameters
        };

        smsRequest.RelatedEntityId = appointment.Id;
        smsRequest.RelatedEntityType = "Appointment";

        var result = await SendSmsAsync(smsRequest);

        // Mark appointment as reminder sent (reuse ConfirmationSent field)
        if (result.Status == "sent")
        {
            appointment.ConfirmationSent = true;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return result;
    }

    // ─── Send Pending Reminders ───────────────────────────────────────────
    public async Task<int> SendPendingRemindersAsync()
    {
        await EnsureSettingsCacheAsync();

        var settings = await GetGatewaySettingsAsync();
        if (!settings.Enabled || !settings.SendAppointmentReminders)
            return 0;

        var reminderHoursList = (settings.ReminderHours ?? "24,2")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(h => int.TryParse(h.Trim(), out var val) ? val : 0)
            .Where(h => h > 0)
            .ToList();

        if (reminderHoursList.Count == 0) return 0;

        int sentCount = 0;
        // CORE-APPT-008: this used DateTime.UtcNow, so the target day was the host
        // day, not the clinic day (Yemen is UTC+3 — between 21:00 and 00:00 UTC the
        // reminder run targeted the wrong date entirely).
        var now = ClinicTimeProvider.ClinicNow();

        foreach (var hoursBefore in reminderHoursList)
        {
            // CORE-APPT-008: `targetHour` used to be computed here and then never
            // used, and the query filtered by DATE ONLY — so enabling the 2-hour
            // window sent "موعدك بعد ساعتين" to every scheduled appointment on that
            // day regardless of its actual time. ReminderWindow.For resolves both the
            // clinic date and the one-hour bucket; see its tests for the arithmetic.
            var window = ReminderWindow.For(now, hoursBefore);

            var appointments = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.AppointmentDate == window.Date
                    && a.StartTime >= window.Start
                    && a.StartTime <= window.End
                    && a.Status == Domain.Enums.AppointmentStatus.Scheduled
                    && a.IsActive
                    && !a.ConfirmationSent)
                .ToListAsync();

            foreach (var appointment in appointments)
            {
                try
                {
                    await SendAppointmentReminderAsync(new SendSmsAppointmentReminderRequest
                    {
                        AppointmentId = appointment.Id,
                        HoursBefore = hoursBefore
                    });
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SMS reminder for appointment {Id}", appointment.Id);
                }
            }
        }

        return sentCount;
    }

    // ─── Dashboard ────────────────────────────────────────────────────────
    public async Task<SmsDashboardDto> GetDashboardAsync()
    {
        await EnsureSettingsCacheAsync();

        var settings = await GetGatewaySettingsAsync();
        var todayStart = DateTime.UtcNow.Date;
        var monthStart = DateTime.SpecifyKind(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), DateTimeKind.Utc);

        var todayMessages = await _db.SmsMessages
            .Where(m => m.CreatedAt >= todayStart && m.CreatedAt < todayStart.AddDays(1) && m.IsActive)
            .ToListAsync();

        var monthMessages = await _db.SmsMessages
            .Where(m => m.CreatedAt >= monthStart && m.IsActive)
            .ToListAsync();

        var recentMessages = await _db.SmsMessages
            .Include(m => m.Patient)
            .Where(m => m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .Select(m => MapToDto(m, m.Patient))
            .ToListAsync();

        var totalSentToday = todayMessages.Count(m => m.Status == "sent" || m.Status == "delivered");
        var totalDelivered = todayMessages.Count(m => m.Status == "delivered");
        var totalSent = todayMessages.Count(m => m.Status is "sent" or "delivered");

        return new SmsDashboardDto
        {
            IsEnabled = settings.Enabled,
            IsGatewayConnected = settings.IsGatewayConnected,
            GatewayUrl = settings.ApiUrl,
            SenderName = settings.SenderName,
            DailyLimit = settings.DailyLimit,
            SentToday = totalSentToday,
            SentThisMonth = monthMessages.Count(m => m.Status == "sent" || m.Status == "delivered"),
            FailedToday = todayMessages.Count(m => m.Status == "failed"),
            PendingCount = await _db.SmsMessages.CountAsync(m => m.Status == "pending" && m.IsActive),
            DeliveredToday = totalDelivered,
            DeliveryRate = totalSent > 0 ? Math.Round((decimal)totalDelivered / totalSent * 100, 1) : 0,
            RecentMessages = recentMessages
        };
    }

    // ─── Get Messages ─────────────────────────────────────────────────────
    public async Task<PagedResult<SmsMessageDto>> GetMessagesAsync(int page = 1, int pageSize = 20, string? status = null)
    {
        var query = _db.SmsMessages
            .Include(m => m.Patient)
            .Where(m => m.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(m => m.Status == status);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => MapToDto(m, m.Patient))
            .ToListAsync();

        return new PagedResult<SmsMessageDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    // ─── Retry Failed Message ─────────────────────────────────────────────
    public async Task<SmsMessageDto> RetryFailedMessageAsync(Guid messageId)
    {
        await EnsureSettingsCacheAsync();

        var message = await _db.SmsMessages
            .Include(m => m.Patient)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.IsActive);

        if (message == null) throw new InvalidOperationException("الرسالة غير موجودة");
        if (message.Status != "failed") throw new InvalidOperationException("يمكن إعادة إرسال الرسائل الفاشلة فقط");
        if (message.RetryCount >= 3) throw new InvalidOperationException("تم تجاوز الحد الأقصى لإعادة المحاولة (3)");

        message.Status = "pending";
        message.ErrorMessage = null;
        await _db.SaveChangesAsync();

        await SendViaGatewayAsync(message);

        return MapToDto(message, message.Patient);
    }

    // ─── Templates ────────────────────────────────────────────────────────
    public async Task<List<SmsTemplateDto>> GetTemplatesAsync()
    {
        var templates = await _db.SmsTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.TemplateKey)
            .ToListAsync();

        // Seed default templates if none exist
        if (templates.Count == 0)
        {
            await SeedDefaultTemplatesAsync();
            templates = await _db.SmsTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.Category)
                .ThenBy(t => t.TemplateKey)
                .ToListAsync();
        }

        return templates.Select(MapTemplateToDto).ToList();
    }

    public async Task<SmsTemplateDto> UpdateTemplateAsync(Guid templateId, string contentTemplate, string? nameAr = null)
    {
        var template = await _db.SmsTemplates.FindAsync(templateId);
        if (template == null) throw new InvalidOperationException("القالب غير موجود");

        template.ContentTemplate = contentTemplate;
        if (!string.IsNullOrWhiteSpace(nameAr))
            template.NameAr = nameAr;
        template.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return MapTemplateToDto(template);
    }

    // ─── Gateway Settings ─────────────────────────────────────────────────
    public async Task<SmsGatewaySettingsDto> GetGatewaySettingsAsync()
    {
        await EnsureSettingsCacheAsync();

        var enabledSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.enabled");
        var senderNameSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.sender_name");
        var dailyLimitSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.daily_limit");
        var apptRemindersSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.send_appointment_reminders");
        var paymentRemindersSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.send_payment_reminders");
        var reminderHoursSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.reminder_hours");
        var gatewayModeSetting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "sms.gateway_mode");

        bool enabled = enabledSetting?.Value?.ToLower() == "true";
        bool isConnected = false;

        if (enabled && IsConfigured)
        {
            try { isConnected = await TestGatewayConnectionInternalAsync(); }
            catch { isConnected = false; }
        }

        return new SmsGatewaySettingsDto
        {
            Enabled = enabled,
            ApiUrl = ApiUrl,
            HasApiKey = !string.IsNullOrWhiteSpace(ApiKey),
            GatewayMode = gatewayModeSetting?.Value ?? GatewayMode,
            SenderName = senderNameSetting?.Value ?? SenderName ?? "Aqlan Dental",
            DailyLimit = int.TryParse(dailyLimitSetting?.Value, out var limit) ? limit : 500,
            SendAppointmentReminders = apptRemindersSetting?.Value?.ToLower() != "false",
            SendPaymentReminders = paymentRemindersSetting?.Value?.ToLower() != "false",
            ReminderHours = reminderHoursSetting?.Value ?? "24,2",
            IsGatewayConnected = isConnected
        };
    }

    public async Task<SmsGatewaySettingsDto> UpdateGatewaySettingsAsync(UpdateSmsGatewaySettingsRequest request)
    {
        // Update all settings in DB (including ApiUrl, ApiKey, and GatewayMode)
        await UpsertSettingAsync("sms.enabled", request.Enabled.ToString().ToLower());
        await UpsertSettingAsync("sms.sender_name", request.SenderName ?? "Aqlan Dental");
        await UpsertSettingAsync("sms.daily_limit", request.DailyLimit.ToString());
        await UpsertSettingAsync("sms.send_appointment_reminders", request.SendAppointmentReminders.ToString().ToLower());
        await UpsertSettingAsync("sms.send_payment_reminders", request.SendPaymentReminders.ToString().ToLower());
        await UpsertSettingAsync("sms.reminder_hours", request.ReminderHours ?? "24,2");
        await UpsertSettingAsync("sms.gateway_mode", request.GatewayMode ?? "local_android");

        // Store ApiUrl and ApiKey in DB so they persist across restarts
        if (!string.IsNullOrWhiteSpace(request.ApiUrl))
        {
            await UpsertSettingAsync("sms.api_url", request.ApiUrl.Trim());
        }
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            await UpsertSettingAsync("sms.api_key", request.ApiKey.Trim());
        }

        // Invalidate cache so next read picks up new values
        _cacheExpiry = DateTime.MinValue;

        return await GetGatewaySettingsAsync();
    }

    // ─── Test Gateway Connection ──────────────────────────────────────────
    public async Task<bool> TestGatewayConnectionAsync()
    {
        await EnsureSettingsCacheAsync();
        return await TestGatewayConnectionInternalAsync();
    }

    /// <summary>
    /// Internal connection test — callers must ensure cache is loaded first.
    /// For local_android mode: GET {ApiUrl}/status
    /// For cloud_api mode: POST {ApiUrl} with empty body to check if endpoint responds
    /// </summary>
    private async Task<bool> TestGatewayConnectionInternalAsync()
    {
        if (!IsConfigured) return false;

        try
        {
            var client = _httpClientFactory.CreateClient("Sms");

            if (IsCloudApi)
            {
                // Cloud API: try a GET to the base domain to verify the service is reachable
                // (the full endpoint URL may not support GET, so we test the base)
                try
                {
                    var uri = new Uri(ApiUrl!);
                    var baseUrl = $"{uri.Scheme}://{uri.Host}";
                    var response = await client.GetAsync(baseUrl);
                    return response.IsSuccessStatusCode || (int)response.StatusCode < 500;
                }
                catch
                {
                    // If GET fails, try a simple POST with auth to see if gateway responds
                    try
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
                        if (!string.IsNullOrWhiteSpace(ApiKey))
                            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                        var response = await client.SendAsync(request);
                        // Any response (even 400) means the server is reachable
                        return (int)response.StatusCode < 500;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            else
            {
                // Local Android gateway: check /status endpoint
                var response = await client.GetAsync($"{ApiUrl}/status");
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }

    // ─── Private Helpers ───────────────────────────────────────────────────

    private async Task SendViaGatewayAsync(SmsMessage message)
    {
        if (!IsConfigured)
        {
            // Demo mode — simulate successful send
            message.Status = "sent";
            message.ExternalId = $"demo-{Guid.NewGuid():N}"[..20];
            message.SentAt = DateTime.UtcNow;
            message.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("SMS demo mode: simulated send to {Phone}", message.PhoneNumber);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Sms");

            // Build request based on gateway mode
            var sendUrl = IsCloudApi ? ApiUrl! : $"{ApiUrl}/sms/send";

            var payload = new
            {
                to = message.PhoneNumber,
                message = message.MessageContent,
                sender = SenderName ?? "AqlanDental"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, sendUrl)
            {
                Content = content
            };

            if (!string.IsNullOrWhiteSpace(ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseBody);

                message.Status = "sent";
                message.ExternalId = result.TryGetProperty("id", out var idProp) ? idProp.GetString()
                    : result.TryGetProperty("messageId", out var msgIdProp) ? msgIdProp.GetString()
                    : $"sms-{Guid.NewGuid():N}"[..20];
                message.Gateway = IsCloudApi ? "cloud_api" : "local_android";
                message.SentAt = DateTime.UtcNow;
                message.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                message.Status = "failed";
                message.ErrorMessage = $"HTTP {(int)response.StatusCode}: {errorBody}"[..500];
                message.RetryCount++;
                message.UpdatedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            message.Status = "failed";
            message.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 500)];
            message.RetryCount++;
            message.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// CORE-APPT-012: delegates to the canonical PhoneNormalizer instead of
    /// hand-rolling the rules, then prefixes "+" for the gateway.
    /// <para>
    /// The previous implementation filtered with char.IsDigit, which ACCEPTS
    /// Arabic-Indic digits (٠-٩) without converting them to ASCII — so every
    /// StartsWith check below then failed and an unusable non-ASCII number was
    /// handed to the gateway silently. It also never stripped the "00"
    /// international prefix, turning "00967770245745" into "+9670967770245745".
    /// PhoneNormalizer already handles both, and is what Patients.NormalizedPhone
    /// is built with — so lookups and sends now agree on one format.
    /// </para>
    /// </summary>
    private static string NormalizePhone(string? phone)
    {
        var normalized = PhoneNormalizer.Normalize(phone);
        return string.IsNullOrEmpty(normalized) ? "" : $"+{normalized}";
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, string> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            template = template.Replace($"{{{{{key}}}}}", value);
        }
        return template;
    }

    private static (int charCount, int segmentCount) CalculateSegments(string content)
    {
        var charCount = content.Length;
        // Check if content contains Arabic/Unicode characters (UCS-2 encoding)
        bool isUnicode = content.Any(c => c > 127);
        int charsPerSegment = isUnicode ? 70 : 160;

        if (charCount <= charsPerSegment)
            return (charCount, 1);

        // Multi-segment: first segment has slightly less capacity due to UDH header
        int charsPerSegmentWithUdh = isUnicode ? 67 : 153;
        int segments = (int)Math.Ceiling((double)(charCount - charsPerSegment) / charsPerSegmentWithUdh) + 1;
        return (charCount, segments);
    }

    private async Task<SmsTemplate?> GetActiveTemplate(string templateType)
    {
        return await _db.SmsTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateType && t.IsTemplateActive && t.IsActive);
    }

    private async Task SeedDefaultTemplatesAsync()
    {
        var defaults = new List<SmsTemplate>
        {
            new()
            {
                TemplateKey = "appointment_reminder",
                NameAr = "تذكير موعد",
                ContentTemplate = "عزيزي {{patient_name}}، تذكير بموعدكم في عيادة أقلان لطب الأسنان بتاريخ {{date}} الساعة {{time}} مع د. {{doctor_name}}",
                Category = "appointment",
                MaxLength = 160
            },
            new()
            {
                TemplateKey = "appointment_reminder_urgent",
                NameAr = "تذكير موعد عاجل",
                ContentTemplate = "تذكير: موعدكم اليوم الساعة {{time}} مع د. {{doctor_name}} في عيادة أقلان. يرجى الحضور قبل الموعد بـ10 دقائق",
                Category = "appointment",
                MaxLength = 160
            },
            new()
            {
                TemplateKey = "payment_reminder",
                NameAr = "تذكير دفع",
                ContentTemplate = "عزيزي {{patient_name}}، لديكم مبلغ مستحق قدره {{amount}} ر.ي. يرجى المرور على العيادة لسداد المبلغ. شكراً لكم",
                Category = "payment",
                MaxLength = 160
            },
            new()
            {
                TemplateKey = "follow_up",
                NameAr = "متابعة",
                ContentTemplate = "عزيزي {{patient_name}}، نأمل أن تكونوا بخير. نذكركم بضرورة زيارة المتابعة. للحجز اتصلوا على {{clinic_phone}}",
                Category = "follow_up",
                MaxLength = 160
            },
            new()
            {
                TemplateKey = "no_show",
                NameAr = "عدم حضور",
                ContentTemplate = "عزيزي {{patient_name}}، لم نتمكن من رؤيتكم اليوم. يرجى الاتصال على {{clinic_phone}} لإعادة حجز موعدكم",
                Category = "appointment",
                MaxLength = 160
            }
        };

        _db.SmsTemplates.AddRange(defaults);
        await _db.SaveChangesAsync();
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            _db.Settings.Add(new Setting { Key = key, Value = value, Category = "sms", UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    private async Task<string> GetClinicNameAsync()
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(s => s.Key == "clinic.name");
        return setting?.Value ?? "عيادة أقلان لطب الأسنان";
    }

    private static string BuildPatientDisplayName(Patient? patient)
    {
        if (patient == null) return "";
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(patient.FirstName)) parts.Add(patient.FirstName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.MiddleName)) parts.Add(patient.MiddleName.Trim());
        if (!string.IsNullOrWhiteSpace(patient.LastName)) parts.Add(patient.LastName.Trim());
        return string.Join(" ", parts);
    }

    private static SmsMessageDto MapToDto(SmsMessage message, Patient? patient)
    {
        return new SmsMessageDto
        {
            Id = message.Id,
            PatientId = message.PatientId,
            PatientName = patient != null ? BuildPatientDisplayName(patient) : "",
            PhoneNumber = message.PhoneNumber,
            TemplateType = message.TemplateType,
            MessageContent = message.MessageContent,
            Status = message.Status,
            ExternalId = message.ExternalId,
            ErrorMessage = message.ErrorMessage,
            RetryCount = message.RetryCount,
            SentAt = message.SentAt,
            DeliveredAt = message.DeliveredAt,
            RelatedEntityId = message.RelatedEntityId,
            RelatedEntityType = message.RelatedEntityType,
            Gateway = message.Gateway,
            CharacterCount = message.CharacterCount,
            SegmentCount = message.SegmentCount,
            CreatedAt = message.CreatedAt
        };
    }

    private static SmsTemplateDto MapTemplateToDto(SmsTemplate template)
    {
        return new SmsTemplateDto
        {
            Id = template.Id,
            TemplateKey = template.TemplateKey,
            NameAr = template.NameAr,
            ContentTemplate = template.ContentTemplate,
            IsTemplateActive = template.IsTemplateActive,
            Category = template.Category,
            MaxLength = template.MaxLength
        };
    }
}
