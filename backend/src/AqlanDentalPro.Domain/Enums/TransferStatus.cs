namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Status of a liquidity transfer between vaults.
/// </summary>
public enum TransferStatus
{
    Pending,   // معلق قيد الاستلام والعد المادي
    Approved,  // تم تأكيد الاستلام المادي وتحديث الأرصدة والترحيل بنجاح
    Rejected   // مرفوض (إرجاع المبلغ للخزنة المصدر)
}
