namespace AqlanDentalPro.API.Middleware;

/// <summary>
/// Adds security headers to all responses.
/// These headers protect against XSS, clickjacking, MIME sniffing, etc.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IWebHostEnvironment env)
    {
        // Prevent clickjacking — allow same-origin only
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

        // XSS Protection (legacy browsers)
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer Policy — send origin only on cross-origin requests
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy.
        // SEC (audit §5.10): the API serves only JSON responses and uploaded files in
        // production (Swagger UI — the only backend-served HTML with inline scripts — is
        // disabled in Production, see Program.cs). So in production we DROP 'unsafe-inline'
        // and 'unsafe-eval' from script-src entirely. Non-production keeps them so Swagger UI
        // still works. (The Next.js app is served by Vercel with its own headers and is not
        // affected by this backend CSP.) 'unsafe-inline' is retained for style-src only —
        // inline styles cannot execute script, and Swagger/ASP.NET error pages rely on them.
        var scriptSrc = env.IsProduction()
            ? "script-src 'self' https://www.google.com https://www.gstatic.com; "
            : "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://www.google.com https://www.gstatic.com; ";

        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            scriptSrc +
            "frame-src https://www.google.com; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data: blob:; " +
            "connect-src 'self' https://www.google.com; " +
            "object-src 'none'; " +        // no plugins/embeds
            "base-uri 'self'; " +          // block <base> tag injection
            "frame-ancestors 'self';";     // modern clickjacking guard (complements X-Frame-Options)

        // Permissions Policy — restrict browser features
        context.Response.Headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=()";

        // HSTS — force HTTPS for 1 year (only meaningful in production behind HTTPS)
        context.Response.Headers["Strict-Transport-Security"] =
            "max-age=31536000; includeSubDomains";

        await _next(context);
    }
}

/// <summary>
/// Extension method for easy registration.
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
