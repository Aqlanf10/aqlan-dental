using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for CORS registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class CorsConfiguration
{
    /// <summary>
    /// Registers CORS policies: AllowFrontend (authenticated staff) and AllowPublicApi (public endpoints).
    /// </summary>
    public static void AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // ── CORS ──────────────────────────────────────────────────────────────────────
        var allowedOrigins = configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? ["http://localhost:3000", "http://localhost:3001"];
        // Always include Vercel deployment origins so the frontend can call the API directly
        allowedOrigins = [..allowedOrigins,
            "https://aqlan-dental-pro.vercel.app",
            "https://aqlan-dental.vercel.app",
            // PR #281 Vercel preview — allows Railway backend to accept calls from this preview deployment
            "https://aqlan-dental-6g7ji6s9y-aqlanf10-9871s-projects.vercel.app"];
        services.AddCors(opts =>
        {
            // Authenticated staff endpoints — strict origins, cookies allowed
            opts.AddPolicy("AllowFrontend", policy =>
            {
                policy.SetIsOriginAllowed(origin =>
                    {
                        if (allowedOrigins.Contains(origin)) return true;
                        // C-01 FIX: Removed wildcard *.vercel.app — only explicitly listed origins are allowed
                        return false;
                    })
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            // Public API endpoints (/api/public/*) — no auth/cookies, any origin is safe
            opts.AddPolicy("AllowPublicApi", policy =>
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod());
        });
    }
}
