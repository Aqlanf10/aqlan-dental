namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl);
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? textBody = null);
    Task<bool> SendAppointmentReminderAsync(string toEmail, string subject, string htmlBody, Guid appointmentId);
    Task<bool> IsConfiguredAsync();
    Task<int> GetDailyEmailCountAsync();
    /// <summary>Returns the configured daily email limit (from EMAIL_DAILY_LIMIT env var or default 100).</summary>
    int ConfiguredDailyLimit { get; }
}
