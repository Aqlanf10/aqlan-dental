namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// FIN-16 / CLIN-07 / SEQ-13: Provides clinic-local date/time using a safely
/// cached, configurable timezone. The process starts with Asia/Aden and the
/// startup initializer may replace it once from Settings/configuration.
/// </summary>
public static class ClinicTimeProvider
{
    public const string DefaultTimeZoneId = "Asia/Aden";

    private static readonly TimeZoneInfo DefaultTz =
        TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);

    private static TimeZoneInfo _configuredTz = DefaultTz;

    /// <summary>
    /// Resolves a timezone identifier without mutating global state. Missing or
    /// invalid identifiers safely fall back to Asia/Aden.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId, Action<string>? warn = null)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return DefaultTz;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            warn?.Invoke($"Clinic timezone '{timeZoneId}' was not found. Falling back to {DefaultTimeZoneId}.");
            return DefaultTz;
        }
        catch (InvalidTimeZoneException)
        {
            warn?.Invoke($"Clinic timezone '{timeZoneId}' is invalid. Falling back to {DefaultTimeZoneId}.");
            return DefaultTz;
        }
    }

    /// <summary>
    /// Configures the process-wide clinic timezone once during application
    /// startup. Volatile access keeps readers lock-free and thread-safe.
    /// </summary>
    public static TimeZoneInfo Configure(string? timeZoneId, Action<string>? warn = null)
    {
        var resolved = ResolveTimeZone(timeZoneId, warn);
        Volatile.Write(ref _configuredTz, resolved);
        return resolved;
    }

    public static TimeZoneInfo CurrentTimeZone => Volatile.Read(ref _configuredTz);

    /// <summary>
    /// Returns the clinic-local date based on the configured timezone.
    /// Explicit timezone parameters remain available for deterministic tests.
    /// </summary>
    public static DateOnly ClinicToday(TimeZoneInfo? tz = null)
        => DateOnly.FromDateTime(ClinicNow(tz));

    /// <summary>
    /// Returns the clinic-local DateTime (UTC converted to clinic timezone).
    /// </summary>
    public static DateTime ClinicNow(TimeZoneInfo? tz = null)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz ?? CurrentTimeZone);

    /// <summary>
    /// Converts a clinic-local DateOnly to a UTC DateTime range [start, end)
    /// for database queries.
    /// </summary>
    public static (DateTime Start, DateTime End) ToUtcRange(DateOnly clinicDate, TimeZoneInfo? tz = null)
    {
        var zone = tz ?? CurrentTimeZone;
        var localStart = clinicDate.ToDateTime(TimeOnly.MinValue);
        var localEnd = localStart.AddDays(1);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
        return (DateTime.SpecifyKind(utcStart, DateTimeKind.Utc),
                DateTime.SpecifyKind(utcEnd, DateTimeKind.Utc));
    }
}
