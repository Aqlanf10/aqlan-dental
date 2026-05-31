using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A Credit Note (إشعار دائن) issued against a patient invoice.
/// Represents a return/adjustment that reduces the invoice amount.
/// Must be approved before a refund payment can be processed.
/// </summary>
public class CreditNote : BaseEntity
{
    /// <summary>The invoice this credit note is issued against.</summary>
    public Guid InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    /// <summary>The patient who receives the credit.</summary>
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Amount to be credited/refunded (YER).</summary>
    public decimal Amount { get; set; }

    /// <summary>Reason for the credit note (e.g., "مرتجع مواد", "خصم إضافي").</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Current lifecycle status of the credit note.</summary>
    public CreditNoteStatus Status { get; set; } = CreditNoteStatus.Draft;

    /// <summary>Once refunded, links to the refund Payment record.</summary>
    public Guid? RefundPaymentId { get; set; }
    public Payment? RefundPayment { get; set; }

    /// <summary>Branch this credit note belongs to.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>User who created this credit note.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Optional additional notes.</summary>
    public string? Notes { get; set; }
}
