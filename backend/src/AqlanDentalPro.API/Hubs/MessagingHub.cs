using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AqlanDentalPro.API.Hubs;

/// <summary>
/// SignalR Hub للمراسلة والإشعارات الفورية.
/// يتيح الدفع الفوري للرسائل والإشعارات بدلاً من الاعتماد الكامل على HTTP polling.
/// </summary>
[Authorize]
public class MessagingHub(AppDbContext db, ILogger<MessagingHub> logger) : Hub
{
    /// <summary>
    /// عند الاتصال، يُضاف المستخدم تلقائياً إلى مجموعة تحمل معرفه.
    /// هذا يتيح إرسال رسائل مخصصة لكل مستخدم.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            logger.LogDebug("SignalR: User {UserId} connected with connection {ConnectionId}", userId, Context.ConnectionId);
        }

        // Also add to role groups for targeted notifications
        var user = await db.Users.FindAsync(userId);
        if (user != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role-{user.Role}");
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");

            var user = await db.Users.FindAsync(userId);
            if (user != null)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"role-{user.Role}");
            }
        }

        if (exception != null)
            logger.LogWarning(exception, "SignalR: User {UserId} disconnected with error", userId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// ينضم المستخدم إلى مجموعة المحادثة لاستقبال الرسائل الجديدة فورياً.
    /// </summary>
    public async Task JoinConversation(Guid conversationId)
    {
        var userId = GetUserId();
        if (userId == null) return;

        // تحقق من أن المستخدم مشارك في المحادثة
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);

        if (isParticipant)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
            logger.LogDebug("SignalR: User {UserId} joined conversation {ConvId}", userId, conversationId);
        }
    }

    /// <summary>
    /// يغادر المستخدم مجموعة المحادثة.
    /// </summary>
    public async Task LeaveConversation(Guid conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conv-{conversationId}");
    }

    private Guid? GetUserId()
    {
        // JWT uses ClaimTypes.NameIdentifier (mapped from "sub" claim) for user ID.
        // Must match CurrentUserService.UserId logic exactly.
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub");
        if (sub != null && Guid.TryParse(sub, out var id))
            return id;
        return null;
    }
}

/// <summary>
/// أنواع الرسائل التي يرسلها SignalR للعملاء.
/// </summary>
public static class MessagingHubEvents
{
    /// <summary>رسالة جديدة في محادثة</summary>
    public const string NewMessage = "NewMessage";

    /// <summary>رسالة معدّلة</summary>
    public const string MessageEdited = "MessageEdited";

    /// <summary>رسالة محذوفة</summary>
    public const string MessageDeleted = "MessageDeleted";

    /// <summary>تحديث عدد غير المقروء</summary>
    public const string UnreadCountUpdated = "UnreadCountUpdated";

    /// <summary>إشعار جديد</summary>
    public const string NewNotification = "NewNotification";

    /// <summary>تحديث قائمة المحادثات</summary>
    public const string ConversationsUpdated = "ConversationsUpdated";

    /// <summary>تغيير حالة عنصر في الطابور (إضافة/استدعاء/دخول/بدء/إكمال/إلغاء)</summary>
    public const string QueueUpdated = "QueueUpdated";

    /// <summary>استدعاء مريض — للتحديث الفوري على شاشة العرض</summary>
    public const string PatientCalled = "PatientCalled";
}
