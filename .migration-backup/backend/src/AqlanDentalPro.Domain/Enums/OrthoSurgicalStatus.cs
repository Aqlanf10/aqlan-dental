namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Workflow state of a shared Ortho-Surgical (orthognathic) planning case — the
/// collaboration lifecycle between the orthodontist and the oral surgeon.
///
/// This is DELIBERATELY separate from <see cref="SurgeryCaseStatus"/>: that enum
/// tracks execution of the actual operation (Scheduled/InProgress/Completed...),
/// whereas this enum tracks the joint PLANNING and dual-approval workflow that
/// precedes (and follows) the operation. The link to the real operation is the
/// <c>OrthoSurgicalCase.SurgeryCaseId</c> created when entering SurgeryScheduled.
/// Stored as string in the DB (HasConversion&lt;string&gt;) for readability/compat.
/// </summary>
public enum OrthoSurgicalStatus
{
    DraftByOrthodontist = 0,
    RecordsIncomplete = 1,
    CephReady = 2,
    VtoDraft = 3,
    SentToSurgeon = 4,
    SurgeonReviewPending = 5,
    SurgeonRequestedChanges = 6,
    JointPlanApproved = 7,
    ReadyForSurgery = 8,
    SurgeryScheduled = 9,
    SurgeryDone = 10,
    PostOpOrthodontics = 11,
    Completed = 12,
    // Terminal "not a surgical candidate" outcome (case stays orthodontic-only).
    NotSurgicalCandidate = 13,
    Cancelled = 14
}
