namespace AqlanDentalPro.Application.Common;

/// <summary>
/// أسماء أحداث SignalR المشتركة بين طبقة التطبيق والبنية التحتية وطبقة API.
/// نُسكن هذه الثوابت في طبقة Application لأن واجهة <see cref="AqlanDentalPro.Application.Interfaces.Services.IRealTimePushService"/>
/// معرَّفة هنا أيضًا، ولأن بعض الخدمات في طبقة Infrastructure (مثل CheckoutService)
/// تحتاج إلى دفع الأحداث دون الاعتماد على طبقة API (المستوى الأعلى).
/// طبقة API تحتفظ بـ <c>MessagingHubEvents</c> كاسم بديل/مختصر لأحداث الطابور
/// والمراسلة فقط — أي حدث عام يجب أن يُضاف هنا.
/// </summary>
public static class RealTimeEvents
{
    /// <summary>تحديث رحلة المريض (وصول/طابور/زيارة/تحصيل/خروج) — للتحديث الفوري لشاشة العمليات اليومية</summary>
    public const string JourneyUpdated = "JourneyUpdated";
}
