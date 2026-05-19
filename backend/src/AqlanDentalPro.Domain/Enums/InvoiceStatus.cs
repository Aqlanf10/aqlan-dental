namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Status of an invoice in its lifecycle.
/// Draft → Issued → Paid (via external Payments module).
/// Draft → Cancelled is also allowed.
/// IMPORTANT: This sprint does NOT auto-set Paid status.
/// Payments module remains the source of truth for actual payment records.
/// </summary>
public enum InvoiceStatus
{
    Draft,
    Issued,
    Cancelled,
    Paid
}
