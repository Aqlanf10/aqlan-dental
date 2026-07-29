namespace AqlanDentalPro.Domain.Enums;

public enum RequestStatus
{
    Pending = 0,     // قيد المراجعة
    Approved = 1,    // موافق عليه
    Rejected = 2,    // مرفوض
    Cancelled = 3    // ملغي
}
