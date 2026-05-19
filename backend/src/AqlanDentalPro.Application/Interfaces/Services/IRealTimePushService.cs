namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// خدمة الدفع الفوري — تُستخدم لإرسال الأحداث للعملاء عبر SignalR.
/// يتم تنفيذها في طبقة API باستخدام SignalR Hub.
/// </summary>
public interface IRealTimePushService
{
    /// <summary>إرسال حدث لمستخدم محدد</summary>
    Task PushToUserAsync(Guid userId, string eventName, object? payload = null);

    /// <summary>إرسال حدث لجميع المستخدمين بدور محدد</summary>
    Task PushToRoleAsync(string role, string eventName, object? payload = null);

    /// <summary>إرسال حدث لجميع المشاركين في محادثة</summary>
    Task PushToConversationAsync(Guid conversationId, string eventName, object? payload = null);
}
