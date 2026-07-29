using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

/// <summary>
/// SEQ-13: configuration resolution is tested without mutating the process-wide
/// timezone, so parallel tests cannot leak state into one another.
/// </summary>
public class ClinicTimeProviderConfigurationTests
{
    [Fact]
    public void ResolveTimeZone_MissingValue_UsesAsiaAden()
    {
        var zone = ClinicTimeProvider.ResolveTimeZone(null);

        zone.Id.Should().Be(ClinicTimeProvider.DefaultTimeZoneId);
    }

    [Fact]
    public void ResolveTimeZone_ValidAlternate_ReturnsRequestedZone()
    {
        var zone = ClinicTimeProvider.ResolveTimeZone("Asia/Tokyo");

        zone.Id.Should().Be("Asia/Tokyo");
    }

    [Fact]
    public void ResolveTimeZone_InvalidValue_FallsBackAndWarns()
    {
        string? warning = null;

        var zone = ClinicTimeProvider.ResolveTimeZone(
            "Invalid/Clinic-Zone",
            message => warning = message);

        zone.Id.Should().Be(ClinicTimeProvider.DefaultTimeZoneId);
        warning.Should().Contain("Invalid/Clinic-Zone");
        warning.Should().Contain(ClinicTimeProvider.DefaultTimeZoneId);
    }

    [Fact]
    public void ToUtcRange_ExplicitTokyoZone_CoversCorrectUtcWindow()
    {
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        var clinicDate = new DateOnly(2026, 7, 16);

        var (start, end) = ClinicTimeProvider.ToUtcRange(clinicDate, tokyo);

        start.Should().Be(new DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 7, 16, 15, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ExplicitZones_InParallel_DoNotMutateConfiguredState()
    {
        var before = ClinicTimeProvider.CurrentTimeZone.Id;
        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        var aden = TimeZoneInfo.FindSystemTimeZoneById("Asia/Aden");
        var date = new DateOnly(2026, 7, 16);

        await Task.WhenAll(
            Task.Run(() => ClinicTimeProvider.ToUtcRange(date, tokyo)),
            Task.Run(() => ClinicTimeProvider.ToUtcRange(date, aden)),
            Task.Run(() => ClinicTimeProvider.ResolveTimeZone("UTC")));

        ClinicTimeProvider.CurrentTimeZone.Id.Should().Be(before);
    }
}
