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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null && !Directory.Exists(Path.Combine(current.FullName, "backend", "src")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }
}
