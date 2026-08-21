using AqlanDentalPro.Domain.Enums;
using System.Security.Claims;

namespace AqlanDentalPro.API.Middleware;

/// <summary>
/// Defense-in-depth guard for owner-only account/security management routes.
/// Existing controllers still keep their normal authorization attributes; this
/// middleware adds the stricter invariant that user/role/permission management
/// can only be performed by the single configured owner.
/// </summary>
public sealed class SuperAdminManagementGuardMiddleware(
    RequestDelegate next,
    IConfiguration configuration,
    ILogger<SuperAdminManagementGuardMiddleware> logger)
{
    private readonly string _ownerUsername =
        (configuration["Security:SuperAdminUsername"]
         ?? Environment.GetEnvironmentVariable("SUPER_ADMIN_USERNAME")
         ?? "admin").Trim();

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsOwnerOnlyManagementPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        // Let the normal ASP.NET authorization pipeline produce the correct 401
        // for unauthenticated callers.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var username = context.User.FindFirstValue(ClaimTypes.Name)
            ?? context.User.FindFirstValue("unique_name")
            ?? string.Empty;

        var isSuperAdmin = string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.Ordinal);

        // Transitional compatibility: an access token issued immediately before
        // the startup promotion may still carry Admin until it is refreshed. Only
        // the exact configured owner username is accepted in that narrow case.
        var isPromotedOwnerWithOldToken =
            string.Equals(role, nameof(UserRole.Admin), StringComparison.Ordinal)
            && string.Equals(username, _ownerUsername, StringComparison.OrdinalIgnoreCase);

        if (isSuperAdmin || isPromotedOwnerWithOldToken)
        {
            await next(context);
            return;
        }

        logger.LogWarning(
            "SEC: blocked non-owner user-management request. User={Username}, Role={Role}, Path={Path}, Method={Method}",
            username,
            role,
            context.Request.Path.Value,
            context.Request.Method);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(new
        {
            message = "هذه العملية مخصصة للمشرف العام مالك النظام فقط"
        });
    }

    private static bool IsOwnerOnlyManagementPath(PathString path)
    {
        return path.StartsWithSegments("/api/users", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/roles", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/permissions", StringComparison.OrdinalIgnoreCase);
    }
}
