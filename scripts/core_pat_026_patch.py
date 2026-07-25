from pathlib import Path

# Clinic time provider: expose deterministic UTC -> clinic conversion for tests.
provider = Path("backend/src/AqlanDentalPro.Infrastructure/Services/ClinicTimeProvider.cs")
text = provider.read_text(encoding="utf-8")
old = '''    public static DateOnly ClinicToday(TimeZoneInfo? tz = null)
        => DateOnly.FromDateTime(ClinicNow(tz));

    /// <summary>
    /// Returns the clinic-local DateTime (UTC converted to clinic timezone).
    /// </summary>
    public static DateTime ClinicNow(TimeZoneInfo? tz = null)
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz ?? CurrentTimeZone);
'''
new = '''    public static DateOnly ClinicToday(TimeZoneInfo? tz = null)
        => ClinicDateFromUtc(DateTime.UtcNow, tz);

    /// <summary>
    /// Deterministically converts a UTC instant to the clinic-local calendar date.
    /// Useful at midnight boundaries and in tests.
    /// </summary>
    public static DateOnly ClinicDateFromUtc(DateTime utc, TimeZoneInfo? tz = null)
        => DateOnly.FromDateTime(ClinicTimeFromUtc(utc, tz));

    /// <summary>
    /// Returns the clinic-local DateTime (UTC converted to clinic timezone).
    /// </summary>
    public static DateTime ClinicNow(TimeZoneInfo? tz = null)
        => ClinicTimeFromUtc(DateTime.UtcNow, tz);

    public static DateTime ClinicTimeFromUtc(DateTime utc, TimeZoneInfo? tz = null)
    {
        var normalizedUtc = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, tz ?? CurrentTimeZone);
    }
'''
if old not in text:
    raise SystemExit("CORE-PAT-026: ClinicTimeProvider target block not found")
provider.write_text(text.replace(old, new, 1), encoding="utf-8")

# Appointments controller: replace calendar-day decisions only. UTC audit timestamps remain untouched.
appointments = Path("backend/src/AqlanDentalPro.API/Controllers/AppointmentsController.cs")
text = appointments.read_text(encoding="utf-8")
old_day = "DateOnly.FromDateTime(DateTime.UtcNow)"
if text.count(old_day) != 4:
    raise SystemExit(f"CORE-PAT-026: expected 4 appointment UTC-day conversions, found {text.count(old_day)}")
text = text.replace(old_day, "ClinicTimeProvider.ClinicToday()")
old_upcoming = '''        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
'''
new_upcoming = '''        var now = ClinicTimeProvider.ClinicNow();
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);
'''
if old_upcoming not in text:
    raise SystemExit("CORE-PAT-026: upcoming appointment clock block not found")
appointments.write_text(text.replace(old_upcoming, new_upcoming, 1), encoding="utf-8")

# Portal: DateTime.Today follows the server timezone (UTC in production). Every occurrence
# here is a patient-facing calendar-day decision, so all five use the clinic day.
portal = Path("backend/src/AqlanDentalPro.Infrastructure/Services/PatientPortalService.cs")
text = portal.read_text(encoding="utf-8")
old_portal_day = "DateOnly.FromDateTime(DateTime.Today)"
if text.count(old_portal_day) != 5:
    raise SystemExit(f"CORE-PAT-026: expected 5 portal server-day conversions, found {text.count(old_portal_day)}")
portal.write_text(text.replace(old_portal_day, "ClinicTimeProvider.ClinicToday()"), encoding="utf-8")

# Deterministic provider tests plus a source contract that prevents the two audited files
# from reintroducing server/UTC calendar-day decisions.
test = Path("backend/tests/AqlanDentalPro.UnitTests/ClinicQueue/ClinicDayConsistencyTests.cs")
test.write_text('''using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

public class ClinicDayConsistencyTests
{
    [Fact]
    public void ClinicDateFromUtc_AfterNinePmUtc_IsNextDayInAden()
    {
        var aden = ClinicTimeProvider.ResolveTimeZone("Asia/Aden");
        var utc = new DateTime(2026, 7, 24, 22, 30, 0, DateTimeKind.Utc);

        ClinicTimeProvider.ClinicDateFromUtc(utc, aden)
            .Should().Be(new DateOnly(2026, 7, 25));
        ClinicTimeProvider.ClinicTimeFromUtc(utc, aden).TimeOfDay
            .Should().Be(new TimeSpan(1, 30, 0));
    }

    [Fact]
    public void ClinicDateFromUtc_BeforeNinePmUtc_RemainsSameDayInAden()
    {
        var aden = ClinicTimeProvider.ResolveTimeZone("Asia/Aden");
        var utc = new DateTime(2026, 7, 24, 20, 30, 0, DateTimeKind.Utc);

        ClinicTimeProvider.ClinicDateFromUtc(utc, aden)
            .Should().Be(new DateOnly(2026, 7, 24));
    }

    [Fact]
    public void AppointmentAndPortalCalendarLogic_UseClinicClockNotServerDay()
    {
        var root = FindRepositoryRoot();
        var appointments = File.ReadAllText(Path.Combine(
            root, "backend", "src", "AqlanDentalPro.API", "Controllers", "AppointmentsController.cs"));
        var portal = File.ReadAllText(Path.Combine(
            root, "backend", "src", "AqlanDentalPro.Infrastructure", "Services", "PatientPortalService.cs"));

        appointments.Should().NotContain("DateOnly.FromDateTime(DateTime.UtcNow)");
        appointments.Should().Contain("ClinicTimeProvider.ClinicToday()");
        appointments.Should().Contain("ClinicTimeProvider.ClinicNow()");
        portal.Should().NotContain("DateOnly.FromDateTime(DateTime.Today)");
        portal.Should().Contain("ClinicTimeProvider.ClinicToday()");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "backend", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
''', encoding="utf-8")

for temporary in (
    Path(".github/workflows/core-pat-026-bootstrap.yml"),
    Path("scripts/core_pat_026_patch.py"),
):
    temporary.unlink(missing_ok=True)
