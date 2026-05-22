namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetToken, string resetUrl);
    Task<bool> IsConfiguredAsync();
}
