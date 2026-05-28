using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Defines valid status transitions for clinic queue items.
/// Centralizes the transition rules that were previously scattered in the controller.
/// Prevents invalid state changes (e.g., jumping from Waiting directly to InProgress).
///
/// Transition map:
///   Waiting    → Called, InRoom, Cancelled, NoShow
///   Called     → InRoom, Waiting, Cancelled, NoShow
///   InRoom     → InProgress, Called, Cancelled
///   InProgress → Completed, Cancelled
///   Completed  → (terminal)
///   Cancelled  → (terminal)
///   NoShow     → (terminal)
///
/// NoShow: Added for patients who were called but did not arrive.
/// This distinguishes "patient didn't show" from "clinic cancelled the appointment".
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
        [ClinicQueueStatus.Cancelled] = "ملغي",
        [ClinicQueueStatus.NoShow] = "لم يحضر"
    };

    // Arabic labels for priority levels
    private static readonly Dictionary<ClinicQueuePriority, string> PriorityArabicLabels = new()
    {
        [ClinicQueuePriority.Normal] = "عادي",
        [ClinicQueuePriority.Urgent] = "عاجل",
        [ClinicQueuePriority.VIP] = "مميز",
        [ClinicQueuePriority.Emergency] = "طوارئ"
    };

    // Each status maps to the set of statuses it can transition TO.
    private static readonly Dictionary<ClinicQueueStatus, HashSet<ClinicQueueStatus>> ValidTransitions = new()
    {
        [ClinicQueueStatus.Waiting] = new()
        {
            ClinicQueueStatus.Called,
            ClinicQueueStatus.InRoom,    // Direct entry if business allows
            ClinicQueueStatus.Cancelled,
            ClinicQueueStatus.NoShow     // Patient didn't show up for scheduled slot
        },
        [ClinicQueueStatus.Called] = new()
        {
            ClinicQueueStatus.InRoom,
            ClinicQueueStatus.Waiting,   // Return to waiting
            ClinicQueueStatus.Cancelled,
            ClinicQueueStatus.NoShow     // Called but didn't respond
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
        [ClinicQueueStatus.Cancelled] = new(),
        [ClinicQueueStatus.NoShow] = new()
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

    /// <summary>
    /// Returns the Arabic label for a given priority level.
    /// </summary>
    public static string GetPriorityArabicLabel(ClinicQueuePriority priority)
    {
        return PriorityArabicLabels.GetValueOrDefault(priority, priority.ToString());
    }
}
