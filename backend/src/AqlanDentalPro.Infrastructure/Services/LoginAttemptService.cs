using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AqlanDentalPro.Infrastructure.Services;

public class LoginAttemptService : ILoginAttemptService
{
    private readonly IDatabase _redis;
    private readonly ILogger<LoginAttemptService> _logger;

    // Lock out after 5 failed attempts
    private const int MaxFailedAttempts = 5;
    // Lock out duration: 15 minutes
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    // Failed attempts window: 15 minutes (counter resets after this)
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    public LoginAttemptService(IConnectionMultiplexer redis, ILogger<LoginAttemptService> logger)
    {
        _redis = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<int> RecordFailedAttemptAsync(string username)
    {
        var key = $"login:fail:{username}";
        var lockKey = $"login:lock:{username}";

        var raw = _redis.StringGet(key);
        var currentCount = raw.IsNull ? 0 : (int)raw;
        currentCount++;

        _logger.LogWarning("Failed login attempt {Count}/{Max} for user '{Username}'", 
            currentCount, MaxFailedAttempts, username);

        if (currentCount >= MaxFailedAttempts)
        {
            // Set lockout
            _redis.StringSet(lockKey, DateTime.UtcNow.Add(LockoutDuration).ToString("O"), LockoutDuration);
            _logger.LogWarning("Account '{Username}' locked out for {Minutes} minutes", 
                username, LockoutDuration.TotalMinutes);
        }

        // Increment counter with expiry window
        _redis.StringSet(key, currentCount, AttemptWindow);
        return currentCount;
    }

    public async Task<(bool IsLocked, int RemainingMinutes)> IsLockedOutAsync(string username)
    {
        var lockKey = $"login:lock:{username}";
        var lockValue = await _redis.StringGetAsync(lockKey);

        if (!lockValue.HasValue || !DateTime.TryParse(lockValue.ToString(), out var lockUntil))
            return (false, 0);

        if (DateTime.UtcNow < lockUntil)
        {
            var remaining = (int)Math.Ceiling((lockUntil - DateTime.UtcNow).TotalMinutes);
            return (true, remaining);
        }

        // Lock expired, clean up
        _redis.KeyDelete(lockKey);
        return (false, 0);
    }

    public async Task ResetFailedAttemptsAsync(string username)
    {
        var key = $"login:fail:{username}";
        var lockKey = $"login:lock:{username}";
        _redis.KeyDelete([key, lockKey]);
        _logger.LogInformation("Failed login attempts reset for user '{Username}'", username);
    }
}
