namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Status of a cashier session (day box).
/// </summary>
public enum SessionStatus
{
    Open,       // وردية مفتوحة
    Closed,     // وردية مغلقة ومرحلة من الكاشير
    Reconciled  // وردية تمت مطابقتها واعتمادها محاسبياً
}
