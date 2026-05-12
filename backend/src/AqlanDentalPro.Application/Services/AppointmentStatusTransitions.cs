using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Defines valid status transitions for appointments.
/// Prevents invalid state changes (e.g., jumping from Scheduled directly to InProgress).
/// </summary>
public static class AppointmentStatusTransitions
{
    // Each status maps to the set of statuses it can transition TO.
    private static readonly Dictionary<AppointmentStatus, HashSet<AppointmentStatus>> ValidTransitions = new()
    {
        // New appointment can be confirmed, cancelled, or marked no-show
        [AppointmentStatus.Scheduled] = new()
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.Arrived,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Confirmed can move to arrived, cancelled, or no-show
        [AppointmentStatus.Confirmed] = new()
        {
            AppointmentStatus.Scheduled,  // Un-confirm
            AppointmentStatus.Arrived,
            AppointmentStatus.Waiting,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Arrived can move to waiting, cancelled, or no-show
        [AppointmentStatus.Arrived] = new()
        {
            AppointmentStatus.Scheduled,  // Re-schedule
            AppointmentStatus.Confirmed,  // Back to confirmed
            AppointmentStatus.Waiting,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Waiting can be called back to confirmed/arrived, cancelled, or no-show
        [AppointmentStatus.Waiting] = new()
        {
            AppointmentStatus.Called,
            AppointmentStatus.Confirmed,
            AppointmentStatus.Arrived,
            AppointmentStatus.Cancelled,
            AppointmentStatus.NoShow
        },
        // Called can go to in-room or back to waiting
        [AppointmentStatus.Called] = new()
        {
            AppointmentStatus.InRoom,
            AppointmentStatus.Waiting,
            AppointmentStatus.Cancelled
        },
        // In room can start treatment or go back
        [AppointmentStatus.InRoom] = new()
        {
            AppointmentStatus.InProgress,
            AppointmentStatus.Called,
            AppointmentStatus.Cancelled
        },
        // In progress can only be completed or cancelled
        [AppointmentStatus.InProgress] = new()
        {
            AppointmentStatus.Completed,
            AppointmentStatus.Cancelled
        },
        // Terminal states — no transitions out
        [AppointmentStatus.Completed] = new(),
        [AppointmentStatus.Cancelled] = new(),
        [AppointmentStatus.NoShow] = new()
    };

    /// <summary>
    /// Checks if a transition from currentStatus to newStatus is valid.
    /// </summary>
    public static bool IsValidTransition(AppointmentStatus currentStatus, AppointmentStatus newStatus)
    {
        // Same status is always valid (idempotent)
        if (currentStatus == newStatus) return true;

        // Check if the current status has any defined transitions
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return false;

        return allowedStatuses.Contains(newStatus);
    }

    /// <summary>
    /// Gets all valid target statuses for a given current status.
    /// </summary>
    public static IEnumerable<AppointmentStatus> GetAllowedTransitions(AppointmentStatus currentStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return [];

        // Always include current status (idempotent)
        return allowedStatuses.Prepend(currentStatus);
    }
}
