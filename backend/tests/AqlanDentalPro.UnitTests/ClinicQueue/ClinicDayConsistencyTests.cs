using AqlanDentalPro.Infrastructure.Services;
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

    /// <summary>
    /// The dashboard's own tests must ask the same clock the dashboard asks.
    ///
    /// <para>
    /// `DashboardService` was moved onto `ClinicTimeProvider.ClinicToday()` and its tests were
    /// left seeding `DateOnly.FromDateTime(DateTime.Today)`. The two agree for 21 hours out of
    /// every 24, so the suite passed all evening and every local run, and the mismatch only
    /// surfaced when CI first ran between 21:00 and 24:00 UTC — after midnight in Aden, where
    /// the clinic's day has already rolled over and the server's has not. Four tests failed on
    /// `main`, which deploys.
    /// </para>
    ///
    /// <para>
    /// The test above guards the production source. This guards the test that exercises it,
    /// because a green suite is only evidence when the fixture and the code share a clock.
    /// </para>
    /// </summary>
    [Fact]
    public void DashboardAlertTests_SeedTheClinicDay_NotTheServerDay()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "backend", "tests", "AqlanDentalPro.UnitTests", "Dashboard", "DashboardAlertsTests.cs"));

        // If the file could not be read, every assertion below would check an empty string.
        source.Length.Should().BeGreaterThan(1000);

        source.Should().NotContain("DateOnly.FromDateTime(DateTime.Today)",
            "the service reads ClinicToday, so a fixture on the server day is wrong for three "
            + "hours every night");
        source.Should().NotContain("DateOnly.FromDateTime(DateTime.UtcNow)",
            "same reason — the clinic's calendar date is not the server's");
        source.Should().Contain("ClinicTimeProvider.ClinicToday()");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "backend", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
