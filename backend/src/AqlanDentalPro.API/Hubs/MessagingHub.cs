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
/// يسمح بالاتصال المجهول لشاشات العرض (clinic-display) التي تحتاج فقط
/// لاستقبال أحداث الطابور. الطرق المحمية (JoinConversation, LeaveConversation)
/// تتطلب مصادقة صالحة.
/// </summary>
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

            // Branch isolation: add to branch group so queue events are scoped per branch
            // Admin sees all branches (no branch group), staff only see their branch
            if (user.BranchId.HasValue && user.BranchId.Value != Guid.Empty)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"branch-{user.BranchId.Value}");
                logger.LogDebug("SignalR: User {UserId} added to branch group {BranchId}", userId, user.BranchId.Value);
            }
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

                // Remove from branch group
                if (user.BranchId.HasValue && user.BranchId.Value != Guid.Empty)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"branch-{user.BranchId.Value}");
                }
            }
        }

        if (exception != null)
            logger.LogWarning(exception, "SignalR: User {UserId} disconnected with error", userId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// ينضم المستخدم إلى مجموعة المحادثة لاستقبال الرسائل الجديدة فورياً.
    /// يتطلب مصادقة صالحة.
    /// </summary>
    [Authorize]
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
    /// يغادر المستخدم مجموعة المحادثة. يتطلب مصادقة صالحة.
    /// </summary>
    [Authorize]
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

    /// <summary>تغيير حالة عنصر في الطابور (إضافة/استدعاء/دخول/بدء/إكمال/إلغاء/لم يحضر/أولوية/ترتيب)</summary>
    public const string QueueUpdated = "QueueUpdated";

    /// <summary>استدعاء مريض — للتحديث الفوري على شاشة العرض</summary>
    public const string PatientCalled = "PatientCalled";

    /// <summary>تغيير أولوية عنصر في الطابور</summary>
    public const string QueuePriorityChanged = "QueuePriorityChanged";

    /// <summary>إعادة ترتيب الطابور (سحب وإفلات)</summary>
    public const string QueueReordered = "QueueReordered";
}
