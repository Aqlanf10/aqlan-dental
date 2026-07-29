using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Common;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

/// <summary>
/// PatientAge.Calculate — pure logic, had no direct tests despite being the
/// single source of truth introduced by CORE-PAT-003 to replace three
/// disagreeing implementations.
/// </summary>
public class PatientAgeTests
{
    [Fact]
    public void Calculate_BirthdayAlreadyPassedThisYear_ReturnsCompletedYears()
    {
        PatientAge.Calculate(new DateOnly(2000, 1, 15), new DateOnly(2026, 6, 1))
            .Should().Be(26);
    }

    [Fact]
    public void Calculate_BirthdayNotYetReachedThisYear_ReturnsOneLess()
    {
        // The old buggy formula (today.Year - dob.Year) would say 26 here —
        // the whole reason CORE-PAT-003 exists.
        PatientAge.Calculate(new DateOnly(2000, 12, 15), new DateOnly(2026, 6, 1))
            .Should().Be(25);
    }

    [Fact]
    public void Calculate_ExactlyOnBirthday_CountsTheYearAsComplete()
    {
        PatientAge.Calculate(new DateOnly(2000, 6, 1), new DateOnly(2026, 6, 1))
            .Should().Be(26);
    }

    [Fact]
    public void Calculate_UnknownDateOfBirth_ReturnsNull()
    {
        PatientAge.Calculate(null, new DateOnly(2026, 6, 1)).Should().BeNull();
    }

    [Fact]
    public void Calculate_DateOfBirthInTheFuture_ReturnsNullInsteadOfNegative()
    {
        PatientAge.Calculate(new DateOnly(2030, 1, 1), new DateOnly(2026, 6, 1)).Should().BeNull();
    }
}

/// <summary>
/// CORE-PAT-048: PatientService's age computation must use the injected
/// IClinicClock (the clinic's local day), not DateTime.UtcNow — Yemen is
/// UTC+3, so a patient whose birthday is "today" in clinic time but not yet
/// in UTC time used to show one year younger for a ~3-hour window every year.
/// </summary>
public class PatientServiceClinicDayAgeTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PatientService BuildService(AppDbContext db, DateOnly clinicToday)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.IsAdmin).Returns(true);
        var patientSettings = new Mock<IPatientSettingsReader>();
        patientSettings.Setup(s => s.GetNumberPrefixAsync(It.IsAny<CancellationToken>())).ReturnsAsync("GM");
        var portal = new Mock<IPatientPortalService>();
        var clinicClock = new Mock<IClinicClock>();
        clinicClock.Setup(c => c.Today()).Returns(clinicToday);

        return new PatientService(
            new PatientRepository(db), currentUser.Object, patientSettings.Object,
            portal.Object, clinicClock.Object, new Mock<ILogger<PatientService>>().Object);
    }

    [Fact]
    public async Task GetByIdAsync_UsesInjectedClinicClock_NotRealUtcNow()
    {
        // The patient turns 18 on clinicToday exactly. If the mapping used
        // DateTime.UtcNow instead of the injected clock, this test would be
        // flaky (correct only when run on/after 2008-03-10 in real UTC time,
        // which is always true today — so instead we pin clinicToday far in
        // the future, where the real UTC "today" is provably earlier, and
        // assert the OLDER, injected date wins).
        var clinicToday = new DateOnly(2099, 3, 10);
        await using var db = CreateDb();
        var patient = new Patient
        {
            FirstName = "خالد", LastName = "ناصر", PatientNumber = "GM-2026-900",
            DateOfBirth = new DateOnly(2081, 3, 10),
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var service = BuildService(db, clinicToday);
        var dto = await service.GetByIdAsync(patient.Id);

        dto.Should().NotBeNull();
        dto!.Age.Should().Be(18, "age must be computed against the injected clinic clock, not real UTC now");
    }

    [Fact]
    public async Task GetListAsync_UsesInjectedClinicClock_NotRealUtcNow()
    {
        var clinicToday = new DateOnly(2099, 3, 10);
        await using var db = CreateDb();
        var patient = new Patient
        {
            FirstName = "منى", LastName = "سالم", PatientNumber = "GM-2026-901",
            DateOfBirth = new DateOnly(2081, 3, 11), // one day AFTER clinicToday's month/day → birthday not yet reached
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var service = BuildService(db, clinicToday);
        var result = await service.GetListAsync(search: null, page: 1, pageSize: 20);

        result.Data.Should().ContainSingle().Which.Age.Should().Be(
            17, "birthday hasn't occurred yet relative to the injected clinic day");
    }
}
