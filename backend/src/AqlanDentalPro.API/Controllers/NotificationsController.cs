using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // GET /api/notifications
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var query = db.Notifications.Where(n => n.UserId == userId).AsQueryable();

        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Title,
                n.Body,
                n.IsRead,
                n.RelatedEntity,
                n.RelatedId,
                CreatedAt = n.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
            })
            .ToListAsync();

        var unreadCount = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        return Ok(new { notifications, unreadCount });
    }

    // PUT /api/notifications/{id}/read
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null) return NotFound(new { message = "الإشعار غير موجود" });

        notification.IsRead = true;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم قراءة الإشعار" });
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم قراءة جميع الإشعارات", count = unread.Count });
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(new { count });
    }
}
