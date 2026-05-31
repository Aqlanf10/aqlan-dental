namespace AqlanDentalPro.Domain.Enums;

/// <summary>M2 FIX: LabOrder status enum — replaces magic string "sent"/"manufacturing"/"ready"/"received"/"cancelled".</summary>
public enum LabOrderStatus
{
    Sent = 0,
    Manufacturing = 1,
    Ready = 2,
    Received = 3,
    Cancelled = 4
}

/// <summary>M2 FIX: LabOrder priority enum — replaces magic string "urgent"/"normal"/"low".</summary>
public enum LabOrderPriority
{
    Urgent = 0,
    Normal = 1,
    Low = 2
}
