using Microsoft.Extensions.Configuration;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// FIN-16 / CLIN-07: Provides clinic-local date/time using a configurable timezone.
/// Previously, DateTime.Today (server-local) was used for daily reports, finance dashboards,
/// and cashier sessions. On Railway (UTC), "today" was wrong by 3 hours for a Yemen clinic.
/// This service reads the clinic timezone from Settings:ClinicTimezone (default: Asia/Aden, UTC+3)
/// and provides ClinicToday / ClinicNow that are correct for the clinic's wall clock.
/// </summary>
public static class ClinicTimeProvider
{
    private static readonly TimeZoneInfo DefaultTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Aden");

    /// <summary>
    /// Returns the clinic-local date based on the configured timezone.
    /// On Railway (UTC), this returns Yemen's date, not the server's date.
    /// </summary>
    public static DateOnly ClinicToday(TimeZoneInfo? tz = null)
        => DateOnly.FromDateTime(ClinicNow(tz));

    /// <summary>
    /// Returns the clinic-local DateTime (UTC converted to clinic timezone).
    /// </summary>
    public static DateTime ClinicNow(TimeZoneInfo? tz = null)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz ?? DefaultTz);

    /// <summary>
    /// Converts a clinic-local DateOnly to a UTC DateTime range [start, end) for DB queries.
    /// Usage: var (start, end) = ClinicTimeProvider.ToUtcRange(reportDate);
    ///        query.Where(x => x.CreatedAt >= start && x.CreatedAt < end)
    /// </summary>
    public static (DateTime Start, DateTime End) ToUtcRange(DateOnly clinicDate, TimeZoneInfo? tz = null)
    {
        var zone = tz ?? DefaultTz;
        var localStart = clinicDate.ToDateTime(TimeOnly.MinValue);
        var localEnd = localStart.AddDays(1);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, zone);
        return (DateTime.SpecifyKind(utcStart, DateTimeKind.Utc),
                DateTime.SpecifyKind(utcEnd, DateTimeKind.Utc));
    }
}
