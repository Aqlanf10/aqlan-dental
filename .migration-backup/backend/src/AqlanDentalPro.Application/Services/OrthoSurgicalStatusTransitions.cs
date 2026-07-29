using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Valid status transitions for a shared Ortho-Surgical (orthognathic) planning case.
/// Mirrors the pattern of <see cref="SurgeryCaseStatusTransitions"/> — a deterministic,
/// testable transition map that prevents invalid workflow jumps (e.g. a case cannot go
/// from Completed back to DraftByOrthodontist, nor be approved for surgery while the
/// surgeon review is still pending).
///
/// The workflow is a collaboration between the orthodontist (owns diagnosis, ceph, VTO,
/// orthodontic prep) and the oral surgeon (reviews, proposes the procedure, executes).
/// It converges on a dual-approved JointPlan before ReadyForSurgery.
/// </summary>
public static class OrthoSurgicalStatusTransitions
{
    // Arabic labels — single source of truth. Feminine form to match "حالة" (case is feminine).
    private static readonly Dictionary<OrthoSurgicalStatus, string> StatusArabicLabels = new()
    {
        [OrthoSurgicalStatus.DraftByOrthodontist]     = "مسودة لدى التقويم",
        [OrthoSurgicalStatus.RecordsIncomplete]       = "السجلات ناقصة",
        [OrthoSurgicalStatus.CephReady]               = "السيفالو جاهز",
        [OrthoSurgicalStatus.VtoDraft]                = "مسودة المحاكاة (VTO)",
        [OrthoSurgicalStatus.SentToSurgeon]           = "أُرسلت للجراح",
        [OrthoSurgicalStatus.SurgeonReviewPending]    = "قيد مراجعة الجراح",
        [OrthoSurgicalStatus.SurgeonRequestedChanges] = "الجراح طلب تعديلًا",
        [OrthoSurgicalStatus.JointPlanApproved]       = "الخطة المشتركة معتمدة",
        [OrthoSurgicalStatus.ReadyForSurgery]         = "جاهزة للجراحة",
        [OrthoSurgicalStatus.SurgeryScheduled]        = "الجراحة مجدولة",
        [OrthoSurgicalStatus.SurgeryDone]             = "تمت الجراحة",
        [OrthoSurgicalStatus.PostOpOrthodontics]      = "تقويم ما بعد الجراحة",
        [OrthoSurgicalStatus.Completed]               = "مكتملة",
        [OrthoSurgicalStatus.NotSurgicalCandidate]    = "غير مرشّحة للجراحة",
        [OrthoSurgicalStatus.Cancelled]               = "ملغاة",
    };

    // Cancellation is allowed from any non-terminal state; added to each set below.
    private static readonly Dictionary<OrthoSurgicalStatus, HashSet<OrthoSurgicalStatus>> ValidTransitions = new()
    {
        [OrthoSurgicalStatus.DraftByOrthodontist] = new()
        {
            OrthoSurgicalStatus.RecordsIncomplete,
            OrthoSurgicalStatus.CephReady,
            OrthoSurgicalStatus.SentToSurgeon,
            OrthoSurgicalStatus.NotSurgicalCandidate,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.RecordsIncomplete] = new()
        {
            OrthoSurgicalStatus.DraftByOrthodontist,
            OrthoSurgicalStatus.CephReady,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.CephReady] = new()
        {
            OrthoSurgicalStatus.DraftByOrthodontist,
            OrthoSurgicalStatus.VtoDraft,
            OrthoSurgicalStatus.SentToSurgeon,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.VtoDraft] = new()
        {
            OrthoSurgicalStatus.CephReady,
            OrthoSurgicalStatus.SentToSurgeon,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.SentToSurgeon] = new()
        {
            OrthoSurgicalStatus.SurgeonReviewPending,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.SurgeonReviewPending] = new()
        {
            OrthoSurgicalStatus.SurgeonRequestedChanges,
            OrthoSurgicalStatus.JointPlanApproved,
            OrthoSurgicalStatus.NotSurgicalCandidate,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.SurgeonRequestedChanges] = new()
        {
            OrthoSurgicalStatus.DraftByOrthodontist,
            OrthoSurgicalStatus.CephReady,
            OrthoSurgicalStatus.SentToSurgeon,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.JointPlanApproved] = new()
        {
            OrthoSurgicalStatus.ReadyForSurgery,
            OrthoSurgicalStatus.SurgeonRequestedChanges,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.ReadyForSurgery] = new()
        {
            OrthoSurgicalStatus.SurgeryScheduled,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.SurgeryScheduled] = new()
        {
            OrthoSurgicalStatus.SurgeryDone,
            OrthoSurgicalStatus.Cancelled,
        },
        [OrthoSurgicalStatus.SurgeryDone] = new()
        {
            OrthoSurgicalStatus.PostOpOrthodontics,
            OrthoSurgicalStatus.Completed,
        },
        [OrthoSurgicalStatus.PostOpOrthodontics] = new()
        {
            OrthoSurgicalStatus.Completed,
        },
        // Terminal states — no transitions out.
        [OrthoSurgicalStatus.Completed] = new(),
        [OrthoSurgicalStatus.NotSurgicalCandidate] = new(),
        [OrthoSurgicalStatus.Cancelled] = new(),
    };

    /// <summary>Checks whether a transition is valid. Same-status is always valid (idempotent).</summary>
    public static bool IsValidTransition(OrthoSurgicalStatus currentStatus, OrthoSurgicalStatus newStatus)
    {
        if (currentStatus == newStatus) return true;
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowed)) return false;
        return allowed.Contains(newStatus);
    }

    /// <summary>All valid target statuses for a given current status (including itself for idempotency).</summary>
    public static IEnumerable<OrthoSurgicalStatus> GetAllowedTransitions(OrthoSurgicalStatus currentStatus)
    {
        if (!ValidTransitions.TryGetValue(currentStatus, out var allowed)) return [currentStatus];
        return allowed.Prepend(currentStatus);
    }

    /// <summary>Arabic error message for an invalid transition, or null when the transition is valid.</summary>
    public static string? GetValidationError(OrthoSurgicalStatus currentStatus, OrthoSurgicalStatus newStatus)
    {
        if (IsValidTransition(currentStatus, newStatus)) return null;
        var currentLabel = StatusArabicLabels.GetValueOrDefault(currentStatus, currentStatus.ToString());
        var newLabel = StatusArabicLabels.GetValueOrDefault(newStatus, newStatus.ToString());
        return $"لا يمكن تغيير حالة التخطيط التقويمي الجراحي من {currentLabel} إلى {newLabel}";
    }

    /// <summary>Arabic label for a status.</summary>
    public static string GetArabicLabel(OrthoSurgicalStatus status)
        => StatusArabicLabels.GetValueOrDefault(status, status.ToString());
}
