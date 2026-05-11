namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Status values for clinic queue items.
/// Arabic display: في الانتظار، تم النداء، داخل الغرفة، قيد المعالجة، مكتمل، ملغي
/// </summary>
public enum ClinicQueueStatus
{
    Waiting,    // في الانتظار
    Called,     // تم النداء
    InRoom,     // داخل الغرفة
    InProgress, // قيد المعالجة
    Completed,  // مكتمل
    Cancelled   // ملغي
}
