using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Defines valid status transitions for surgery cases.
/// Mirrors the pattern of AppointmentStatusTransitions / ClinicQueueStatusTransitions.
/// Prevents invalid state changes (e.g., jumping from Completed back to Scheduled,
/// or from Cancelled to InProgress).
///
/// CLIN-03 FIX: Previously SurgeryController.UpdateStatus parsed the enum and saved
/// directly with no transition validation — a surgery case could jump from Completed
/// back to Scheduled, or from Cancelled to InProgress, corrupting the audit trail and
/// leaving postop reports attached to a "scheduled" case.
///
/// Transition map:
///   Scheduled   → InProgress, Cancelled, Postponed
///   InProgress  → Completed, Cancelled
///   Completed   → (terminal)
///   Cancelled   → (terminal)
///   Postponed   → Scheduled, Cancelled
///
/// Rationale:
///   - Scheduled → InProgress: doctor starts the surgery.
///   - Scheduled → Cancelled/Postponed: surgery cancelled or postponed before starting.
///   - InProgress → Completed: surgery finishes successfully.
///   - InProgress → Cancelled: surgery aborted (medical complication, patient decision).
///   - InProgress → Postponed is NOT allowed: once a surgery has started, it must be
///     completed or cancelled (not "postponed" — that's clinically meaningless).
///   - Postponed → Scheduled: the postponed surgery is rescheduled to a new date.
///   - Postponed → Cancelled: the postponed surgery is cancelled outright.
///   - Completed/Cancelled are terminal: the historical record must not be reopened.
///     If a surgery was mistakenly marked Completed, it should be corrected via a
///     separate correction/credit-note flow, not by reverting the status.
/// </summary>
public static class SurgeryCaseStatusTransitions
{
    // Arabic labels for surgery statuses — single source of truth.
    // Feminine form to match "حالة جراحية" (surgery case is feminine in Arabic).
    private static readonly Dictionary<SurgeryCaseStatus, string> StatusArabicLabels = new()
    {
        [SurgeryCaseStatus.Scheduled] = "مقررة",
        [SurgeryCaseStatus.InProgress] = "قيد التنفيذ",
        [SurgeryCaseStatus.Completed] = "مكتملة",
        [SurgeryCaseStatus.Cancelled] = "ملغاة",
        [SurgeryCaseStatus.Postponed] = "مؤجلة"
    };

    // Each status maps to the set of statuses it can transition TO.
    private static readonly Dictionary<SurgeryCaseStatus, HashSet<SurgeryCaseStatus>> ValidTransitions = new()
    {
        [SurgeryCaseStatus.Scheduled] = new()
        {
            SurgeryCaseStatus.InProgress,
            SurgeryCaseStatus.Cancelled,
            SurgeryCaseStatus.Postponed
        },
        [SurgeryCaseStatus.InProgress] = new()
        {
            SurgeryCaseStatus.Completed,
            SurgeryCaseStatus.Cancelled
            // Postponed intentionally excluded — see XML doc above.
        },
        // Terminal states — no transitions out
        [SurgeryCaseStatus.Completed] = new(),
        [SurgeryCaseStatus.Cancelled] = new(),
        [SurgeryCaseStatus.Postponed] = new()
        {
            SurgeryCaseStatus.Scheduled,   // Reschedule to a new date
            SurgeryCaseStatus.Cancelled    // Cancel the postponed surgery outright
        }
    };

    /// <summary>
    /// Checks if a transition from currentStatus to newStatus is valid.
    /// Same-status is always valid (idempotent).
    /// </summary>
    public static bool IsValidTransition(SurgeryCaseStatus currentStatus, SurgeryCaseStatus newStatus)
    {
        if (currentStatus == newStatus) return true;

        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return false;

        return allowedStatuses.Contains(newStatus);
    }

    /// <summary>
    /// Gets all valid target statuses for a given current status (including itself for idempotency).
    /// </summary>
    public static IEnumerable<SurgeryCaseStatus> GetAllowedTransitions(SurgeryCaseStatus currentStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowedStatuses))
            return [currentStatus];

        return allowedStatuses.Prepend(currentStatus);
    }

    /// <summary>
    /// Returns an Arabic error message for an invalid transition.
    /// Returns null if the transition is valid.
    /// </summary>
    public static string? GetValidationError(SurgeryCaseStatus currentStatus, SurgeryCaseStatus newStatus)
    {
        if (IsValidTransition(currentStatus, newStatus))
            return null;

        var currentLabel = StatusArabicLabels.GetValueOrDefault(currentStatus, currentStatus.ToString());
        var newLabel = StatusArabicLabels.GetValueOrDefault(newStatus, newStatus.ToString());

        return $"لا يمكن تغيير حالة الجراحة من {currentLabel} إلى {newLabel}";
    }

    /// <summary>
    /// Returns the Arabic label for a given surgery status.
    /// </summary>
    public static string GetArabicLabel(SurgeryCaseStatus status)
    {
        return StatusArabicLabels.GetValueOrDefault(status, status.ToString());
    }
}
