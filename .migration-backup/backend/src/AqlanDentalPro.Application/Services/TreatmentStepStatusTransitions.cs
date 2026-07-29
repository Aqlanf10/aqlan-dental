using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// CLIN-16 FIX: Extracted from TreatmentPlanController to centralize treatment-step
/// status transition rules and Arabic labels — mirrors AppointmentStatusTransitions
/// and SurgeryCaseStatusTransitions. Previously these lived as private static methods
/// in the controller, making them untestable in isolation.
///
/// Transition map:
///   Planned    → any (flexible — can start, defer, or cancel)
///   InProgress → any (can complete, defer, or cancel)
///   Completed  → Deferred only (completed steps can be deferred if revisited, but not re-planned)
///   Cancelled  → Planned only (cancelled steps can be re-planned)
///   Deferred   → any (deferred steps can be resumed, completed, or cancelled)
/// </summary>
public static class TreatmentStepStatusTransitions
{
    private static readonly Dictionary<TreatmentStepStatus, string> StatusArabicLabels = new()
    {
        [TreatmentStepStatus.Planned]    = "مخطط",
        [TreatmentStepStatus.InProgress] = "قيد التنفيذ",
        [TreatmentStepStatus.Completed]  = "مكتمل",
        [TreatmentStepStatus.Cancelled]  = "ملغي",
        [TreatmentStepStatus.Deferred]   = "مؤجل",
    };

    private static readonly Dictionary<TreatmentStepPriority, string> PriorityArabicLabels = new()
    {
        [TreatmentStepPriority.Low]    = "منخفض",
        [TreatmentStepPriority.Normal] = "عادي",
        [TreatmentStepPriority.High]   = "مرتفع",
        [TreatmentStepPriority.Urgent] = "عاجل",
    };

    public static bool IsValidTransition(TreatmentStepStatus current, TreatmentStepStatus target)
    {
        if (current == target) return true; // idempotent

        return current switch
        {
            TreatmentStepStatus.Planned    => true,
            TreatmentStepStatus.InProgress => true,
            TreatmentStepStatus.Completed  => target == TreatmentStepStatus.Deferred,
            TreatmentStepStatus.Cancelled  => target == TreatmentStepStatus.Planned,
            TreatmentStepStatus.Deferred   => true,
            _ => false,
        };
    }

    public static string? GetValidationError(TreatmentStepStatus current, TreatmentStepStatus target)
    {
        if (IsValidTransition(current, target))
            return null;

        var currentLabel = StatusArabicLabels.GetValueOrDefault(current, current.ToString());
        var targetLabel = StatusArabicLabels.GetValueOrDefault(target, target.ToString());

        return $"لا يمكن تغيير الحالة من {currentLabel} إلى {targetLabel}";
    }

    public static string GetStatusArabic(TreatmentStepStatus status)
        => StatusArabicLabels.GetValueOrDefault(status, status.ToString());

    public static string GetPriorityArabic(TreatmentStepPriority priority)
        => PriorityArabicLabels.GetValueOrDefault(priority, priority.ToString());
}
