using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// CON-01 FIX: Defines valid status transitions for clinic queue items.
/// Centralizes the transition rules that were previously scattered in the controller.
/// Prevents invalid state changes (e.g., jumping from Waiting directly to InProgress).
///
/// Transition map:
///   Waiting  → Called, InRoom, Cancelled
///   Called   → InRoom, Waiting, Cancelled
///   InRoom   → InProgress, Called, Cancelled
///   InProgress → Completed, Cancelled
///   Completed  → (terminal)
///   Cancelled  → (terminal)
///
/// NOTE: Called → InProgress was removed because it creates an asymmetry with
/// AppointmentStatusTransitions (which does not allow Called → InProgress).
/// This caused SyncAppointmentStatus to silently fail, leaving the appointment
/// status inconsistent with the queue status.
/// </summary>
public static class ClinicQueueStatusTransitions
{
    // Arabic labels for error messages — single source of truth
    private static readonly Dictionary<ClinicQueueStatus, string> StatusArabicLabels = new()
    {
        [ClinicQueueStatus.Waiting] = "في الانتظار",
        [ClinicQueueStatus.Called] = "تم النداء",
        [ClinicQueueStatus.InRoom] = "داخل الغرفة",
        [ClinicQueueStatus.InProgress] = "قيد المعالجة",
        [ClinicQueueStatus.Completed] = "مكتمل",
        [ClinicQueueStatus.Cancelled] = "ملغي"
    };

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

    /// <summary>
    /// Returns an Arabic error message for an invalid transition.
    /// Returns null if the transition is valid.
    /// CON-01 FIX: Centralized error message generation — controllers no longer need
    /// to construct their own Arabic messages for transition validation failures.
    /// </summary>
    public static string? GetValidationError(ClinicQueueStatus currentStatus, ClinicQueueStatus newStatus)
    {
        if (IsValidTransition(currentStatus, newStatus))
            return null;

        var currentLabel = StatusArabicLabels.GetValueOrDefault(currentStatus, currentStatus.ToString());
        var newLabel = StatusArabicLabels.GetValueOrDefault(newStatus, newStatus.ToString());

        return $"لا يمكن تغيير حالة الطابور من {currentLabel} إلى {newLabel}";
    }

    /// <summary>
    /// Returns the Arabic label for a given queue status.
    /// Useful for controllers that need to display status in responses.
    /// </summary>
    public static string GetArabicLabel(ClinicQueueStatus status)
    {
        return StatusArabicLabels.GetValueOrDefault(status, status.ToString());
    }
}
