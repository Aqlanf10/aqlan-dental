using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// An invoice for a patient's clinical visit.
/// Created as Draft from checkout workflow — a financial preparation document,
/// not a completed payment. Actual payments are recorded via the Payments module.
/// Supports tax calculation, multi-currency, COGS tracking, and insurance linkage.
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

    /// <summary>Current lifecycle status of the invoice.</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    /// <summary>Sum of line item totals before discount/tax.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Discount amount applied (optional).</summary>
    public decimal? DiscountAmount { get; set; }

    // ─── الضرائب (Tax Support) ───

    /// <summary>نسبة الضريبة المطبقة (مثال: 15 تعني 15%).</summary>
    public decimal TaxPercentage { get; set; }

    /// <summary>قيمة الضريبة المحسوبة = Subtotal × TaxPercentage / 100.</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>Final total = Subtotal - DiscountAmount + TaxAmount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Additional notes (optional).</summary>
    public string? Notes { get; set; }

    /// <summary>User who created this invoice.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>User who last updated this invoice.</summary>
    public Guid? UpdatedBy { get; set; }

    // ─── تعدد العملات (Multi-Currency Support) ───

    /// <summary>العملة الافتراضية للفاتورة (مثال: YER, SAR, USD). القيمة الافتراضية الريال اليمني.</summary>
    public string Currency { get; set; } = "YER";

    /// <summary>سعر الصرف وقت إنشاء الفاتورة مقارنة بالعملة الأساسية.</summary>
    public decimal ExchangeRate { get; set; } = 1.0m;

    // ─── التكلفة الآلية COGS (Cost of Goods Sold) ───

    /// <summary>إجمالي تكلفة المواد المستخدمة من المخزون في هذه الفاتورة.</summary>
    public decimal TotalCostOfGoodsSold { get; set; }

    // ─── ربط التأمين (Insurance Linkage) ───

    /// <summary>المطالبة التأمينية المرتبطة بالفاتورة (اختياري - للمرضى المؤمن عليهم).</summary>
    public Guid? InsuranceClaimId { get; set; }
    public InsuranceClaim? InsuranceClaim { get; set; }

    // Navigation properties
    public Patient Patient { get; set; } = null!;
    public Visit? Visit { get; set; }
    public Appointment? Appointment { get; set; }
    public ICollection<InvoiceLineItem> LineItems { get; set; } = [];

    /// <summary>Payments linked to this invoice (via Payment.InvoiceId).</summary>
    public ICollection<Payment> Payments { get; set; } = [];
}
