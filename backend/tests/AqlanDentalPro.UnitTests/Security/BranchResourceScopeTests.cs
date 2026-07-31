using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Security;

public class BranchResourceScopeTests
{
    [Fact]
    public void NonAdmin_CanAccessOnlyTheirBranch()
    {
        var ownBranch = Guid.NewGuid();
        var otherBranch = Guid.NewGuid();
        var currentUser = CreateUser(UserRole.Reception, ownBranch);
        var scope = new BranchResourceScope(currentUser.Object);

        scope.HasGlobalAccess.Should().BeFalse();
        scope.EffectiveBranchId.Should().Be(ownBranch);
        scope.CanAccess(ownBranch).Should().BeTrue();
        scope.CanAccess(otherBranch).Should().BeFalse();
        scope.CanAccess(null).Should().BeFalse();
    }

    [Fact]
    public void NonAdminWithoutBranch_FailsClosed()
    {
        var currentUser = CreateUser(UserRole.Reception, null);
        var scope = new BranchResourceScope(currentUser.Object);

        scope.EffectiveBranchId.Should().BeNull();
        scope.CanAccess(Guid.NewGuid()).Should().BeFalse();
        scope.ResolveWriteBranch().Should().BeNull();
    }

    [Fact]
    public async Task PatientList_NonAdminWithoutBranch_ReturnsEmptyWithoutQueryingRepository()
    {
        var currentUser = CreateUser(UserRole.Reception, null);
        var repo = new Mock<IPatientRepository>(MockBehavior.Strict);
        var service = new PatientService(
            repo.Object,
            currentUser.Object,
            new BranchResourceScope(currentUser.Object),
            Mock.Of<IPatientSettingsReader>(),
            Mock.Of<IPatientPortalService>(),
            Mock.Of<IClinicClock>(),
            NullLogger<PatientService>.Instance);

        var result = await service.GetListAsync(null, 1, 20);

        result.Data.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AppointmentList_NonAdminWithoutBranch_ReturnsEmptyWithoutQueryingRepository()
    {
        var currentUser = CreateUser(UserRole.Reception, null);
        var repo = new Mock<IAppointmentRepository>(MockBehavior.Strict);
        var service = new AppointmentService(
            repo.Object,
            currentUser.Object,
            new BranchResourceScope(currentUser.Object),
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<AppointmentService>.Instance);

        var result = await service.GetTodayAsync();

        result.Should().BeEmpty();
        repo.VerifyNoOtherCalls();
    }

    [Fact]
    public void AuthenticatedAdmin_HasExplicitGlobalScope()
    {
        var currentUser = CreateUser(UserRole.Admin, null);
        var scope = new BranchResourceScope(currentUser.Object);

        scope.HasGlobalAccess.Should().BeTrue();
        scope.EffectiveBranchId.Should().BeNull();
        scope.CanAccess(Guid.NewGuid()).Should().BeTrue();
    }

    [Fact]
    public async Task PatientService_DirectIdFromOtherBranch_ReturnsNotFound()
    {
        var ownBranch = Guid.NewGuid();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "QA-100",
            FirstName = "QA",
            LastName = "Patient",
            BranchId = Guid.NewGuid()
        };
        var repo = new Mock<IPatientRepository>();
        repo.Setup(repository => repository.GetWithHistoriesAsync(patient.Id)).ReturnsAsync(patient);
        var currentUser = CreateUser(UserRole.Reception, ownBranch);

        var service = new PatientService(
            repo.Object,
            currentUser.Object,
            new BranchResourceScope(currentUser.Object),
            Mock.Of<IPatientSettingsReader>(),
            Mock.Of<IPatientPortalService>(),
            Mock.Of<IClinicClock>(),
            NullLogger<PatientService>.Instance);

        (await service.GetByIdAsync(patient.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AppointmentService_DirectIdFromOtherBranch_ReturnsNotFound()
    {
        var ownBranch = Guid.NewGuid();
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            AppointmentType = "QA"
        };
        var repo = new Mock<IAppointmentRepository>();
        repo.Setup(repository => repository.GetWithDetailAsync(appointment.Id)).ReturnsAsync(appointment);
        var currentUser = CreateUser(UserRole.Reception, ownBranch);

        var service = new AppointmentService(
            repo.Object,
            currentUser.Object,
            new BranchResourceScope(currentUser.Object),
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<AppointmentService>.Instance);

        (await service.GetByIdAsync(appointment.Id)).Should().BeNull();
    }

    [Fact]
    public async Task PaymentService_DirectIdFromOtherBranch_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var ownBranch = Guid.NewGuid();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            Amount = 100,
            PaymentMethod = "cash",
            Currency = "YER"
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        var currentUser = CreateUser(UserRole.Accountant, ownBranch);
        var service = new PaymentService(
            db,
            currentUser.Object,
            new BranchResourceScope(currentUser.Object),
            Mock.Of<INotificationService>(),
            NullLogger<PaymentService>.Instance,
            Mock.Of<ICommissionService>(),
            Mock.Of<IJournalEntryService>());

        (await service.GetPaymentByIdAsync(payment.Id)).Should().BeNull();
    }

    [Fact]
    public async Task LabQuery_DirectIdFromOtherBranch_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var ownBranch = Guid.NewGuid();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "QA-200",
            FirstName = "QA",
            LastName = "Lab",
            BranchId = Guid.NewGuid()
        };
        var order = new LabOrder
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Patient = patient,
            BranchId = patient.BranchId,
            OrderNumber = "QA-LAB-1",
            ApplianceType = "QA",
            Status = "draft"
        };
        db.AddRange(patient, order);
        await db.SaveChangesAsync();
        var currentUser = CreateUser(UserRole.Reception, ownBranch);
        var service = new LabOrderQueryService(
            db,
            new BranchResourceScope(currentUser.Object),
            NullLogger<LabOrderQueryService>.Instance);

        (await service.GetByIdAsync(order.Id)).Should().BeNull();
    }

    private static Mock<ICurrentUserService> CreateUser(UserRole role, Guid? branchId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        currentUser.SetupGet(user => user.Role).Returns(role);
        currentUser.SetupGet(user => user.IsAdmin).Returns(role == UserRole.Admin);
        currentUser.SetupGet(user => user.BranchId).Returns(branchId);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        return currentUser;
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"branch-scope-{Guid.NewGuid()}")
            .Options);
}
