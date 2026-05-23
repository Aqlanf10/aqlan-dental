namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl);
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? textBody = null);
    Task<bool> IsConfiguredAsync();
    Task<int> GetDailyEmailCountAsync();
}
