using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AqlanDentalPro.API.Middleware;

/// <summary>
/// Middleware that blocks access to all protected endpoints (except auth/change-password)
/// when the JWT contains mustChangePassword=true claim.
/// This ensures backend enforcement — even if the frontend redirect is bypassed,
/// the user cannot call any protected API until they change their password.
/// SEC-02 FIX: Extended from portal-only to also cover staff API paths.
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    // Paths that are allowed even when mustChangePassword is true
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        // Portal auth paths
        "/api/portal/auth/login",
        "/api/portal/auth/forgot-password",
        "/api/portal/auth/reset-password",
        "/api/portal/auth/change-password",
        "/api/portal/auth/refresh-token",
        "/api/portal/clinic-info",
        // Staff auth paths (SEC-02 FIX: allow staff to change their password)
        "/api/auth/login",
        "/api/auth/refresh",
        "/api/auth/me",
    };

    // Path prefixes that are always allowed (public endpoints)
    private static readonly string[] AllowedPrefixes =
    [
        "/api/auth/changepassword",
        "/api/auth/change-password",
        "/swagger",
        "/health",
    ];

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        if (path != null && (path.StartsWith("/api/portal/", StringComparison.OrdinalIgnoreCase) ||
                             path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)))
        {
            // Check if the user is authenticated and has the mustChangePassword claim
            var mustChangeClaim = context.User.FindFirst("mustChangePassword")?.Value;

            if (mustChangeClaim == "true")
            {
                // Allow if exact path match
                if (AllowedPaths.Contains(path))
                {
                    await _next(context);
                    return;
                }

                // Allow if path starts with any allowed prefix
                foreach (var prefix in AllowedPrefixes)
                {
                    if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        await _next(context);
                        return;
                    }
                }

                // Block all other API requests
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    message = "يجب تغيير كلمة المرور قبل الوصول إلى هذا المورد",
                    code = "MUST_CHANGE_PASSWORD"
                });
                return;
            }
        }

        await _next(context);
    }
}
