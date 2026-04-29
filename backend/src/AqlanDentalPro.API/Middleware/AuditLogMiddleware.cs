using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.API.Middleware;

public class AuditLogMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> AuditedMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ICurrentUserService currentUser)
    {
        await next(context);

        if (!AuditedMethods.Contains(context.Request.Method)) return;
        if (!context.User.Identity?.IsAuthenticated == true) return;
        if (context.Response.StatusCode >= 500) return;

        var action = context.Request.Method switch
        {
            "POST"   => AuditAction.Create,
            "PUT"    => AuditAction.Update,
            "PATCH"  => AuditAction.Update,
            "DELETE" => AuditAction.Delete,
            _        => AuditAction.View
        };

        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var resource = segments.Length >= 2 ? segments[1] : "unknown";
        Guid.TryParse(segments.Length >= 3 ? segments[2] : null, out var resourceId);

        var log = new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            Resource = resource,
            ResourceId = resourceId == Guid.Empty ? null : resourceId,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }
}
