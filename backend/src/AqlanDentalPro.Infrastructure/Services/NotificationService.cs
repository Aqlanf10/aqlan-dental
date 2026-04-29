using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public class NotificationService(AppDbContext db) : INotificationService
{
    public async Task NotifyUserAsync(Guid userId, string type, string title, string body, string? relatedEntity = null, Guid? relatedId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            RelatedEntity = relatedEntity,
            RelatedId = relatedId,
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
    }

    public async Task NotifyRoleAsync(string role, string type, string title, string body, string? relatedEntity = null, Guid? relatedId = null)
    {
        if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            return;

        var users = await db.Users
            .Where(u => u.Role == userRole && u.IsActive)
            .ToListAsync();

        foreach (var user in users)
        {
            db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = type,
                Title = title,
                Body = body,
                RelatedEntity = relatedEntity,
                RelatedId = relatedId,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task NotifyAllAsync(string type, string title, string body, string? relatedEntity = null, Guid? relatedId = null)
    {
        var users = await db.Users
            .Where(u => u.IsActive)
            .ToListAsync();

        foreach (var user in users)
        {
            db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = type,
                Title = title,
                Body = body,
                RelatedEntity = relatedEntity,
                RelatedId = relatedId,
            });
        }

        await db.SaveChangesAsync();
    }
}
