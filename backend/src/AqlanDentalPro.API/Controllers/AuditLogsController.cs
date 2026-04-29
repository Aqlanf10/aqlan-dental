using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = "AdminOnly")]
public class AuditLogsController(AppDbContext db) : ControllerBase
{
    // GET /api/audit-logs
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? action = null,
        [FromQuery] string? resource = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? search = null,
        [FromQuery] string? export = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.AuditLogs.AsQueryable();

        // Filter by action
        if (!string.IsNullOrWhiteSpace(action) && Enum.TryParse<AuditAction>(action, true, out var auditAction))
            query = query.Where(a => a.Action == auditAction);

        // Filter by resource
        if (!string.IsNullOrWhiteSpace(resource))
            query = query.Where(a => a.Resource == resource);

        // Filter by userId
        if (userId.HasValue)
            query = query.Where(a => a.UserId == userId.Value);

        // Date range
        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value);

        // Search in Resource
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Resource.Contains(search));

        // If CSV export requested
        if (export == "csv")
        {
            var allItems = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    Username = a.User != null ? a.User.Username : (string?)null,
                    Action = a.Action.ToString(),
                    a.Resource,
                    a.ResourceId,
                    a.IpAddress,
                    CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("التاريخ,المستخدم,الإجراء,المورد,معرف المورد,عنوان IP");
            foreach (var item in allItems)
            {
                csv.AppendLine($"{item.CreatedAt},{item.Username},{item.Action},{item.Resource},{item.ResourceId},{item.IpAddress}");
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"audit-logs-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                Username = a.User != null ? a.User.Username : (string?)null,
                Action = a.Action.ToString(),
                a.Resource,
                a.ResourceId,
                a.IpAddress,
                CreatedAt = a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                OldData = a.OldData != null ? TruncateJson(a.OldData) : (string?)null,
                NewData = a.NewData != null ? TruncateJson(a.NewData) : (string?)null,
            })
            .ToListAsync();

        return Ok(new { items, totalCount, page, pageSize });
    }

    private static string TruncateJson(JsonDocument doc)
    {
        var json = doc.RootElement.GetRawText();
        return json.Length > 500 ? json[..500] + "..." : json;
    }
}
