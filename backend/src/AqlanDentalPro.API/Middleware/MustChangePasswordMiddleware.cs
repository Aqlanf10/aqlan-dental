using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AqlanDentalPro.API.Middleware;

/// <summary>
/// Middleware that blocks access to all portal endpoints (except auth/change-password)
/// when the JWT contains mustChangePassword=true claim.
/// This ensures backend enforcement — even if the frontend redirect is bypassed,
/// the patient cannot call any portal API until they change their password.
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    // Paths that are allowed even when mustChangePassword is true
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/portal/auth/login",
        "/api/portal/auth/forgot-password",
        "/api/portal/auth/reset-password",
        "/api/portal/auth/change-password",
        "/api/portal/auth/refresh-token",
        "/api/portal/clinic-info"
    };

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        // Only check portal paths
        if (path != null && path.StartsWith("/api/portal/", StringComparison.OrdinalIgnoreCase))
        {
            // Check if the user is authenticated and has the mustChangePassword claim
            var mustChangeClaim = context.User.FindFirst("mustChangePassword")?.Value;

            if (mustChangeClaim == "true" && !AllowedPaths.Contains(path))
            {
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
