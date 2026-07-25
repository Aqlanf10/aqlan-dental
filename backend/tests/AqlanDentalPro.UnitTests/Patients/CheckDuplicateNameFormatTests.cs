using System.Linq;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

/// <summary>
/// CORE-PAT-047: PatientsController.CheckDuplicate built FullName as
/// `FirstName + " " + MiddleName + " " + LastName` with no null guard, so any
/// patient with an empty MiddleName (the common case) showed a double space
/// in the duplicate-warning dialog reception sees at registration — a small
/// but visible correctness bug in a screen shown on every new-patient save.
/// </summary>
public class CheckDuplicateNameFormatTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static PatientsController BuildController(AppDbContext db)
    {
        return new PatientsController(
            service: null!,
            db: db,
            financeReadService: null!,
            currentUser: new Mock<ICurrentUserService>().Object,
            patientAccess: new Mock<IPatientAccessService>().Object,
            audit: new Mock<IAuditService>().Object,
            logger: new Mock<ILogger<PatientsController>>().Object);
    }

    [Fact]
    public async Task CheckDuplicate_PatientWithNoMiddleName_FullNameHasNoDoubleSpace()
    {
        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            FirstName = "أحمد",
            MiddleName = null,
            LastName = "علي",
            PatientNumber = "P-100",
            NormalizedPhone = "967770000001",
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.CheckDuplicate(
            phone: "770000001", whatsApp: null, patientNumber: null,
            firstName: null, lastName: null, dateOfBirth: null, excludeId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        var matchesProp = value.GetType().GetProperty("matches");
        var matches = (System.Collections.IEnumerable)matchesProp!.GetValue(value)!;
        var match = matches.Cast<object>().Single();
        var fullName = (string)match.GetType().GetProperty("FullName")!.GetValue(match)!;

        fullName.Should().Be("أحمد علي");
        fullName.Should().NotContain("  ", "an empty MiddleName must not leave a double space");
    }

    [Fact]
    public async Task CheckDuplicate_PatientWithMiddleName_FullNameIncludesIt()
    {
        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            FirstName = "سارة",
            MiddleName = "محمد",
            LastName = "قاسم",
            PatientNumber = "P-101",
            NormalizedPhone = "967770000002",
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.CheckDuplicate(
            phone: "770000002", whatsApp: null, patientNumber: null,
            firstName: null, lastName: null, dateOfBirth: null, excludeId: null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        var matches = (System.Collections.IEnumerable)value.GetType().GetProperty("matches")!.GetValue(value)!;
        var match = matches.Cast<object>().Single();
        var fullName = (string)match.GetType().GetProperty("FullName")!.GetValue(match)!;

        fullName.Should().Be("سارة محمد قاسم");
    }
}
