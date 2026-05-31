namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Lifecycle status of a supplier bill (Accounts Payable entry).
/// </summary>
public enum BillStatus
{
    Unpaid,         // غير مدفوع — الفاتورة مستحقة بالكامل
    PartiallyPaid,  // مدفوع جزئياً — تم سداد جزء من الفاتورة
    FullyPaid,      // مدفوع بالكامل — صفر رصيد متبقي
    Cancelled       // ملغي — تم إلغاء الفاتورة
}
