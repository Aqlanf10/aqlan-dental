using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace AqlanDentalPro.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<bool> IsConfiguredAsync()
    {
        var host = _config["SMTP_HOST"] ?? _config["Smtp:Host"] ?? "";
        var isConfigured = !string.IsNullOrWhiteSpace(host);
        return Task.FromResult(isConfigured);
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl)
    {
        try
        {
            var host = _config["SMTP_HOST"] ?? _config["Smtp:Host"] ?? "";
            if (string.IsNullOrWhiteSpace(host))
            {
                _logger.LogWarning("SMTP not configured — cannot send password reset email to {Email}", toEmail);
                return false;
            }

            var port = int.Parse(_config["SMTP_PORT"] ?? _config["Smtp:Port"] ?? "587");
            var username = _config["SMTP_USERNAME"] ?? _config["Smtp:Username"] ?? "";
            var password = _config["SMTP_PASSWORD"] ?? _config["Smtp:Password"] ?? "";
            var fromEmail = _config["SMTP_FROM_EMAIL"] ?? _config["Smtp:FromEmail"] ?? "noreply@aqlandental.com";
            var fromName = _config["SMTP_FROM_NAME"] ?? _config["Smtp:FromName"] ?? "مركز د. عقلان الكامل";
            var appUrl = _config["APP_PUBLIC_URL"] ?? _config["App:PublicUrl"] ?? "http://localhost:3000";

            var fullResetUrl = $"{appUrl.TrimEnd('/')}/{resetUrl.TrimStart('/')}?token={Uri.EscapeDataString(resetToken)}";

            var subject = "استعادة كلمة المرور — مركز د. عقلان الكامل";

            var body = $@"
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
            <h1>🦷 مركز د. عقلان الكامل</h1>
        </div>
        <div class='content'>
            <p>السلام عليكم،</p>
            <p>تلقينا طلباً لاستعادة كلمة المرور الخاصة بحسابكم في نظام مركز د. عقلان الكامل لطب وتقويم الأسنان.</p>
            <div style='text-align: center;'>
                <a href='{fullResetUrl}' class='btn'>إعادة تعيين كلمة المرور</a>
            </div>
            <p>إذا لم يعمل الزر أعلاه، يمكنكم نسخ الرابط التالي ولصقه في المتصفح:</p>
            <p style='direction: ltr; text-align: left; word-break: break-all; font-size: 13px; color: #666;'>{fullResetUrl}</p>
            <div class='warning'>
                ⚠️ هذا الرابط صالح لمدة 30 دقيقة فقط. إذا لم تطلبوا استعادة كلمة المرور، يمكنكم تجاهل هذه الرسالة بأمان.
            </div>
        </div>
        <div class='footer'>
            <p>© {DateTime.UtcNow.Year} مركز د. عقلان الكامل لطب وتقويم الأسنان</p>
            <p>تعز، اليمن — شارع التحرير الأعلى</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(host, port);
            client.EnableSsl = true;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(username, password);

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName, Encoding.UTF8),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Password reset email sent successfully to {Email}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset email to {Email}. SMTP error — email not sent.", toEmail);
            return false;
        }
    }
}
