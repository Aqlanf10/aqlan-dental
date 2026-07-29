namespace AqlanDentalPro.Domain.Enums;

/// <summary>M2 FIX: OrthoCase status enum — replaces magic string "active"/"completed"/"on_hold"/"cancelled".</summary>
public enum OrthoCaseStatus
{
    Active = 0,
    Completed = 1,
    OnHold = 2,
    Cancelled = 3
}
