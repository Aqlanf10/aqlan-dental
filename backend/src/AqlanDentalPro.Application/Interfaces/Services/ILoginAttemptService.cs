namespace AqlanDentalPro.Application.Interfaces.Services;

public interface ILoginAttemptService
{
    /// <summary>
    /// Records a failed login attempt for the given username.
    /// Returns the current count of consecutive failed attempts.
    /// </summary>
    Task<int> RecordFailedAttemptAsync(string username);

    /// <summary>
    /// Checks if the account is currently locked out.
    /// Returns (isLocked, remainingMinutes).
    /// </summary>
    Task<(bool IsLocked, int RemainingMinutes)> IsLockedOutAsync(string username);

    /// <summary>
    /// Resets the failed attempt counter (called on successful login).
    /// </summary>
    Task ResetFailedAttemptsAsync(string username);
}
