using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = "StaffOnly")]
public class NotificationsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    // GET /api/notifications?unreadOnly=true&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var userId = currentUser.UserId;
        if (userId is null || userId == Guid.Empty) return Unauthorized();

        var query = db.Notifications
            .Where(n => n.UserId == userId);

        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync();
        var items = await query
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
                CreatedAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
            .ToListAsync();

        var unreadCount = await db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);

        return Ok(new { data = items, total, unreadCount, page, pageSize });
    }

    // GET /api/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = currentUser.UserId;
        if (userId is null || userId == Guid.Empty) return Unauthorized();

        var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return Ok(new { count });
    }

    // PUT /api/notifications/{id}/read
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = currentUser.UserId;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null) return NotFound();

        notification.IsRead = true;
        await db.SaveChangesAsync();
        return Ok(new { message = "تم تحديد الإشعار كمقروء" });
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = currentUser.UserId;
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        return Ok(new { message = "تم تحديد كل الإشعارات كمقروءة" });
    }

    // DELETE /api/notifications/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = currentUser.UserId;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is null) return NotFound();

        db.Notifications.Remove(notification);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الإشعار" });
    }

    // POST /api/notifications/seed-demo  — for testing
    [HttpPost("seed-demo")]
    public async Task<IActionResult> SeedDemo()
    {
        var userId = currentUser.UserId;
        if (userId is null || userId == Guid.Empty) return Unauthorized();

        var uid = userId.Value;
        var demo = new List<Notification>
        {
            new() { UserId = uid, Type = "appointment", Title = "موعد جديد", Body = "تم حجز موعد جديد لـ أحمد محمد الساعة 10:00 صباحاً", IsRead = false },
            new() { UserId = uid, Type = "payment", Title = "دفعة مستلمة", Body = "تم تسجيل دفعة بمبلغ 50,000 ر.ي من المريض سارة علي", IsRead = false },
            new() { UserId = uid, Type = "lab", Title = "طلب مختبر جاهز", Body = "الطبقة المتحركة للمريض خالد العمري جاهزة للاستلام", IsRead = false },
            new() { UserId = uid, Type = "system", Title = "تحديث النظام", Body = "تم تحديث النظام إلى الإصدار الجديد بنجاح", IsRead = true },
        };

        db.Notifications.AddRange(demo);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم إضافة إشعارات تجريبية", count = demo.Count });
    }
}
