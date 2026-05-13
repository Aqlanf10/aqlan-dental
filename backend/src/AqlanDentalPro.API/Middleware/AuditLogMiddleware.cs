using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Domain.Entities;
using System.Security.Claims;
using System.Text.Json;

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

        // Patient portal accounts have IDs in PatientAccounts, not Users.
        // Setting UserId for them violates FK_AuditLogs_Users_UserId.
        // For patient portal users, we store their identity in NewData metadata
        // and leave UserId null (which is allowed — the FK is nullable with SetNull).
        var isPatientPortal = IsPatientPortalUser(context.User);

        var log = new AuditLog
        {
            UserId = isPatientPortal ? null : currentUser.UserId,
            Action = action,
            Resource = resource,
            ResourceId = resourceId == Guid.Empty ? null : resourceId,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString()
        };

        if (isPatientPortal)
        {
            // Preserve patient identity in NewData for traceability
            var patientMeta = new
            {
                actorType = "PatientPortal",
                patientId = context.User.FindFirstValue("patientId"),
                patientUsername = context.User.FindFirstValue(ClaimTypes.Name)
                                  ?? context.User.FindFirstValue("unique_name")
            };
            log.NewData = JsonDocument.Parse(JsonSerializer.Serialize(patientMeta));
        }

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Detects patient portal authenticated users.
    /// Patient portal JWTs contain a "portal" claim or have Role = Patient.
    /// These IDs reference PatientAccounts, not the Users table.
    /// </summary>
    private static bool IsPatientPortalUser(ClaimsPrincipal user)
    {
        // Primary: check for explicit "portal" claim set during patient portal auth
        var portalClaim = user.FindFirstValue("portal");
        if (string.Equals(portalClaim, "true", StringComparison.OrdinalIgnoreCase))
            return true;

        // Secondary: check role claim — Patient role is exclusive to portal accounts
        var roleClaim = user.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(roleClaim, "Patient", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
