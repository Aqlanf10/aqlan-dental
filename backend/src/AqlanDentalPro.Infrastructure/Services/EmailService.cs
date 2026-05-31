using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Web;

namespace AqlanDentalPro.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Daily email limit — configurable via EMAIL_DAILY_LIMIT env var. Default: 100.</summary>
    private int DailyLimit => GetDailyLimit();

    private int GetDailyLimit()
    {
        var envVal = Environment.GetEnvironmentVariable("EMAIL_DAILY_LIMIT");
        if (int.TryParse(envVal, out var limit) && limit > 0)
            return limit;
        var cfgVal = _config["EMAIL_DAILY_LIMIT"];
        if (int.TryParse(cfgVal, out var cfgLimit) && cfgLimit > 0)
            return cfgLimit;
        return 100; // Default
    }

    /// <summary>Public accessor for the configured daily limit (used by EmailStatsController).</summary>
    public int ConfiguredDailyLimit => DailyLimit;

    public EmailService(IConfiguration config, ILogger<EmailService> logger, IServiceScopeFactory scopeFactory)
    {
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task<bool> IsConfiguredAsync()
    {
        var resendKey = GetConfig("RESEND_API_KEY");
        var smtpHost = GetConfig("SMTP_HOST", "Smtp:Host");
        var isConfigured = !string.IsNullOrWhiteSpace(resendKey) || !string.IsNullOrWhiteSpace(smtpHost);

        _logger.LogInformation(
            "Email configuration check: RESEND_API_KEY={ResendSet}, SMTP_HOST={SmtpSet}, IsConfigured={IsConfigured}",
            !string.IsNullOrWhiteSpace(resendKey) ? "(set)" : "(not set)",
            !string.IsNullOrWhiteSpace(smtpHost) ? "(set)" : "(not set)",
            isConfigured);

        if (!isConfigured)
        {
            _logger.LogWarning(
                "Email not configured. Set RESEND_API_KEY (recommended) or SMTP_HOST env variables " +
                "to enable email-based password reset. Without email, forgot-password falls back " +
                "to admin-managed PasswordResetRequest.");
        }
        return Task.FromResult(isConfigured);
    }

    /// <summary>
    /// Returns the number of emails sent today (UTC). Used for daily limit tracking.
    /// </summary>
    public async Task<int> GetDailyEmailCountAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var todayStart = DateTime.UtcNow.Date;
            return await db.EmailLogs
                .CountAsync(e => e.IsSent && e.CreatedAt >= todayStart);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get daily email count from EmailLogs.");
            return 0;
        }
    }

    /// <summary>
    /// Generic email sending method. Can be used for appointment reminders,
    /// notifications, or any email type. Logs to EmailLog table for statistics.
    /// </summary>
    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? textBody = null)
    {
        return await SendEmailInternalAsync(toEmail, subject, htmlBody, "general", null, null);
    }

    /// <summary>
    /// Sends an appointment reminder email with proper category and entity tracking.
    /// Logs to EmailLog with category "appointment_reminder" and links to the appointment entity.
    /// </summary>
    public async Task<bool> SendAppointmentReminderAsync(string toEmail, string subject, string htmlBody, Guid appointmentId)
    {
        return await SendEmailInternalAsync(toEmail, subject, htmlBody, "appointment_reminder", "Appointment", appointmentId);
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl)
    {
        var appUrl = GetConfig("APP_PUBLIC_URL", "App:PublicUrl") ?? "http://localhost:3000";
        var fullResetUrl = $"{appUrl.TrimEnd('/')}/{resetUrl.TrimStart('/')}?token={Uri.EscapeDataString(resetToken)}";

        var subject = "استعادة كلمة المرور — مركز د. عقلان الكامل";
        var htmlBody = BuildResetEmailHtml(fullResetUrl);

        return await SendEmailInternalAsync(toEmail, subject, htmlBody, "password_reset");
    }

    /// <summary>
    /// Internal send method with logging. All email sending goes through here.
    /// </summary>
    private async Task<bool> SendEmailInternalAsync(
        string toEmail, string subject, string htmlBody,
        string category, string? relatedEntityType = null, Guid? relatedEntityId = null)
    {
        var dailyLimit = DailyLimit;

        // ── Daily limit check ──
        var dailyCount = await GetDailyEmailCountAsync();
        if (dailyCount >= dailyLimit)
        {
            _logger.LogWarning(
                "Daily email limit ({Limit}) reached. Skipping email to {To}. " +
                "Upgrade Resend plan or set EMAIL_DAILY_LIMIT to increase limit.",
                dailyLimit, MaskEmail(toEmail));
            await LogEmailAsync(toEmail, subject, category, null, false,
                $"Daily limit ({dailyLimit}) reached", null, relatedEntityType, relatedEntityId);
            return false;
        }

        if (dailyCount >= dailyLimit - 10)
        {
            _logger.LogWarning(
                "Approaching daily email limit: {Count}/{Limit}. " +
                "Consider upgrading Resend plan or setting EMAIL_DAILY_LIMIT.",
                dailyCount, dailyLimit);
        }

        var fromEmail = GetConfig("SMTP_FROM_EMAIL", "Smtp:FromEmail") ?? "onboarding@resend.dev";
        var fromName = GetConfig("SMTP_FROM_NAME", "Smtp:FromName") ?? "مركز د. عقلان الكامل";

        // ── Priority 1: Resend API (HTTP-based, works from any cloud server) ──
        var resendKey = GetConfig("RESEND_API_KEY") ?? "";
        if (!string.IsNullOrWhiteSpace(resendKey))
        {
            var (sent, externalId, error) = await SendViaResendAsync(resendKey, toEmail, fromEmail, fromName, subject, htmlBody);
            if (sent)
            {
                await LogEmailAsync(toEmail, subject, category, "resend", true, null, externalId, relatedEntityType, relatedEntityId);
                return true;
            }

            await LogEmailAsync(toEmail, subject, category, "resend", false, error, null, relatedEntityType, relatedEntityId);
            _logger.LogWarning("Resend API failed, falling back to SMTP if configured.");
        }

        // ── Priority 2: SMTP (traditional, may be blocked by cloud providers) ──
        var smtpHost = GetConfig("SMTP_HOST", "Smtp:Host") ?? "";
        if (!string.IsNullOrWhiteSpace(smtpHost))
        {
            var (sent, error) = await SendViaSmtpAsync(smtpHost, toEmail, fromEmail, fromName, subject, htmlBody);
            if (sent)
            {
                await LogEmailAsync(toEmail, subject, category, "smtp", true, null, null, relatedEntityType, relatedEntityId);
                return true;
            }

            await LogEmailAsync(toEmail, subject, category, "smtp", false, error, null, relatedEntityType, relatedEntityId);
        }

        _logger.LogWarning("All email providers failed. Email not sent to {To}.", MaskEmail(toEmail));
        return false;
    }

    /// <summary>
    /// Persists an email log entry to the database.
    /// Uses a separate scope to avoid DbContext concurrency issues from BackgroundServices.
    /// </summary>
    private async Task LogEmailAsync(
        string toEmail, string subject, string category,
        string? provider, bool isSent, string? errorMessage,
        string? externalId, string? relatedEntityType, Guid? relatedEntityId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.EmailLogs.Add(new EmailLog
            {
                ToEmail = toEmail,
                Subject = subject,
                Category = category,
                Provider = provider,
                IsSent = isSent,
                ErrorMessage = errorMessage?.Length > 500 ? errorMessage[..500] : errorMessage,
                ExternalId = externalId,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log email to EmailLogs table.");
        }
    }

    /// <summary>
    /// Reads a config value from IConfiguration first, then falls back to
    /// Environment.GetEnvironmentVariable() directly. Railway and other cloud
    /// platforms sometimes inject env vars that IConfiguration doesn't pick up.
    /// </summary>
    private string? GetConfig(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _config[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        // Fallback: read directly from environment variables (works when IConfiguration
        // doesn't see the env var — common on Railway, Render, etc.)
        foreach (var key in keys)
        {
            var envKey = key.Replace(":", "__"); // .NET config uses : but env vars use __
            var value = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrWhiteSpace(value))
            {
                _logger.LogDebug("Config key '{Key}' not found in IConfiguration but found via Environment.GetEnvironmentVariable.", key);
                return value;
            }
        }
        return null;
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

    /// <summary>
    /// Sends email via Resend.com HTTP API. This is the recommended method for cloud deployments
    /// because it uses HTTPS instead of SMTP, which avoids port blocking and IP reputation issues.
    /// Free tier: 100 emails/day, 1 custom domain. Sign up at https://resend.com
    /// </summary>
    private async Task<(bool success, string? externalId, string? error)> SendViaResendAsync(
        string apiKey, string toEmail, string fromEmail, string fromName, string subject, string htmlBody)
    {
        try
        {
            var fromField = $"{fromName} <{fromEmail}>";
            _logger.LogInformation("Attempting to send email via Resend API to {To}.", MaskEmail(toEmail));

            var payload = new
            {
                from = fromField,
                to = new[] { toEmail },
                subject,
                html = htmlBody
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                string? emailId = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    emailId = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                }
                catch { /* non-critical */ }

                _logger.LogInformation("Email sent successfully via Resend API. ExternalId={ExternalId}.", emailId ?? "N/A");
                return (true, emailId, null);
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Resend API returned {StatusCode}: {Body}. Email not sent.",
                (int)response.StatusCode, errorBody);
            return (false, null, $"HTTP {(int)response.StatusCode}: {errorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resend API exception while sending email.");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Sends email via traditional SMTP. May fail from cloud servers (Railway, AWS, etc.)
    /// because Gmail and other providers often block SMTP connections from cloud IP ranges.
    /// </summary>
    private async Task<(bool success, string? error)> SendViaSmtpAsync(
        string host, string toEmail, string fromEmail, string fromName, string subject, string htmlBody)
    {
        try
        {
            var port = int.Parse(GetConfig("SMTP_PORT", "Smtp:Port") ?? "587");
            var username = GetConfig("SMTP_USERNAME", "Smtp:Username") ?? "";
            var password = GetConfig("SMTP_PASSWORD", "Smtp:Password") ?? "";

            using var client = new SmtpClient(host, port);
            client.EnableSsl = true;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password);

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email sent successfully via SMTP.");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP failed. This is common from cloud servers — use Resend API instead.");
            return (false, ex.Message);
        }
    }

    private static string BuildResetEmailHtml(string fullResetUrl)
    {
        return $@"
<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Arial, sans-serif; direction: rtl; background-color: #f5f5f5; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #0E7490, #059669); padding: 30px; text-align: center; color: white; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 30px; }}
        .content p {{ font-size: 16px; line-height: 1.8; color: #333; }}
        .btn {{ display: inline-block; background: #0E7490; color: white; padding: 14px 40px; text-decoration: none; border-radius: 8px; font-size: 18px; font-weight: bold; margin: 20px 0; }}
        .footer {{ background: #f9f9f9; padding: 20px; text-align: center; color: #888; font-size: 13px; border-top: 1px solid #eee; }}
        .warning {{ background: #FFF3CD; border: 1px solid #FFEEBA; border-radius: 8px; padding: 15px; margin: 15px 0; color: #856404; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>&#x1F9B7; مركز د. عقلان الكامل</h1>
        </div>
        <div class='content'>
            <p>السلام عليكم،</p>
            <p>تلقينا طلباً لاستعادة كلمة المرور الخاصة بحسابكم في نظام مركز د. عقلان الكامل لطب وتقويم الأسنان.</p>
            <div style='text-align: center;'>
                <a href='{HttpUtility.HtmlAttributeEncode(fullResetUrl)}' class='btn'>إعادة تعيين كلمة المرور</a>
            </div>
            <p>إذا لم يعمل الزر أعلاه، يمكنكم نسخ الرابط التالي ولصقه في المتصفح:</p>
            <p style='direction: ltr; text-align: left; word-break: break-all; font-size: 13px; color: #666;'>{HttpUtility.HtmlEncode(fullResetUrl)}</p>
            <div class='warning'>
                &#x26A0;&#xFE0F; هذا الرابط صالح لمدة 30 دقيقة فقط. إذا لم تطلبوا استعادة كلمة المرور، يمكنكم تجاهل هذه الرسالة بأمان.
            </div>
        </div>
        <div class='footer'>
            <p>&#x00A9; {DateTime.UtcNow.Year} مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
            <p>تعز، اليمن — شارع التحرير الأعلى</p>
        </div>
    </div>
</body>
</html>";
    }

    /// <summary>
    /// Builds an appointment reminder email HTML body.
    /// All dynamic user data is HTML-encoded to prevent injection.
    /// </summary>
    public static string BuildAppointmentReminderHtml(
        string patientName, string doctorName, string appointmentDate,
        string appointmentTime, string? clinicService, string? notes)
    {
        // HTML-encode all dynamic content to prevent XSS/injection
        var safePatientName = HttpUtility.HtmlEncode(patientName);
        var safeDoctorName = HttpUtility.HtmlEncode(doctorName);
        var safeDate = HttpUtility.HtmlEncode(appointmentDate);
        var safeTime = HttpUtility.HtmlEncode(appointmentTime);
        var safeService = !string.IsNullOrWhiteSpace(clinicService)
            ? $"<p><strong>الخدمة:</strong> {HttpUtility.HtmlEncode(clinicService)}</p>"
            : "";
        var safeNotes = !string.IsNullOrWhiteSpace(notes)
            ? $"<p><strong>ملاحظات:</strong> {HttpUtility.HtmlEncode(notes)}</p>"
            : "";

        return $@"
<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Arial, sans-serif; direction: rtl; background-color: #f5f5f5; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #fff; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #0E7490, #059669); padding: 30px; text-align: center; color: white; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ padding: 30px; }}
        .content p {{ font-size: 16px; line-height: 1.8; color: #333; }}
        .info-box {{ background: #F0FDFA; border: 1px solid #99F6E4; border-radius: 8px; padding: 20px; margin: 15px 0; }}
        .info-box p {{ margin: 5px 0; }}
        .footer {{ background: #f9f9f9; padding: 20px; text-align: center; color: #888; font-size: 13px; border-top: 1px solid #eee; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>&#x1F9B7; تذكير بموعد — مركز د. عقلان الكامل</h1>
        </div>
        <div class='content'>
            <p>السلام عليكم {safePatientName}،</p>
            <p>نود تذكيركم بموعدكم القادم في مركز د. عقلان الكامل لطب وتقويم الأسنان.</p>
            <div class='info-box'>
                <p><strong>الطبيب:</strong> {safeDoctorName}</p>
                <p><strong>التاريخ:</strong> {safeDate}</p>
                <p><strong>الوقت:</strong> {safeTime}</p>
                {safeService}
                {safeNotes}
            </div>
            <p>يرجى الحضور قبل الموعد بـ 10 دقائق. في حال الرغبة بإلغاء أو تغيير الموعد، يرجى التواصل مع المركز.</p>
        </div>
        <div class='footer'>
            <p>&#x00A9; {DateTime.UtcNow.Year} مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
            <p>تعز، اليمن — شارع التحرير الأعلى</p>
        </div>
    </div>
</body>
</html>";
    }
}
