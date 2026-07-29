using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class PatientSettingsReaderTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"patient-settings-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetNumberPrefixAsync_NoStoredValue_ReturnsGM()
    {
        await using var db = CreateDb();
        var reader = new PatientSettingsReader(db);

        (await reader.GetNumberPrefixAsync()).Should().Be("GM");
    }

    [Fact]
    public async Task GetNumberPrefixAsync_UsesTheAdminSetting()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = PatientSettingsReader.NumberPrefixKey,
            Value = "adc",
            Category = "patients",
        });
        await db.SaveChangesAsync();

        var reader = new PatientSettingsReader(db);

        (await reader.GetNumberPrefixAsync()).Should().Be("ADC");
    }

    [Theory]
    [InlineData(" A-C 10 ", "AC10")]
    [InlineData("--------", "GM")]
    [InlineData("LONGPREFIX123", "LONGPREF")]
    public void NormalizePrefix_ProducesAStableSafePrefix(string value, string expected)
    {
        PatientSettingsReader.NormalizePrefix(value).Should().Be(expected);
    }
}
