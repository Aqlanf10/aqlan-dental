using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Tracks failed login attempts and account lockout state in Redis.
///
/// HOTFIX: All Redis operations are wrapped in try/catch to prevent
/// Redis unavailability from breaking the login flow. When Redis is down:
/// - IsLockedOutAsync returns (false, 0) — fail-open, login proceeds
/// - RecordFailedAttemptAsync returns 0 — lockout counter is not tracked
/// - ResetFailedAttemptsAsync no-ops — silently continues
/// This means login still works when Redis is unavailable, but lockout
/// protection is temporarily degraded until Redis recovers.
/// </summary>
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

    public Task<int> RecordFailedAttemptAsync(string username)
    {
        try
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
            return Task.FromResult(currentCount);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during RecordFailedAttemptAsync for user '{Username}'. Lockout counter not updated. Login will proceed without lockout protection.",
                username);
            return Task.FromResult(0);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during RecordFailedAttemptAsync for user '{Username}'. Lockout counter not updated.",
                username);
            return Task.FromResult(0);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during RecordFailedAttemptAsync for user '{Username}'. Lockout counter not updated.",
                username);
            return Task.FromResult(0);
        }
    }

    public async Task<(bool IsLocked, int RemainingMinutes)> IsLockedOutAsync(string username)
    {
        try
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
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during IsLockedOutAsync for user '{Username}'. Assuming NOT locked (fail-open). Lockout protection is degraded.",
                username);
            return (false, 0);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during IsLockedOutAsync for user '{Username}'. Assuming NOT locked (fail-open).",
                username);
            return (false, 0);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during IsLockedOutAsync for user '{Username}'. Assuming NOT locked (fail-open).",
                username);
            return (false, 0);
        }
    }

    public Task ResetFailedAttemptsAsync(string username)
    {
        try
        {
            var key = $"login:fail:{username}";
            var lockKey = $"login:lock:{username}";
            _redis.KeyDelete([key, lockKey]);
            _logger.LogInformation("Failed login attempts reset for user '{Username}'", username);
            return Task.CompletedTask;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during ResetFailedAttemptsAsync for user '{Username}'. Reset skipped.",
                username);
            return Task.CompletedTask;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during ResetFailedAttemptsAsync for user '{Username}'. Reset skipped.",
                username);
            return Task.CompletedTask;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during ResetFailedAttemptsAsync for user '{Username}'. Reset skipped.",
                username);
            return Task.CompletedTask;
        }
    }
}
