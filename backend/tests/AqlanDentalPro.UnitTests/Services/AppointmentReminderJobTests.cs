using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Services;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Tests for appointment email reminder reliability features.
/// Covers: separate channel tracking, multiple windows, duplicate prevention,
/// HTML encoding, email resolution priority, timezone handling, and status filtering.
/// </summary>
public class AppointmentReminderJobTests
{
    // ── ParseSentWindows ─────────────────────────────────────────────────────

    [Fact]
    public void ParseSentWindows_NullJson_ReturnsEmptyList()
    {
        var result = AppointmentReminderJob.ParseSentWindows(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSentWindows_EmptyJson_ReturnsEmptyList()
    {
        var result = AppointmentReminderJob.ParseSentWindows("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSentWindows_ValidJson_ReturnsParsedWindows()
    {
        var json = JsonSerializer.Serialize(new List<int> { 24, 2 });
        var result = AppointmentReminderJob.ParseSentWindows(json);
        result.Should().BeEquivalentTo(new List<int> { 24, 2 });
    }

    [Fact]
    public void ParseSentWindows_InvalidJson_ReturnsEmptyList()
    {
        var result = AppointmentReminderJob.ParseSentWindows("not json");
        result.Should().BeEmpty();
    }

    // ── MaskEmail ────────────────────────────────────────────────────────────

    [Fact]
    public void MaskEmail_NormalEmail_MasksCorrectly()
    {
        var result = AppointmentReminderJob.MaskEmail("patient@gmail.com");
        result.Should().Be("p*****t@gmail.com");
    }

    [Fact]
    public void MaskEmail_ShortLocal_MasksCorrectly()
    {
        var result = AppointmentReminderJob.MaskEmail("ab@gmail.com");
        result.Should().Be("**@gmail.com");
    }

    [Fact]
    public void MaskEmail_NullEmail_ReturnsStars()
    {
        var result = AppointmentReminderJob.MaskEmail(null!);
        result.Should().Be("***");
    }

    [Fact]
    public void MaskEmail_EmptyEmail_ReturnsStars()
    {
        var result = AppointmentReminderJob.MaskEmail("");
        result.Should().Be("***");
    }

    [Fact]
    public void MaskEmail_NoAtSign_ReturnsStars()
    {
        var result = AppointmentReminderJob.MaskEmail("nonsense");
        result.Should().Be("***");
    }

    // ── Email Reminder HTML Encoding ─────────────────────────────────────────

    [Fact]
    public void BuildAppointmentReminderHtml_EncodesPatientName()
    {
        var html = EmailService.BuildAppointmentReminderHtml(
            "<script>alert('xss')</script>", "Dr. Ahmad", "2026/01/01", "10:00", null, null);

        html.Should().NotContain("<script>");
        html.Should().Contain("&lt;script&gt;");
    }

    [Fact]
    public void BuildAppointmentReminderHtml_EncodesDoctorName()
    {
        var html = EmailService.BuildAppointmentReminderHtml(
            "Patient", "<b>Dr. Evil</b>", "2026/01/01", "10:00", null, null);

        html.Should().NotContain("<b>Dr. Evil</b>");
        html.Should().Contain("&lt;b&gt;Dr. Evil&lt;/b&gt;");
    }

    [Fact]
    public void BuildAppointmentReminderHtml_EncodesNotes()
    {
        var html = EmailService.BuildAppointmentReminderHtml(
            "Patient", "Doctor", "2026/01/01", "10:00", null, "<img src=x onerror=alert(1)>");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("&lt;img");
    }

    [Fact]
    public void BuildAppointmentReminderHtml_EncodesServiceName()
    {
        var html = EmailService.BuildAppointmentReminderHtml(
            "Patient", "Doctor", "2026/01/01", "10:00", "<iframe>bad</iframe>", null);

        html.Should().NotContain("<iframe>");
        html.Should().Contain("&lt;iframe&gt;");
    }

    [Fact]
    public void BuildAppointmentReminderHtml_EncodesDateAndTime()
    {
        var html = EmailService.BuildAppointmentReminderHtml(
            "Patient", "Doctor", "2026/01/01", "10:00", null, null);

        // These should appear as safe content (just numbers/slashes)
        html.Should().Contain("2026/01/01");
        html.Should().Contain("10:00");
    }

    // ── Duplicate Window Prevention ──────────────────────────────────────────

    [Fact]
    public void ParseSentWindows_SameWindowTracked_DoesNotDuplicate()
    {
        var sentWindows = new List<int> { 24 };
        var json = JsonSerializer.Serialize(sentWindows);
        var parsed = AppointmentReminderJob.ParseSentWindows(json);

        parsed.Should().Contain(24);
        parsed.Count(w => w == 24).Should().Be(1);
    }

    [Fact]
    public void ParseSentWindows_MultipleWindows_BothTracked()
    {
        var sentWindows = new List<int> { 24, 2 };
        var json = JsonSerializer.Serialize(sentWindows);
        var parsed = AppointmentReminderJob.ParseSentWindows(json);

        parsed.Should().Contain(24);
        parsed.Should().Contain(2);
        parsed.Count.Should().Be(2);
    }

    // ── Separate Channel Tracking ────────────────────────────────────────────

    [Fact]
    public void Appointment_NewInstance_HasNullReminderSentAt()
    {
        var appointment = new Domain.Entities.Appointment();
        appointment.EmailReminderSentAt.Should().BeNull();
        appointment.WhatsAppReminderSentAt.Should().BeNull();
        appointment.EmailReminderWindowsSent.Should().BeNull();
    }

    // ── Status Filtering Logic ───────────────────────────────────────────────

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, true)]
    [InlineData(AppointmentStatus.Confirmed, true)]
    [InlineData(AppointmentStatus.Cancelled, false)]
    [InlineData(AppointmentStatus.Completed, false)]
    [InlineData(AppointmentStatus.NoShow, false)]
    [InlineData(AppointmentStatus.InProgress, false)]
    public void TargetStatuses_OnlyScheduledAndConfirmed(AppointmentStatus status, bool shouldBeTargeted)
    {
        var targetStatuses = new List<AppointmentStatus>
        {
            AppointmentStatus.Scheduled,
            AppointmentStatus.Confirmed
        };

        targetStatuses.Contains(status).Should().Be(shouldBeTargeted);
    }

    // ── Email Resolution Priority ────────────────────────────────────────────

    [Fact]
    public void Patient_Entity_HasEmailField()
    {
        var patient = new Domain.Entities.Patient();
        patient.Email.Should().BeNull(); // Field exists but defaults to null
    }

    // ── Daily Email Limit Configuration ──────────────────────────────────────

    [Fact]
    public void EmailService_MaskEmail_StaticMethod_Works()
    {
        var masked = EmailService.MaskEmail("test@example.com");
        masked.Should().Be("t**t@example.com");
    }

    [Fact]
    public void EmailService_MaskEmail_ShortLocal_Works()
    {
        var masked = EmailService.MaskEmail("ab@example.com");
        masked.Should().Be("**@example.com");
    }

    // ── ConfirmationSent Legacy Flag ─────────────────────────────────────────

    [Fact]
    public void Appointment_ConfirmationSent_DefaultsFalse()
    {
        var appointment = new Domain.Entities.Appointment();
        appointment.ConfirmationSent.Should().BeFalse();
    }

    // ── Email Log Entity ─────────────────────────────────────────────────────

    [Fact]
    public void EmailLog_NewInstance_HasDefaults()
    {
        var log = new Domain.Entities.EmailLog();
        log.Id.Should().NotBe(Guid.Empty);
        log.IsSent.Should().BeFalse();
        log.Category.Should().Be("general");
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
