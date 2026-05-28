namespace AqlanDentalPro.Application.DTOs.Finance;

/// <summary>F5/H5: Request DTO for creating a standalone invoice.</summary>
public class CreateInvoiceRequest
{
    public Guid PatientId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? AppointmentId { get; set; }
    public List<CreateInvoiceLineItemRequest>? LineItems { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? Notes { get; set; }

    // ─── V4 إضافات التأمين والضرائب ───────────────────────────────
    /// <summary>نسبة الضريبة كنسبة مئوية (مثال: 5 تعني 5%). إن لم تُحدد، يُستخدم TaxAmount مباشرة.</summary>
    public decimal TaxPercentage { get; set; } = 0;

    /// <summary>معرّف شركة التأمين. إن لم يُحدد، الفاتورة نقدية (بدون تأمين).</summary>
    public Guid? InsuranceCompanyId { get; set; }

    /// <summary>نسبة التغطية التأمينية المخصصة لتجاوز النسبة الافتراضية للشركة (مثال: 80 تعني 80%).</summary>
    public decimal? CustomCoveragePercentage { get; set; }
}

public class CreateInvoiceLineItemRequest
{
    public Guid? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? RelatedTreatmentPlanStepId { get; set; }
    public Guid? RelatedVisitId { get; set; }
}

public class UpdateInvoiceRequest
{
    public List<UpdateInvoiceLineItemRequest>? LineItems { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? Notes { get; set; }
}

public class UpdateInvoiceLineItemRequest
{
    public Guid? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? RelatedTreatmentPlanStepId { get; set; }
    public Guid? RelatedVisitId { get; set; }
}

public class CancelInvoiceRequest
{
    public string? Notes { get; set; }
}

// ─── V4: Insurance & Settlement DTOs ───────────────────────────────

/// <summary>
/// بيانات المطالبة التأمينية المرتبطة بفاتورة.
/// </summary>
public class InsuranceClaimDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid InsuranceCompanyId { get; set; }
    public string? InsuranceCompanyName { get; set; }
    public Guid PatientId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CoveredAmount { get; set; }
    public decimal PatientCoPay { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
}

/// <summary>
/// طلب تسوية مطالبة تأمينية - يُستخدم عندما تقوم شركة التأمين
/// بتحويل المبلغ المستحق (شيك أو حوالة بنكية) إلى العيادة.
/// يتم تحديد الصندوق تلقائياً بناءً على فرع الفاتورة (تحويل بنكي).
/// </summary>
public class SettleInsuranceClaimRequest
{
    /// <summary>ملاحظات مرجعية (رقم الشيك، رقم الحوالة، إلخ).</summary>
    public string? ReferenceNotes { get; set; }
}
