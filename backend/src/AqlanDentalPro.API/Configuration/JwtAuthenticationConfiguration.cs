using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for JWT Authentication registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class JwtAuthenticationConfiguration
{
    /// <summary>
    /// Registers JWT Bearer authentication with the configured secret key, issuer, and audience.
    /// </summary>
    public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // ── JWT Authentication ────────────────────────────────────────────────────────
        var jwtKey = configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is required");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew = TimeSpan.Zero
                };
                opts.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Inline media cannot attach an Authorization header. The backend
                        // issues this cookie as HttpOnly and it is accepted only by the
                        // read-only, ownership-checked file route.
                        if (context.Request.Path.StartsWithSegments("/uploads") &&
                            context.Request.Cookies.TryGetValue("aqlan_access_token", out var token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
    }
}
