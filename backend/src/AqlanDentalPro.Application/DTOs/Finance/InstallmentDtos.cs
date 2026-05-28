using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.DTOs.Finance;

/// <summary>
/// طلب إنشاء خطة تقسيط لعقد تقويم.
/// يقوم النظام تلقائياً بتوزيع المبلغ المتبقي على الأقساط الشهرية.
/// </summary>
public class CreateInstallmentPlanRequest
{
    /// <summary>معرّف العقد المراد إنشاء خطة تقسيط له.</summary>
    public Guid ContractId { get; set; }

    /// <summary>الدفعة المقدمة المدفوعة عند توقيع العقد (قد تكون صفراً).</summary>
    public decimal DownPayment { get; set; }

    /// <summary>عدد أشهر التقسيط.</summary>
    public int NumberOfMonths { get; set; }

    /// <summary>تاريخ بدء أول قسط (عادة تاريخ توقيع العقد أو بداية الشهر التالي).</summary>
    public DateTime StartDate { get; set; }
}

/// <summary>
/// بيانات خطة التقسيط الكاملة مع الأقساط المجدولة.
/// </summary>
public class InstallmentPlanDto
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid PatientId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public int NumberOfMonths { get; set; }
    public decimal MonthlyAmount { get; set; }
    public DateTime StartDate { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>الأقساط المجدولة مرتبة حسب تاريخ الاستحقاق.</summary>
    public List<InstallmentDto> Installments { get; set; } = new();
}

/// <summary>
/// بيانات قسط واحد ضمن خطة التقسيط.
/// </summary>
public class InstallmentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public InstallmentStatus Status { get; set; }

    /// <summary>معرّف سند القبض المرتبط بسداد هذا القسط (إذا تم السداد).</summary>
    public Guid? PaymentId { get; set; }
}

/// <summary>
/// طلب سداد قسط تقسيط.
/// يتضمن طريقة الدفع والصندوق المستلم وملاحظات اختيارية.
/// النظام يطبق قفلاً تزامنياً (Transaction) لمنع السداد المزدوج.
/// </summary>
public class PayInstallmentRequest
{
    /// <summary>طريقة الدفع (cash, card, bank_transfer).</summary>
    public string PaymentMethod { get; set; } = "cash";

    /// <summary>ملاحظات اختيارية على عملية السداد.</summary>
    public string? Notes { get; set; }
}
