using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// شركة التأمين - تمثل شركة تأمين تتعامل مع العيادة.
/// تحتوي على بيانات الاتصال ونسبة التغطية الافتراضية التي يتم تطبيقها
/// على المطالبات الجديدة تلقائياً عند إنشائها.
/// </summary>
public class InsuranceCompany : BaseEntity
{
    /// <summary>اسم شركة التأمين (مثال: التأمين الاجتماعي، أليانز، بوبا).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>البريد الإلكتروني للتواصل بشأن المطالبات.</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>رقم الهاتف للتواصل بشأن المطالبات.</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// نسبة تحمل التأمين الافتراضية (مثال: 0.80 تعني 80% تغطية تأمينية).
    /// يمكن تجاوزها في كل مطالبة على حدة.
    /// </summary>
    public decimal DefaultCoveragePercentage { get; set; }

    // Note: IsActive is inherited from BaseEntity and serves the same purpose.
    // No separate IsActive property needed here — BaseEntity.IsActive controls
    // whether the insurance company is active and available for new claims.

    // Navigation properties

    /// <summary>المطالبات المرتبطة بشركة التأمين هذه.</summary>
    public ICollection<InsuranceClaim> Claims { get; set; } = new List<InsuranceClaim>();
}

/// <summary>
/// مطالبة تأمينية - تمثل مطالبة مقدمة لشركة تأمين مقابل فاتورة علاج مريض.
/// تربط الفاتورة بشركة التأمين وتحسب المبلغ المغطى ونسبة تحمل المريض.
/// تتبع دورة حياة المطالبة من التقديم حتى السداد.
/// </summary>
public class InsuranceClaim : BaseEntity
{
    /// <summary>الفاتورة المرتبطة بهذه المطالبة التأمينية.</summary>
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    /// <summary>شركة التأمين التي قُدمت لها المطالبة.</summary>
    public Guid InsuranceCompanyId { get; set; }
    public InsuranceCompany InsuranceCompany { get; set; } = null!;

    /// <summary>المريض صاحب المطالبة.</summary>
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>إجمالي مبلغ الفاتورة قبل التأمين.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>مبلغ التأمين - المبلغ الذي ستتحمله شركة التأمين.</summary>
    public decimal CoveredAmount { get; set; }

    /// <summary>نسبة تحمل المريض - المبلغ الذي يدفعه المريض من جيبه.</summary>
    public decimal PatientCoPay { get; set; }

    /// <summary>حالة المطالبة الحالية (قيد الانتظار، موافق عليها، مرفوضة، تم السداد).</summary>
    public ClaimStatus Status { get; set; }

    /// <summary>سبب الرفض إن وُجد (يُملأ فقط عندما تكون الحالة مرفوضة).</summary>
    public string? RejectionReason { get; set; }
}
