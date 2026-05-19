using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Hubs;

/// <summary>
/// تنفيذ خدمة الدفع الفوري باستخدام SignalR.
/// تُسجَّل في DI كـ Scoped لتتوافق مع دورة حياة الخدمات الأخرى.
/// </summary>
public class SignalRPushService(IHubContext<MessagingHub> hubContext, AppDbContext db) : IRealTimePushService
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
}
