namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IRecaptchaService
{
    /// <summary>
    /// Validates a reCAPTCHA v3 token.
    /// Returns (isValid, score, errorMessage).
    /// </summary>
    Task<(bool IsValid, float Score, string? ErrorMessage)> ValidateTokenAsync(string token);
}
