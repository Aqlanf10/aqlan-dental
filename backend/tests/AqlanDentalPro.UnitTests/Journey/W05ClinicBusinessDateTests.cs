using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Journey;

public sealed class W05ClinicBusinessDateTests
{
    private static readonly DateOnly BusinessDate = new(2026, 8, 1);

    [Theory]
    [InlineData(2026, 8, 1)]
    [InlineData(2026, 7, 31)]
    public void SameDayOrPastAppointment_IsAllowedWithoutOverride(int year, int month, int day)
    {
        var policy = new JourneyBusinessDatePolicy(new FixedClinicClock(BusinessDate));

        var decision = policy.Evaluate(
            new DateOnly(year, month, day),
            overrideRequested: false,
            overrideReason: null,
            canOverride: false);

        decision.Allowed.Should().BeTrue();
        decision.Overridden.Should().BeFalse();
        decision.BusinessDate.Should().Be(BusinessDate);
    }

    [Fact]
    public void TomorrowAppointment_IsBlockedWithoutOverride()
    {
        var policy = new JourneyBusinessDatePolicy(new FixedClinicClock(BusinessDate));

        var decision = policy.Evaluate(
            BusinessDate.AddDays(1),
            overrideRequested: false,
            overrideReason: null,
            canOverride: false);

        decision.Allowed.Should().BeFalse();
        decision.ErrorCode.Should().Be("future_appointment");
        decision.Message.Should().Contain("موعد مستقبلي");
    }

    [Fact]
    public void ReceptionCannotOverrideFutureAppointment()
    {
        var policy = new JourneyBusinessDatePolicy(new FixedClinicClock(BusinessDate));

        var decision = policy.Evaluate(
            BusinessDate.AddDays(1),
            overrideRequested: true,
            overrideReason: "المريض حضر من سفر بعيد",
            canOverride: false);

        decision.Allowed.Should().BeFalse();
        decision.ErrorCode.Should().Be("future_appointment_override_forbidden");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ManagerOverrideRequiresOperationalReason(string? reason)
    {
        var policy = new JourneyBusinessDatePolicy(new FixedClinicClock(BusinessDate));

        var decision = policy.Evaluate(
            BusinessDate.AddDays(1),
            overrideRequested: true,
            overrideReason: reason,
            canOverride: true);

        decision.Allowed.Should().BeFalse();
        decision.ErrorCode.Should().Be("future_appointment_override_reason_required");
    }

    [Fact]
    public void ManagerOverrideWithReason_IsAllowedAndNormalizedForAudit()
    {
        var policy = new JourneyBusinessDatePolicy(new FixedClinicClock(BusinessDate));

        var decision = policy.Evaluate(
            BusinessDate.AddDays(1),
            overrideRequested: true,
            overrideReason: "  موافقة المدير بسبب سفر المريض  ",
            canOverride: true);

        decision.Allowed.Should().BeTrue();
        decision.Overridden.Should().BeTrue();
        decision.OverrideReason.Should().Be("موافقة المدير بسبب سفر المريض");
    }

    [Fact]
    public void AsiaAdenMidnightBoundary_UsesClinicDateNotUtcDate()
    {
        var aden = ClinicTimeProvider.ResolveTimeZone("Asia/Aden");

        ClinicTimeProvider.ClinicDateFromUtc(
                new DateTime(2026, 7, 31, 20, 59, 59, DateTimeKind.Utc), aden)
            .Should().Be(new DateOnly(2026, 7, 31));

        ClinicTimeProvider.ClinicDateFromUtc(
                new DateTime(2026, 7, 31, 21, 0, 0, DateTimeKind.Utc), aden)
            .Should().Be(new DateOnly(2026, 8, 1));
    }

    private sealed class FixedClinicClock(DateOnly today) : IClinicClock
    {
        public DateOnly Today() => today;
        public DateTime UtcNow() => today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        public DateTime ClinicNow() => today.ToDateTime(TimeOnly.MinValue);
        public DateOnly DateFromUtc(DateTime utc) => today;
        public string TimeZoneId => "Asia/Aden";
    }
}

public sealed class W05JourneyGuardContractTests
{
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "backend")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root not found");
    }

    [Fact]
    public void ActiveJourneyServiceGuardsArrivalQueueAndVisitStart()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "backend/src/AqlanDentalPro.Infrastructure/Services/CheckoutService.cs"));

        source.Should().Contain("ValidateJourneyBusinessDate(appointment, req");
        source.Should().Contain("AddFutureAppointmentOverrideAudit(appointment, \"Intake\"");
        source.Should().Contain("AddFutureAppointmentOverrideAudit(appointment, \"SendToQueue\"");
        source.Should().Contain("AddFutureAppointmentOverrideAudit(appointment, \"StartVisit\"");
        source.Should().Contain("Resource = \"FutureAppointmentJourneyOverride\"");
    }

    [Fact]
    public void LegacyQueuePathsCannotBypassTheCanonicalGuard()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "backend/src/AqlanDentalPro.API/Controllers/ClinicQueueController.cs"));

        source.Should().Contain("AddFutureAppointmentOverrideAudit(appointment, \"LegacyAddToQueue\"");
        source.Should().Contain("AddFutureAppointmentOverrideAudit(appointment, \"LegacyMarkArrived\"");
        source.Should().NotContain("appointment.AppointmentDate != today");
    }
}
