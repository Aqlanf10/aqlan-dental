using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Patients;

/// <summary>
/// CORE-PAT-019: the patient profile summary and account-statement endpoint must
/// enforce the same owner-configurable RolePermissions used by PaymentsController.
/// Revoking a permission in Settings must close every route that exposes balances.
/// </summary>
public class PatientFinancePermissionEnforcementTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService User(UserRole role)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        mock.SetupGet(u => u.Role).Returns(role);
        mock.SetupGet(u => u.IsAdmin).Returns(role == UserRole.Admin);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    private static void GrantView(AppDbContext db, UserRole role, string resource)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role.ToString(),
            Resource = resource,
            CanView = true,
        });
        db.SaveChanges();
    }

    private static Guid SeedPatient(AppDbContext db)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-TEST-001",
            FirstName = "Test",
            LastName = "Patient",
            IsActive = true,
        };
        db.Patients.Add(patient);
        db.SaveChanges();
        return patient.Id;
    }

    private static (PatientsController controller, Mock<IFinanceReadService> financeRead) Build(
        AppDbContext db,
        ICurrentUserService user)
    {
        var financeRead = new Mock<IFinanceReadService>();
        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(p => p.IsDoctor).Returns(false);
        patientAccess.SetupGet(p => p.HasFullAccess).Returns(true);

        var controller = new PatientsController(
            service: null!,
            db: db,
            portalService: new Mock<IPatientPortalService>().Object,
            financeReadService: financeRead.Object,
            currentUser: user,
            patientAccess: patientAccess.Object,
            audit: new Mock<IAuditService>().Object,
            logger: NullLogger<PatientsController>.Instance);

        return (controller, financeRead);
    }

    private static object? PropertyValue(IActionResult result, string propertyName)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value!.GetType().GetProperty(propertyName)!.GetValue(ok.Value);
    }

    [Fact]
    public async Task Reception_WithoutPatientBalanceGrant_GetsNullFinanceTotals()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        var (controller, financeRead) = Build(db, User(UserRole.Reception));

        var result = await controller.GetSummary(patientId);

        PropertyValue(result, "totalPaid").Should().BeNull();
        PropertyValue(result, "totalOutstanding").Should().BeNull();
        financeRead.Verify(
            service => service.GetPatientFinanceSummaryAsync(It.IsAny<Guid>()),
            Times.Never,
            "revoking finance.patient_balance.view must stop the financial query itself");
    }

    [Fact]
    public async Task Reception_WithPatientBalanceGrant_GetsFinanceTotals()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        GrantView(db, UserRole.Reception, "finance.patient_balance");
        var (controller, financeRead) = Build(db, User(UserRole.Reception));
        financeRead.Setup(service => service.GetPatientFinanceSummaryAsync(patientId))
            .ReturnsAsync(new PatientFinanceSummaryDto
            {
                TotalPaid = 25_000m,
                OutstandingBalance = 10_000m,
                UnbilledVisitsAmount = 2_000m,
            });

        var result = await controller.GetSummary(patientId);

        PropertyValue(result, "totalPaid").Should().Be(25_000m);
        PropertyValue(result, "totalOutstanding").Should().Be(10_000m);
        PropertyValue(result, "unbilledVisitsAmount").Should().Be(2_000m);
        financeRead.Verify(service => service.GetPatientFinanceSummaryAsync(patientId), Times.Once);
    }

    [Fact]
    public async Task Reception_WithoutAccountStatementGrant_IsDeniedBeforeFinanceRead()
    {
        await using var db = CreateDb();
        var (controller, financeRead) = Build(db, User(UserRole.Reception));

        var result = await controller.GetAccountStatement(Guid.NewGuid());

        var denied = result.Should().BeOfType<ObjectResult>().Subject;
        denied.StatusCode.Should().Be(403);
        denied.Value!.GetType().GetProperty("message")!.GetValue(denied.Value)
            .Should().Be("غير مصرح لك بعرض البيانات المالية للمريض");
        financeRead.Verify(
            service => service.GetAccountStatementAsync(It.IsAny<Guid>()),
            Times.Never,
            "authorization must run before the detailed statement query");
    }

    [Fact]
    public async Task Accountant_WithAccountStatementGrant_CanReadStatement()
    {
        await using var db = CreateDb();
        var patientId = Guid.NewGuid();
        GrantView(db, UserRole.Accountant, "finance.account_statement");
        var (controller, financeRead) = Build(db, User(UserRole.Accountant));
        var statement = new AccountStatementDto
        {
            PatientId = patientId,
            PatientName = "Test Patient",
            PatientNumber = "GM-TEST-001",
            TotalPaid = 25_000m,
            TotalRemaining = 10_000m,
        };
        financeRead.Setup(service => service.GetAccountStatementAsync(patientId))
            .ReturnsAsync(statement);

        var result = await controller.GetAccountStatement(patientId);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(statement);
        financeRead.Verify(service => service.GetAccountStatementAsync(patientId), Times.Once);
    }
}
