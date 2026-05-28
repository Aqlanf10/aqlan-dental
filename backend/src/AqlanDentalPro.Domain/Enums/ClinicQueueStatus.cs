namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Status values for clinic queue items.
/// Arabic display: في الانتظار، تم النداء، داخل الغرفة، قيد المعالجة، مكتمل، ملغي، لم يحضر
/// </summary>
public enum ClinicQueueStatus
{
    Waiting,    // في الانتظار
    Called,     // تم النداء
    InRoom,     // داخل الغرفة
    InProgress, // قيد المعالجة
    Completed,  // مكتمل
    Cancelled,  // ملغي
    NoShow      // لم يحضر
}

/// <summary>
/// Priority levels for clinic queue items.
/// Higher priority items appear first in the queue.
/// </summary>
public enum ClinicQueuePriority
{
    Normal = 0,    // عادي
    Urgent = 1,    // عاجل
    VIP = 2,       // مميز
    Emergency = 3  // طوارئ
}
