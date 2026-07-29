using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Prescriptions;

/// <summary>
/// Codex P1 on #676 (spec 010): PatientAccessFilter only enforces access when a
/// patientId is present in the route/query, so a plain list request used to
/// return EVERY patient's prescriptions/radiology orders to any doctor. These
/// tests pin the fix: doctors' list queries are filtered by
/// GetAccessiblePatientIdsAsync(), and a failure to load the accessible set is
/// fail-closed (500) instead of silently leaking or hiding data.
/// </summary>
public class ClinicalListAccessTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateDoctorUser(Guid userId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(UserRole.GeneralDentist);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static IPatientAccessService CreateDoctorAccess(HashSet<Guid>? accessible, bool throws = false)
    {
        var mock = new Mock<IPatientAccessService>();
        mock.Setup(p => p.IsDoctor).Returns(true);
        mock.Setup(p => p.HasFullAccess).Returns(false);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => accessible != null && accessible.Contains(id));
        if (throws)
            mock.Setup(p => p.GetAccessiblePatientIdsAsync()).ThrowsAsync(new InvalidOperationException("db down"));
        else
            mock.Setup(p => p.GetAccessiblePatientIdsAsync()).ReturnsAsync(accessible);
        return mock.Object;
    }

    private static Patient MakePatient(string number) => new()
    {
        Id = Guid.NewGuid(),
        PatientNumber = number,
        FirstName = "مريض",
        LastName = number,
    };

    private static async Task<(AppDbContext db, Patient mine, Patient other)> SeedAsync()
    {
        var db = CreateDb();
        var mine = MakePatient("P-MINE");
        var other = MakePatient("P-OTHER");
        db.Patients.AddRange(mine, other);

        db.Prescriptions.AddRange(
            new Prescription { PatientId = mine.Id },
            new Prescription { PatientId = other.Id });
        db.RadiologyOrders.AddRange(
            new RadiologyOrder { PatientId = mine.Id, StudyType = "panoramic" },
            new RadiologyOrder { PatientId = other.Id, StudyType = "cbct_3d" });
        await db.SaveChangesAsync();
        return (db, mine, other);
    }

    private static int TotalOf(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var totalProp = ok.Value!.GetType().GetProperty("total");
        totalProp.Should().NotBeNull();
        return (int)totalProp!.GetValue(ok.Value)!;
    }

    // ── Prescriptions ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Prescriptions_ListWithoutPatientId_ReturnsOnlyAccessiblePatients()
    {
        var (db, mine, _) = await SeedAsync();
        await using var _db = db;
        var controller = new PrescriptionsController(
            db, CreateDoctorUser(Guid.NewGuid()), CreateDoctorAccess([mine.Id]),
            new Mock<IAuditService>().Object);

        var result = await controller.GetAll(patientId: null);

        TotalOf(result).Should().Be(1);
    }

    [Fact]
    public async Task Prescriptions_AccessibleSetFailure_IsFailClosed500()
    {
        var (db, _, _) = await SeedAsync();
        await using var _db = db;
        var controller = new PrescriptionsController(
            db, CreateDoctorUser(Guid.NewGuid()), CreateDoctorAccess(null, throws: true),
            new Mock<IAuditService>().Object);

        var result = await controller.GetAll(patientId: null);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    // ── Radiology orders ──────────────────────────────────────────────────────

    [Fact]
    public async Task RadiologyOrders_ListWithoutPatientId_ReturnsOnlyAccessiblePatients()
    {
        var (db, mine, _) = await SeedAsync();
        await using var _db = db;
        var controller = new RadiologyOrdersController(
            db, CreateDoctorUser(Guid.NewGuid()), CreateDoctorAccess([mine.Id]),
            new Mock<IAuditService>().Object);

        var result = await controller.GetAll(patientId: null);

        TotalOf(result).Should().Be(1);
    }

    [Fact]
    public async Task RadiologyOrders_AccessibleSetFailure_IsFailClosed500()
    {
        var (db, _, _) = await SeedAsync();
        await using var _db = db;
        var controller = new RadiologyOrdersController(
            db, CreateDoctorUser(Guid.NewGuid()), CreateDoctorAccess(null, throws: true),
            new Mock<IAuditService>().Object);

        var result = await controller.GetAll(patientId: null);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task RadiologyOrders_NonDoctorRoles_SeeAllOrders()
    {
        var (db, _, _) = await SeedAsync();
        await using var _db = db;
        var access = new Mock<IPatientAccessService>();
        access.Setup(p => p.IsDoctor).Returns(false);
        var admin = new Mock<ICurrentUserService>();
        admin.Setup(u => u.Role).Returns(UserRole.Admin);
        admin.Setup(u => u.IsAdmin).Returns(true);
        var controller = new RadiologyOrdersController(
            db, admin.Object, access.Object, new Mock<IAuditService>().Object);

        var result = await controller.GetAll(patientId: null);

        TotalOf(result).Should().Be(2);
    }
}
