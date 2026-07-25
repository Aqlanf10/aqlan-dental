using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

public class ActivePatientWriteGuardTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"active-patient-guard-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ExistsAsync_AcceptsAnActivePatient()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            PatientNumber = "PT-ACTIVE",
            FirstName = "Active",
            LastName = "Patient",
            IsActive = true,
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var exists = await ActivePatientWriteGuard.ExistsAsync(db, patient.Id);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_RejectsAnArchivedPatientThroughTheGlobalFilter()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            PatientNumber = "PT-ARCHIVED",
            FirstName = "Archived",
            LastName = "Patient",
            IsActive = false,
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var exists = await ActivePatientWriteGuard.ExistsAsync(db, patient.Id);

        exists.Should().BeFalse();
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task EnsureAsync_RejectsMissingOrEmptyPatientIds(string value)
    {
        await using var db = CreateDb();
        var patientId = Guid.Parse(value);

        var action = () => ActivePatientWriteGuard.EnsureAsync(db, patientId);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage(ActivePatientWriteGuard.ErrorMessage);
    }
}
