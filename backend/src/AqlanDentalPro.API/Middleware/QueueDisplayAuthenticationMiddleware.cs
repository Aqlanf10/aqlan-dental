using Microsoft.AspNetCore.Http;

namespace AqlanDentalPro.API.Middleware;

/// <summary>
/// CORE-PAT-020: hard security boundary for endpoints that expose the live clinic queue.
/// The display action historically carries AllowAnonymous, so this guard runs after
/// authentication and before endpoint authorization to prevent direct internet access
/// even if an action attribute is accidentally loosened later.
/// </summary>
public sealed class QueueDisplayAuthenticationMiddleware(RequestDelegate next)
{
    private static readonly string[] ProtectedPaths =
    [
        "/api/clinic-queue/display",
        "/api/public/queue",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path.Value;
        var protectsPatientQueue = ProtectedPaths.Any(path =>
            string.Equals(requestPath, path, StringComparison.OrdinalIgnoreCase));

        if (protectsPatientQueue && context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "يلزم تسجيل الدخول لعرض شاشة الانتظار",
            });
            return;
        }

        await next(context);
    }
}
