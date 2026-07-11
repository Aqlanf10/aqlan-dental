using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// SEQ-13: resolves the clinic timezone once during application startup.
/// Priority: Settings table (clinic.timezone), environment/app configuration
/// (Clinic:Timezone, then legacy Settings:ClinicTimezone), then Asia/Aden.
/// No database read occurs on ClinicToday/ClinicNow calls.
/// </summary>
public sealed class ClinicTimeZoneInitializer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ClinicTimeZoneInitializer> logger) : IHostedService
{
    public const string SettingsKey = "clinic.timezone";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? databaseValue = null;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            databaseValue = await db.Settings
                .AsNoTracking()
                .Where(s => s.Key == SettingsKey)
                .Select(s => s.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Timezone configuration must never prevent production startup.
            logger.LogWarning(ex,
                "Could not read {SettingsKey}; configuration fallback will be used.",
                SettingsKey);
        }

        var configuredId = FirstNonBlank(
            databaseValue,
            configuration["Clinic:Timezone"],
            configuration["Settings:ClinicTimezone"]);

        var zone = ClinicTimeProvider.Configure(
            configuredId,
            warning => logger.LogWarning("{Warning}", warning));

        logger.LogInformation(
            "Clinic timezone initialized as {TimeZoneId} (source: {Source}).",
            zone.Id,
            SourceName(databaseValue, configuration));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    internal static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string SourceName(string? databaseValue, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(databaseValue)) return SettingsKey;
        if (!string.IsNullOrWhiteSpace(configuration["Clinic:Timezone"])) return "Clinic:Timezone";
        if (!string.IsNullOrWhiteSpace(configuration["Settings:ClinicTimezone"])) return "Settings:ClinicTimezone";
        return $"default:{ClinicTimeProvider.DefaultTimeZoneId}";
    }
}
