using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for Rate Limiter registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class RateLimiterConfiguration
{
    /// <summary>
    /// Registers rate limiting policies for auth, booking, portal, and global endpoints.
    /// </summary>
    public static void AddRateLimiterConfiguration(this IServiceCollection services)
    {
        // ── Rate Limiting (H1 FIX: prevent brute-force on auth endpoints) ────────────
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("AuthPolicy", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    }));

            // P4 FIX: Strict rate limiting for public booking to prevent spam
            options.AddPolicy("BookingPolicy", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 1
                    }));

            // P7 FIX: Portal auth rate limiting (prevents abuse of forgot-password WhatsApp messages)
            options.AddPolicy("PortalAuthPolicy", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 2,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 1
                    }));

            // SEC-04 FIX: Stricter rate limit for password reset — 3 requests per 15 minutes per IP
            // Prevents brute-force attacks on password reset codes while allowing legitimate retries
            options.AddPolicy("PortalPasswordResetPolicy", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(15),
                        SegmentsPerWindow = 3,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0 // No queue — reject immediately
                    }));

            // Forgot password rate limiting — 3 requests per 15 minutes per IP
            options.AddPolicy("ForgotPasswordPolicy", context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(15),
                        SegmentsPerWindow = 3,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress ?? IPAddress.Loopback,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 10
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
                }
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "طلبات كثيرة جداً. حاول مرة أخرى بعد قليل.",
                    retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var r) ? r.TotalSeconds : 60
                }, cancellationToken);
            };
        });
    }
}
