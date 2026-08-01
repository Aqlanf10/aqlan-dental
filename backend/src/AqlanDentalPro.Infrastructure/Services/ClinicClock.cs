using AqlanDentalPro.Application.Interfaces.Services;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Canonical adapter over ClinicTimeProvider. Application services depend on
/// this abstraction instead of mixing DateTime.UtcNow, DateOnly.FromDateTime,
/// and direct timezone conversion calls.
/// </summary>
public sealed class ClinicClock : IClinicClock
{
    public DateOnly Today() => ClinicTimeProvider.ClinicToday();
    public DateTime UtcNow() => DateTime.UtcNow;
    public DateTime ClinicNow() => ClinicTimeProvider.ClinicNow();
    public DateOnly DateFromUtc(DateTime utc) => ClinicTimeProvider.ClinicDateFromUtc(utc);
    public string TimeZoneId => ClinicTimeProvider.CurrentTimeZone.Id;
}
