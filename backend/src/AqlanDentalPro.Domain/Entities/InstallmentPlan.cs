using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// خطة تقسيط - تجزئئ مبلغ العقد على أقساط شهرية مع دفعة مقدمة اختيارية.
/// يتم إنشاؤها عند إبرام عقد تقويم أو علاج طويل الأمد.
/// مستقبلاً سيرتبط النظام بإشعارات الواتساب للتذكير بالأقساط المستحقة.
/// </summary>
public class InstallmentPlan : BaseEntity
{
    /// <summary>العقد المرتبط بخطة التقسيط.</summary>
    public Guid ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    /// <summary>المريض صاحب خطة التقسيط.</summary>
    public Guid PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    /// <summary>إجمالي المبلغ المطلوب تقسيطه (بعد خصم الدفعة المقدمة).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>الدفعة المقدمة المدفوعة عند توقيع العقد (قد تكون صفراً).</summary>
    public decimal DownPayment { get; set; }

    /// <summary>عدد الأشهر/الأقساط المتفق عليها.</summary>
    public int NumberOfMonths { get; set; }

    /// <summary>مبلغ القسط الشهري = (TotalAmount - DownPayment) / NumberOfMonths.</summary>
    public decimal MonthlyAmount { get; set; }

    /// <summary>تاريخ بدء أول قسط.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>هل اكتملت خطة التقسيط (تم سداد جميع الأقساط).</summary>
    public bool IsCompleted { get; set; } = false;

    // Navigation properties

    /// <summary>الأقساط المجدولة ضمن هذه الخطة.</summary>
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
}

/// <summary>
/// قسط تقسيط - يمثل قسطاً واحداً ضمن خطة تقسيط.
/// يحتوي على تاريخ الاستحقاق وحالة السداد ويرتبط بسند القبض عند الدفع.
/// النظام سيقوم تلقائياً بتحديث حالة الأقساط المتأخرة وإرسال تذكيرات.
/// </summary>
public class Installment : BaseEntity
{
    /// <summary>خطة التقسيط التي ينتمي إليها هذا القسط.</summary>
    public Guid InstallmentPlanId { get; set; }
    public InstallmentPlan InstallmentPlan { get; set; } = null!;

    /// <summary>مبلغ القسط المستحق.</summary>
    public decimal Amount { get; set; }

    /// <summary>تاريخ استحقاق القسط (الموعد النهائي للسداد).</summary>
    public DateTime DueDate { get; set; }

    /// <summary>تاريخ السداد الفعلي (يُملأ عند دفع القسط).</summary>
    public DateTime? PaidDate { get; set; }

    /// <summary>حالة القسط (مستحق، مدفوع، متأخر).</summary>
    public InstallmentStatus Status { get; set; }

    /// <summary>سند القبض المرتبط بسداد هذا القسط (يُملأ عند الدفع).</summary>
    public Guid? PaymentId { get; set; }
    public Payment? Payment { get; set; }
}
