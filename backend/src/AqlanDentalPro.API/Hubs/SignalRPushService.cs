using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace AqlanDentalPro.API.Hubs;

/// <summary>
/// تنفيذ خدمة الدفع الفوري باستخدام SignalR.
/// تُسجَّل في DI كـ Scoped لتتوافق مع دورة حياة الخدمات الأخرى.
/// </summary>
public class SignalRPushService(IHubContext<MessagingHub> hubContext) : IRealTimePushService
{
    public async Task PushToUserAsync(Guid userId, string eventName, object? payload = null)
    {
        await hubContext.Clients.Group($"user-{userId}").SendAsync(eventName, payload);
    }

    public async Task PushToRoleAsync(string role, string eventName, object? payload = null)
    {
        await hubContext.Clients.Group($"role-{role}").SendAsync(eventName, payload);
    }

    public async Task PushToConversationAsync(Guid conversationId, string eventName, object? payload = null)
    {
        await hubContext.Clients.Group($"conv-{conversationId}").SendAsync(eventName, payload);
    }

    public async Task PushToAllAsync(string eventName, object? payload = null)
    {
        await hubContext.Clients.All.SendAsync(eventName, payload);
    }

    public async Task PushToBranchAsync(Guid branchId, string eventName, object? payload = null)
    {
        await hubContext.Clients.Group($"branch-{branchId}").SendAsync(eventName, payload);
    }
}
