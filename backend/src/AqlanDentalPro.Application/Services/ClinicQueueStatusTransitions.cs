using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// CON-01 FIX: Defines valid status transitions for clinic queue items.
/// Centralizes the transition rules that were previously scattered in the controller.
/// Prevents invalid state changes (e.g., jumping from Waiting directly to InProgress).
/// </summary>
public static class ClinicQueueStatusTransitions
{
    // Each status maps to the set of statuses it can transition TO.
    private static readonly Dictionary<ClinicQueueStatus, HashSet<ClinicQueueStatus>> ValidTransitions = new()
    {
        [ClinicQueueStatus.Waiting] = new()
        {
            ClinicQueueStatus.Called,
            ClinicQueueStatus.InRoom,    // Direct entry if business allows
            ClinicQueueStatus.Cancelled
        },
        [ClinicQueueStatus.Called] = new()
        {
            ClinicQueueStatus.InRoom,
            ClinicQueueStatus.Waiting,   // Return to waiting
            ClinicQueueStatus.InProgress, // Direct start from called
            ClinicQueueStatus.Cancelled
        },
        [ClinicQueueStatus.InRoom] = new()
        {
            ClinicQueueStatus.InProgress,
            ClinicQueueStatus.Called,    // Go back to called
            ClinicQueueStatus.Cancelled
        },
        [ClinicQueueStatus.InProgress] = new()
        {
            ClinicQueueStatus.Completed,
            ClinicQueueStatus.Cancelled
        },
        // Terminal states — no transitions out
        [ClinicQueueStatus.Completed] = new(),
        [ClinicQueueStatus.Cancelled] = new()
    };

    /// <summary>
    /// Checks if a transition from currentStatus to newStatus is valid.
    /// </summary>
    public static bool IsValidTransition(ClinicQueueStatus currentStatus, ClinicQueueStatus newStatus)
    {
        // Same status is always valid (idempotent)
        if (currentStatus == newStatus) return true;

        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return false;

        return allowedStatuses.Contains(newStatus);
    }

    /// <summary>
    /// Gets all valid target statuses for a given current status.
    /// </summary>
    public static IEnumerable<ClinicQueueStatus> GetAllowedTransitions(ClinicQueueStatus currentStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return [];

        // Always include current status (idempotent)
        return allowedStatuses.Prepend(currentStatus);
    }
}
