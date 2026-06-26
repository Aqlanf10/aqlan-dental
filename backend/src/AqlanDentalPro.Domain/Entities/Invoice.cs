using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// An invoice for a patient's clinical visit.
/// Created as Draft from checkout workflow — a financial preparation document,
/// not a completed payment. Actual payments are recorded via the Payments module.
/// </summary>
public class Invoice : BaseEntity
{
    public Guid PatientId { get; set; }

    /// <summary>Linked visit (optional — invoice may be standalone).</summary>
    public Guid? VisitId { get; set; }

    /// <summary>Linked appointment (optional — carried from visit).</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>Auto-generated invoice number (e.g. INV-20260531-001).</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Currency of the invoice account amount (YER, SAR, USD).</summary>
    public string Currency { get; set; } = "YER";

    /// <summary>Current lifecycle status of the invoice.</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Sum of line item totals before discount/tax.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Discount amount applied (optional).</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>Tax amount (optional — for future use).</summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>Final total = Subtotal - DiscountAmount + TaxAmount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Additional notes (optional).</summary>
    public string? Notes { get; set; }

    /// <summary>User who created this invoice.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>User who last updated this invoice.</summary>
    public Guid? UpdatedBy { get; set; }

    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public Visit? Visit { get; set; }
    public Appointment? Appointment { get; set; }
    public ICollection<InvoiceLineItem> LineItems { get; set; } = [];

    /// <summary>Payments linked to this invoice (via Payment.InvoiceId).</summary>
    public ICollection<Payment> Payments { get; set; } = [];
}
