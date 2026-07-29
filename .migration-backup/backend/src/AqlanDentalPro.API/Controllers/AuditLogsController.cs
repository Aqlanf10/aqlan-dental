using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = "AdminOnly")]
public class AuditLogsController(AppDbContext db) : ControllerBase
{
    // GET /api/audit-logs?page=1&pageSize=50&userId=&resource=&action=&from=&to=
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? userId,
        [FromQuery] string? resource,
        [FromQuery] string? action,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var query = db.AuditLogs.Include(a => a.User).AsQueryable();

        if (userId.HasValue) query = query.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(resource)) query = query.Where(a => a.Resource == resource);
        if (!string.IsNullOrWhiteSpace(action) && Enum.TryParse<Domain.Enums.AuditAction>(action, out var parsedAction))
            query = query.Where(a => a.Action == parsedAction);

        if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var fromDate))
            query = query.Where(a => a.CreatedAt >= fromDate);
        if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var toDate))
            query = query.Where(a => a.CreatedAt <= toDate.AddDays(1));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                Action = a.Action.ToString(),
                a.Resource,
                a.ResourceId,
                a.IpAddress,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Username = a.User != null ? a.User.Username : "النظام",
                UserId = a.UserId
            })
            .ToListAsync();

        // Distinct resources for filter dropdown
        var resources = await db.AuditLogs
            .Select(a => a.Resource)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync();

        return Ok(new { data = items, total, page, pageSize, resources });
    }
}
