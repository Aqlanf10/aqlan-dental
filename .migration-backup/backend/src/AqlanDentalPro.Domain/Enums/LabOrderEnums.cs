namespace AqlanDentalPro.Domain.Enums;

/// <summary>Professional lab order workflow statuses.</summary>
public enum LabOrderStatus
{
    Draft = 0,
    Sent = 1,
    Manufacturing = 2,
    TryIn = 3,
    Ready = 4,
    Received = 5,
    Delivered = 6,
    Returned = 7,
    Remake = 8,
    Cancelled = 9
}

/// <summary>M2 FIX: LabOrder priority enum — replaces magic string "urgent"/"normal"/"low".</summary>
public enum LabOrderPriority
{
    Urgent = 0,
    Normal = 1,
    Low = 2
}
