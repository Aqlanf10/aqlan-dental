using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.GoLive;

/// <summary>
/// Third go-live dry run, walked through the lab module — «تراكم التراكيب».
///
/// <para>
/// The overdue lab endpoint reads <see cref="ClinicTimeProvider.ClinicToday"/>. The dashboard
/// computed its own "today" from <c>DateTime.Today</c>, the server's date, while a comment
/// beside it claimed the two used the same definition. Railway runs UTC and the clinic is at
/// UTC+3, so every night between 00:00 and 03:00 Yemen time the dashboard was a day behind the
/// lab page: an appliance due yesterday was not yet counted as late, and "today's" appointments
/// and no-shows were yesterday's.
/// </para>
///
/// <para>
/// This is asserted at a fixed instant rather than against the wall clock, because a test that
/// only fails for three hours a night is a test that passes by luck.
/// </para>
/// </summary>
public class DashboardClinicDayTests
{
    private static readonly TimeZoneInfo Aden = ClinicTimeProvider.ResolveTimeZone("Asia/Aden");

    [Theory]
    // 21:00 UTC onwards is already tomorrow in Yemen — the window the dashboard got wrong.
    [InlineData(21, 30, "2026-08-23")]
    [InlineData(23, 59, "2026-08-23")]
    // Outside the window the two agree, which is why this went unnoticed for so long.
    [InlineData(20, 30, "2026-08-22")]
    [InlineData(2, 30, "2026-08-22")]
    public void The_clinic_day_is_not_the_servers_day(int utcHour, int utcMinute, string expected)
    {
        var instant = new DateTime(2026, 8, 22, utcHour, utcMinute, 0, DateTimeKind.Utc);

        var clinicDate = ClinicTimeProvider.ClinicDateFromUtc(instant, Aden);
        var serverDate = DateOnly.FromDateTime(instant);   // what DateTime.Today gives on a UTC host

        clinicDate.Should().Be(DateOnly.Parse(expected));

        if (utcHour >= 21)
            clinicDate.Should().NotBe(serverDate,
                "this is the window in which the dashboard reported the wrong day");
    }

    /// <summary>
    /// The behaviour above only matters if the dashboard actually reads the clinic clock, so
    /// this pins the source. A behavioural test would need the whole EF graph stood up for
    /// what is a one-line property of the query.
    /// </summary>
    [Fact]
    public void The_dashboard_reads_the_clinic_day_everywhere_it_asks_for_today()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;
        dir.Should().NotBeNull();

        var source = File.ReadAllText(Path.Combine(
            dir!.FullName, "src", "AqlanDentalPro.Infrastructure", "Services", "DashboardService.cs"));

        source.Length.Should().BeGreaterThan(5_000, "the file must be readable for this to mean anything");

        source.Should().NotContain("DateTime.Today",
            "the server's date is not the clinic's date; use ClinicTimeProvider.ClinicToday()");
        source.Should().NotContain("DateTime.UtcNow.Date",
            "a UTC midnight boundary is not a clinic-day boundary; use ClinicTimeProvider.ToUtcRange()");
        source.Should().Contain("ClinicTimeProvider.ClinicToday()");
    }
}
