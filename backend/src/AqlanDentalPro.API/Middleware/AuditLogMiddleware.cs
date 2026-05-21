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

    // GET requests on these API path segments are also audited (sensitive data access trail)
    private static readonly HashSet<string> SensitiveGetSegments =
        ["patients", "contracts", "payments", "invoices", "finance", "reports"];

    // Request body is captured (up to 4 KB) for writes on these segments
    private static readonly HashSet<string> BodyCaptureSegments =
        ["payments", "contracts", "invoices", "finance"];

    public async Task InvokeAsync(HttpContext context, AppDbContext db, ICurrentUserService currentUser)
    {
        var method = context.Request.Method;
        var path   = context.Request.Path;

        var isWriteMethod   = AuditedMethods.Contains(method);
        var isSensitiveRead = method == "GET" && PathContainsAny(path, SensitiveGetSegments);

        if (!isWriteMethod && !isSensitiveRead)
        {
            await next(context);
            return;
        }

        // Enable buffering before calling next so we can re-read the request body afterwards
        var captureBody = isWriteMethod && method is "POST" or "PUT"
                          && PathContainsAny(path, BodyCaptureSegments);
        if (captureBody)
            context.Request.EnableBuffering();

        await next(context);

        // Skip anonymous requests — nothing useful to record without an actor
        if (!context.User.Identity?.IsAuthenticated == true) return;

        // Read body after pipeline (seek back to start)
        string? requestBody = null;
        if (captureBody && context.Request.Body.CanSeek)
        {
            context.Request.Body.Position = 0;
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var raw = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(raw) && raw.Length <= 4096)
                requestBody = raw;
        }

        var action = method switch
        {
            "GET"    => AuditAction.View,
            "POST"   => AuditAction.Create,
            "PUT"    => AuditAction.Update,
            "PATCH"  => AuditAction.Update,
            "DELETE" => AuditAction.Delete,
            _        => AuditAction.View
        };

        var segments  = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
        var resource  = segments.Length >= 2 ? segments[1] : (segments.Length >= 1 ? segments[0] : "unknown");
        Guid.TryParse(segments.Length >= 3 ? segments[2] : null, out var resourceId);

        // Patient portal accounts live in PatientAccounts, not Users.
        // Setting UserId for them would violate FK_AuditLogs_Users_UserId.
        var isPatientPortal = IsPatientPortalUser(context.User);

        var log = new AuditLog
        {
            UserId     = isPatientPortal ? null : currentUser.UserId,
            Action     = action,
            Resource   = resource,
            ResourceId = resourceId == Guid.Empty ? null : resourceId,
            IpAddress  = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent  = context.Request.Headers.UserAgent.ToString()
        };

        // Attach metadata: HTTP status, failure flag, request body, portal identity
        var meta = new Dictionary<string, object?> { ["statusCode"] = context.Response.StatusCode };

        if (context.Response.StatusCode >= 400)
            meta["failed"] = true;

        if (requestBody is not null)
        {
            try   { meta["requestBody"] = JsonSerializer.Deserialize<JsonElement>(requestBody); }
            catch { meta["requestBody"] = requestBody; }
        }

        if (isPatientPortal)
        {
            meta["actorType"]       = "PatientPortal";
            meta["patientId"]       = context.User.FindFirstValue("patientId");
            meta["patientUsername"] = context.User.FindFirstValue(ClaimTypes.Name)
                                      ?? context.User.FindFirstValue("unique_name");
        }

        log.NewData = JsonDocument.Parse(JsonSerializer.Serialize(meta));

        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    private static bool PathContainsAny(PathString path, HashSet<string> segments)
    {
        var parts = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts?.Any(p => segments.Contains(p.ToLowerInvariant())) == true;
    }

    /// <summary>
    /// Patient portal JWTs carry a "portal" claim or Role = Patient.
    /// Their sub is a PatientAccount ID, not a User ID.
    /// </summary>
    private static bool IsPatientPortalUser(ClaimsPrincipal user)
    {
        if (string.Equals(user.FindFirstValue("portal"), "true", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(
            user.FindFirstValue(ClaimTypes.Role), "Patient", StringComparison.OrdinalIgnoreCase);
    }
}
