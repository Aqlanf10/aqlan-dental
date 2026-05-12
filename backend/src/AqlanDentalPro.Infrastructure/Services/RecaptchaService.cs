using System.Text.Json;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public class RecaptchaService : IRecaptchaService
{
    private readonly HttpClient _httpClient;
    private readonly string _secretKey;
    private readonly float _minimumScore;
    private readonly ILogger<RecaptchaService> _logger;
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    public RecaptchaService(HttpClient httpClient, IConfiguration configuration, ILogger<RecaptchaService> logger)
    {
        _httpClient = httpClient;
        _secretKey = configuration["Recaptcha:SecretKey"] ?? "";
        _minimumScore = configuration.GetValue("Recaptcha:MinimumScore", 0.5f);
        _logger = logger;
    }

    public async Task<(bool IsValid, float Score, string? ErrorMessage)> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, 0f, "رمز التحقق مطلوب");

        if (string.IsNullOrWhiteSpace(_secretKey))
        {
            // reCAPTCHA not configured — log warning and allow (dev mode)
            _logger.LogWarning("reCAPTCHA secret key not configured — skipping validation");
            return (true, 1f, null);
        }

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _secretKey,
                ["response"] = token
            });

            var response = await _httpClient.PostAsync(VerifyUrl, content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);

            var success = result.GetProperty("success").GetBoolean();
            if (!success)
            {
                var errorCodes = result.GetProperty("error-codes");
                var errors = new List<string>();
                foreach (var error in errorCodes.EnumerateArray())
                    errors.Add(error.GetString() ?? "unknown");

                _logger.LogWarning("reCAPTCHA validation failed: {Errors}", string.Join(", ", errors));
                return (false, 0f, "فشل التحقق من reCAPTCHA");
            }

            var score = result.GetProperty("score").GetSingle();
            var action = result.GetProperty("action").GetString();

            _logger.LogDebug("reCAPTCHA result: score={Score}, action={Action}", score, action);

            if (score < _minimumScore)
            {
                _logger.LogWarning("reCAPTCHA score {Score} below minimum {MinimumScore}", score, _minimumScore);
                return (false, score, "لم تجتز التحقق الأمني. حاول مرة أخرى.");
            }

            return (true, score, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "reCAPTCHA validation error");
            // Fail open on network errors to avoid blocking legitimate users
            return (true, 0f, null);
        }
    }
}
