using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// CORE-APPT-008: the reminder run used to compute a `targetHour` it never used and
/// filter appointments by DATE ONLY, so turning on the "2 hours before" window would
/// text every scheduled appointment on that day — including ones many hours away.
/// It also derived the date as `AddDays(hoursBefore >= 24 ? 1 : 0)`, which is only
/// correct when the run fires at the same hour of day as the appointment.
///
/// These are pure calculations on an injected "now" — no clock, no DB, no global
/// ClinicTimeProvider mutation (which would race the parallel suite).
/// </summary>
public class ReminderWindowTests
{
    // ─── The 2-hour window ────────────────────────────────────────────────

    [Fact]
    public void TwoHourWindow_TargetsTwoHoursAhead_NotTheWholeDay()
    {
        // Run at 08:00 → the 2-hour window is the 10:00–11:00 bucket.
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 8, 0, 0), hoursBefore: 2);

        window.Date.Should().Be(new DateOnly(2026, 6, 15));
        window.Start.Should().Be(new TimeOnly(10, 0));
        window.End.Should().Be(new TimeOnly(11, 0));
    }

    [Theory]
    [InlineData(10, 0, true)]   // exactly at the target
    [InlineData(10, 30, true)]  // inside the bucket
    [InlineData(11, 0, true)]   // inclusive upper edge
    [InlineData(9, 59, false)]  // earlier that day — the old code texted these
    [InlineData(14, 0, false)]  // much later that day — and these
    [InlineData(19, 30, false)] // and these
    public void TwoHourWindow_ContainsOnlyAppointmentsInTheBucket(int hour, int minute, bool expected)
    {
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 8, 0, 0), hoursBefore: 2);

        window.Contains(new TimeOnly(hour, minute)).Should().Be(expected);
    }

    // ─── The 24-hour window ───────────────────────────────────────────────

    [Fact]
    public void TwentyFourHourWindow_RollsToTheNextDay_AtTheSameClockTime()
    {
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 9, 0, 0), hoursBefore: 24);

        window.Date.Should().Be(new DateOnly(2026, 6, 16));
        window.Start.Should().Be(new TimeOnly(9, 0));
        window.End.Should().Be(new TimeOnly(10, 0));
    }

    [Fact]
    public void TwentyFourHourWindow_LateEveningRun_StaysOnTheCorrectDate()
    {
        // The old `AddDays(hoursBefore >= 24 ? 1 : 0)` + date-only filter would pull
        // in the whole of the 16th regardless of hour. now+24h is 22:30 on the 16th.
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 22, 30, 0), hoursBefore: 24);

        window.Date.Should().Be(new DateOnly(2026, 6, 16));
        window.Start.Should().Be(new TimeOnly(22, 30));
        window.Contains(new TimeOnly(9, 0)).Should().BeFalse("a 09:00 appointment is not 24h away from a 22:30 run");
    }

    // ─── Month/year boundaries ────────────────────────────────────────────

    [Fact]
    public void CrossesMonthBoundary()
    {
        var window = ReminderWindow.For(new DateTime(2026, 6, 30, 14, 0, 0), hoursBefore: 24);
        window.Date.Should().Be(new DateOnly(2026, 7, 1));
    }

    [Fact]
    public void CrossesYearBoundary()
    {
        var window = ReminderWindow.For(new DateTime(2026, 12, 31, 14, 0, 0), hoursBefore: 24);
        window.Date.Should().Be(new DateOnly(2027, 1, 1));
    }

    [Fact]
    public void ShortWindowCrossingMidnight_MovesToTheNextDate()
    {
        // 23:00 + 2h = 01:00 the next day.
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 23, 0, 0), hoursBefore: 2);

        window.Date.Should().Be(new DateOnly(2026, 6, 16));
        window.Start.Should().Be(new TimeOnly(1, 0));
        window.End.Should().Be(new TimeOnly(2, 0));
    }

    // ─── The late-night clamp ─────────────────────────────────────────────

    [Fact]
    public void BucketRunningPastMidnight_IsClampedToEndOfDay_NotWrapped()
    {
        // Target lands at 23:30; the bucket would otherwise run to 00:30 the NEXT
        // day. Wrapping would make Start > End and match nothing (or, worse, match
        // early-morning appointments on the wrong date).
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 21, 30, 0), hoursBefore: 2);

        window.Date.Should().Be(new DateOnly(2026, 6, 15));
        window.Start.Should().Be(new TimeOnly(23, 30));
        window.End.Should().Be(TimeOnly.MaxValue);
        window.End.Should().BeAfter(window.Start, "a clamped bucket must still be a valid range");

        window.Contains(new TimeOnly(23, 45)).Should().BeTrue();
        window.Contains(new TimeOnly(0, 15)).Should().BeFalse("00:15 belongs to the next clinic date");
    }

    [Fact]
    public void BucketStartingExactlyAt2300_IsClamped_NotInverted()
    {
        // The exact off-by-one boundary: TimeOnly.AddHours wraps, so 23:00 + 1h is
        // 00:00. Treating 23:00 as "safe to add an hour" produces a 23:00–00:00 range
        // that matches nothing, silently dropping every 23:00-hour appointment.
        var window = ReminderWindow.For(new DateTime(2026, 6, 15, 21, 0, 0), hoursBefore: 2);

        window.Start.Should().Be(new TimeOnly(23, 0));
        window.End.Should().BeAfter(window.Start);
        window.Contains(new TimeOnly(23, 0)).Should().BeTrue();
        window.Contains(new TimeOnly(23, 30)).Should().BeTrue();
    }

    [Fact]
    public void WindowRangeIsAlwaysValid_AcrossEveryStartMinuteOfTheDay()
    {
        // Guards the clamp boundary generally: no start-of-day minute may produce
        // an inverted range, which would silently match zero appointments.
        var midnight = new DateTime(2026, 6, 15, 0, 0, 0);

        for (var minute = 0; minute < 24 * 60; minute++)
        {
            var window = ReminderWindow.For(midnight.AddMinutes(minute), hoursBefore: 2);

            window.End.Should().BeOnOrAfter(window.Start,
                $"window starting at minute {minute} must not invert");
            window.Contains(window.Start).Should().BeTrue(
                $"window starting at minute {minute} must contain its own start");
        }
    }
}
