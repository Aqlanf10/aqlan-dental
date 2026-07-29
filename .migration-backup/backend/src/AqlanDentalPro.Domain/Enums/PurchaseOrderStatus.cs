namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Status of a purchase order in its lifecycle.
/// Draft → Submitted → PartiallyReceived / Received.
/// Draft/Submitted → Cancelled is also allowed.
/// </summary>
public enum PurchaseOrderStatus
{
    Draft = 0,
    Submitted = 1,
    PartiallyReceived = 2,
    Received = 3,
    Cancelled = 4
}
