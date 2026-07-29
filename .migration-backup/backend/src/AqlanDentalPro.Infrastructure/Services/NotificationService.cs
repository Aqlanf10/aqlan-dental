using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public class NotificationService(AppDbContext db, IRealTimePushService pushService) : INotificationService
{
    public async Task NotifyAsync(Guid userId, string type, string title, string body,
        string? relatedEntity = null, Guid? relatedId = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            RelatedEntity = relatedEntity,
            RelatedId = relatedId,
            IsRead = false,
        });
        await db.SaveChangesAsync();

        // دفع الإشعار فوراً عبر SignalR
        await pushService.PushToUserAsync(userId, "NewNotification", new { type, title, body, relatedEntity, relatedId });
    }

    public async Task NotifyRoleAsync(string role, string type, string title, string body,
        string? relatedEntity = null, Guid? relatedId = null)
    {
        if (!Enum.TryParse<UserRole>(role, out var parsedRole)) return;

        var userIds = await db.Users
            .Where(u => u.Role == parsedRole && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var uid in userIds)
        {
            db.Notifications.Add(new Notification
            {
                UserId = uid,
                Type = type,
                Title = title,
                Body = body,
                RelatedEntity = relatedEntity,
                RelatedId = relatedId,
                IsRead = false,
            });
        }

        if (userIds.Count > 0)
        {
            await db.SaveChangesAsync();

            // دفع الإشعار لجميع المستخدمين بهذا الدور عبر SignalR
            await pushService.PushToRoleAsync(parsedRole.ToString(), "NewNotification", new { type, title, body, relatedEntity, relatedId });
        }
    }

    public async Task NotifyDoctorAsync(Guid doctorId, string type, string title, string body,
        string? relatedEntity = null, Guid? relatedId = null)
    {
        var doctor = await db.Doctors.FindAsync(doctorId);
        if (doctor == null) return;
        await NotifyAsync(doctor.UserId, type, title, body, relatedEntity, relatedId);
    }
}
