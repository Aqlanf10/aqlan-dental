using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for Redis connection multiplexer registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class RedisConfiguration
{
    /// <summary>
    /// Registers <see cref="IConnectionMultiplexer"/> as a singleton with a 3-attempt
    /// resilient connection strategy so the app starts even when Redis is unavailable.
    /// </summary>
    public static void AddRedisConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Redis ─────────────────────────────────────────────────────────────────
        // HOTFIX: Make Redis registration fully resilient so the app starts even when
        // Redis is unavailable. Without this, ConnectionMultiplexer.Connect() throws
        // RedisConnectionException at DI resolution time, which crashes the entire
        // LoginAttemptService + TokenService chain and turns every staff login into a 500.
        //
        // Strategy:
        // 1. Try to connect with AbortOnConnectFail=false (returns a multiplexer that
        //    retries in the background even if the initial connect fails).
        // 2. If ConfigurationOptions.Parse() or Connect() throws for ANY reason
        //    (invalid connection string, DNS failure, etc.), fall back to a minimal
        //    "localhost:6379" configuration. This multiplexer will be disconnected but
        //    the app will start — Redis operations in LoginAttemptService and TokenService
        //    are wrapped in try/catch and degrade gracefully.
        // 3. If even the fallback fails, create a completely disconnected multiplexer
        //    using ConnectAsync + manual configuration — this should never happen but
        //    ensures the app ALWAYS starts.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetService<ILogger<Program>>();

            ConnectionMultiplexer? mux = null;

            // Attempt 1: Connect with configured connection string
            try
            {
                var connString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
                var options = ConfigurationOptions.Parse(connString);
                options.AbortOnConnectFail = false;
                options.ConnectRetry = 3;
                options.ReconnectRetryPolicy = new ExponentialRetry(5000);
                options.ConnectTimeout = 3000; // Don't block startup for too long
                mux = ConnectionMultiplexer.Connect(options);
                logger?.LogInformation("Redis connection multiplexer created (AbortOnConnectFail=false, IsConnected={IsConnected})", mux.IsConnected);
                return mux;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Redis: Primary connection attempt failed. Trying fallback configuration.");
            }

            // Attempt 2: Fallback to localhost with minimal config
            try
            {
                var fallbackOptions = new ConfigurationOptions
                {
                    EndPoints = { "localhost:6379" },
                    AbortOnConnectFail = false,
                    ConnectRetry = 0,
                    ConnectTimeout = 1000
                };
                mux = ConnectionMultiplexer.Connect(fallbackOptions);
                logger?.LogWarning("Redis: Connected with fallback configuration (localhost:6379). Redis features will be degraded.");
                return mux;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Redis: Fallback connection also failed. Creating minimal disconnected multiplexer.");
            }

            // Attempt 3: Last resort — return a disconnected multiplexer
            // This ensures the app ALWAYS starts. Redis-dependent features degrade gracefully.
            try
            {
                var lastResortOptions = new ConfigurationOptions
                {
                    EndPoints = { "localhost:6379" },
                    AbortOnConnectFail = false,
                    ConnectTimeout = 1,
                    ConnectRetry = 0,
                    AsyncTimeout = 1,
                    SyncTimeout = 1
                };
                mux = ConnectionMultiplexer.Connect(lastResortOptions);
                logger?.LogCritical("Redis: Created disconnected multiplexer as last resort. LoginAttemptService and TokenService will operate in degraded mode (no lockout, no refresh token persistence).");
                return mux;
            }
            catch (Exception ex)
            {
                // Absolute last resort — this should never happen with AbortOnConnectFail=false
                logger?.LogCritical(ex, "Redis: ALL connection attempts failed. Creating multiplexer with absolute minimum config.");
                var emergencyOptions = new ConfigurationOptions
                {
                    EndPoints = { "127.0.0.1:6379" },
                    AbortOnConnectFail = false
                };
                return ConnectionMultiplexer.Connect(emergencyOptions);
            }
        });
    }
}
