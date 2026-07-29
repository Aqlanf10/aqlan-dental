using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for Controllers, SignalR, Swagger, static files, and Kestrel registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class ControllersConfiguration
{
    /// <summary>
    /// Registers Controllers, SignalR, Swagger, static files configuration, and Kestrel limits.
    /// </summary>
    public static void AddControllersConfiguration(this IServiceCollection services)
    {
        // ── Static Files (uploads) ────────────────────────────────────────────────────
        services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
        });

        // ── Controllers + Swagger ─────────────────────────────────────────────────────
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = false;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.MaximumReceiveMessageSize = 10 * 1024; // 10 KB
        });
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // FIX: Allow enum values as strings in JSON (e.g., "Consultation" instead of 0)
                // Without this, frontend sends category: "Other" but backend expects integer → 400 Bad Request
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

                // Sprint 1: Defensive safety net against circular JSON references.
                // If any endpoint accidentally returns a raw EF entity with bidirectional
                // navigation properties, IgnoreCycles will serialize the cycle as null
                // instead of throwing JsonException and crashing the request pipeline.
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Aqlan Dental Pro API",
                Version = "v1",
                Description = "نظام إدارة مركز د. عقلان الكامل لطب وتقويم الأسنان"
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }
}
