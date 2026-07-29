namespace AqlanDentalPro.Domain.Enums;

/// <summary>M2 FIX: SurgeryCase status enum — replaces magic string "scheduled"/"in_progress"/"completed"/"cancelled"/"postponed".</summary>
public enum SurgeryCaseStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    Postponed = 4
}
