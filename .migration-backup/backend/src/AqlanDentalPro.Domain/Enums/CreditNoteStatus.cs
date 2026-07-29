namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Lifecycle status of a Credit Note (إشعار دائن).
/// Draft → Approved → Refunded (money returned to patient).
/// </summary>
public enum CreditNoteStatus
{
    Draft,      // مسودة — Created but not yet approved
    Approved,   // معتمد — Approved by accountant/admin, awaiting refund
    Refunded    // مسترد — Refund payment has been processed
}
