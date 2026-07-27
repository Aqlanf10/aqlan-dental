using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// CORE-APPT-011: SMS "already sent" state lived in two records that never observed
/// each other — the job's per-window JSON (SmsReminderWindowsSent) and the manual
/// path's legacy boolean (ConfirmationSent). A manual 24h reminder followed by the
/// job reaching the same window texted the patient twice.
///
/// Owner decision: the windowless legacy flag means the 24-hour window ONLY. That
/// stops the common duplicate while leaving the 2-hour window free to fire —
/// suppressing every window instead would risk not reminding a patient shortly
/// before their appointment, which works against the no-show problem.
/// </summary>
public class ReminderSentWindowsTests
{
    // ─── The duplicate this closes ────────────────────────────────────────

    [Fact]
    public void LegacyFlagAlone_CountsAs24hSent()
    {
        // Manual path sent a day-before reminder; the job must not repeat it.
        ReminderSentWindows.WasSent(json: null, legacyConfirmationSent: true, hoursBefore: 24)
            .Should().BeTrue();
    }

    [Fact]
    public void LegacyFlagAlone_DoesNotSuppressThe2hWindow()
    {
        // The deliberate limit of the interpretation: a day-before nudge must not
        // cancel the reminder sent shortly before the appointment.
        ReminderSentWindows.WasSent(json: null, legacyConfirmationSent: true, hoursBefore: 2)
            .Should().BeFalse();
    }

    [Fact]
    public void NoFlagAndNoRecord_MeansNothingSent()
    {
        ReminderSentWindows.WasSent(null, false, 24).Should().BeFalse();
        ReminderSentWindows.WasSent(null, false, 2).Should().BeFalse();
    }

    // ─── The per-window record still governs ──────────────────────────────

    [Theory]
    [InlineData("[24]", 24, true)]
    [InlineData("[24]", 2, false)]
    [InlineData("[2]", 2, true)]
    [InlineData("[2]", 24, false)]
    [InlineData("[24,2]", 24, true)]
    [InlineData("[24,2]", 2, true)]
    [InlineData("[]", 24, false)]
    public void JsonRecordDecidesEachWindow(string json, int hoursBefore, bool expected)
    {
        ReminderSentWindows.WasSent(json, legacyConfirmationSent: false, hoursBefore)
            .Should().Be(expected);
    }

    [Fact]
    public void BothRecords_AreCombined_WithoutDuplicating24()
    {
        // The job wrote [2]; the manual path had already set the legacy flag.
        var merged = ReminderSentWindows.Merge("[2]", legacyConfirmationSent: true);

        merged.Should().BeEquivalentTo(new[] { 2, 24 });
        merged.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void LegacyFlagWithMatchingRecord_DoesNotDuplicate()
    {
        ReminderSentWindows.Merge("[24]", legacyConfirmationSent: true)
            .Should().BeEquivalentTo(new[] { 24 });
    }

    // ─── Malformed stored values must not throw the whole run ─────────────

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    [InlineData("")]
    [InlineData("   ")]
    public void MalformedJson_IsTreatedAsNothingSent_NotAnException(string json)
    {
        ReminderSentWindows.Parse(json).Should().BeEmpty();
        ReminderSentWindows.WasSent(json, false, 24).Should().BeFalse();
    }

    [Fact]
    public void MalformedJson_StillHonoursTheLegacyFlag()
    {
        // A corrupt record must not resurrect an already-sent 24h reminder.
        ReminderSentWindows.WasSent("not json", legacyConfirmationSent: true, hoursBefore: 24)
            .Should().BeTrue();
    }

    // ─── Custom windows configured via settings ───────────────────────────

    [Fact]
    public void ArbitraryConfiguredWindows_AreHandled()
    {
        // reminder_hours is configurable; 48/6 are as valid as 24/2.
        ReminderSentWindows.WasSent("[48]", false, 48).Should().BeTrue();
        ReminderSentWindows.WasSent("[48]", false, 6).Should().BeFalse();
        // The legacy flag only ever means 24 — never some other configured window.
        ReminderSentWindows.WasSent(null, true, 48).Should().BeFalse();
    }
}
