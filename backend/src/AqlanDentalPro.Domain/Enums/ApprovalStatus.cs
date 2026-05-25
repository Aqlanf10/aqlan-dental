namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Approval lifecycle for operational expenses requiring managerial sign-off.
/// Expenses above a configured threshold are auto-placed in Pending until reviewed.
/// </summary>
public enum ApprovalStatus
{
    NotRequired,  // لا يحتاج موافقة — المبلغ أقل من الحد المالي
    Pending,      // قيد الاعتماد — في انتظار موافقة الإدارة
    Approved,     // معتمد — تمت الموافقة وتم ترحيله للأستاذ العام
    Rejected      // مرفوض — تم رفض الصرف وإلغاء المصروف
}
