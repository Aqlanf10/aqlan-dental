using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class OrthoDailyOperationsIntegrationTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetToday_ActiveOrthoCase_ReturnsOperationalSummaryAndBadge()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db);
        var orthoCase = new OrthoCase
        {
            PatientId = seeded.Patient.Id,
            CaseNumber = "OR-TEST-001",
            CurrentStage = "Alignment",
            Status = OrthoCaseStatus.Active,
            DoctorId = seeded.Doctor.Id,
            IsActive = true
        };
        seeded.Appointment.OrthoCaseId = orthoCase.Id;
        db.OrthoCases.Add(orthoCase);
        db.OrthoVisits.Add(new OrthoVisit
        {
            OrthoCaseId = orthoCase.Id,
            VisitNumber = 1,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7),
            NextAppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14),
            IsActive = true
        });
        var contract = new Contract
        {
            PatientId = seeded.Patient.Id,
            RelatedCaseId = orthoCase.Id,
            TotalAmount = 1000m,
            DiscountAmount = 100m,
            IsActive = true
        };
        db.Contracts.Add(contract);
        db.Payments.Add(new Payment
        {
            PatientId = seeded.Patient.Id,
            ContractId = contract.Id,
            Amount = 200m,
            PaymentMethod = "cash",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItemAsync(db, BuildFullAccess());

        Get<bool>(item, "HasActiveOrthoCase").Should().BeTrue();
        Get<Guid?>(item, "OrthoCaseId").Should().Be(orthoCase.Id);
        Get<string>(item, "OrthoCaseNumber").Should().Be("OR-TEST-001");
        Get<string>(item, "OrthoCurrentStage").Should().Be("Alignment");
        Get<decimal?>(item, "OrthoContractRemaining").Should().Be(700m);
    }

    [Fact]
    public async Task GetToday_NonOrthoPatient_RemainsUnaffected()
    {
        await using var db = CreateDb();
        SeedAppointment(db);
        await db.SaveChangesAsync();

        var item = await GetSingleJourneyItemAsync(db, BuildFullAccess());

        Get<bool>(item, "HasActiveOrthoCase").Should().BeFalse();
        Get<Guid?>(item, "OrthoCaseId").Should().BeNull();
        Get<string?>(item, "OrthoCurrentStage").Should().BeNull();
    }

    [Fact]
    public async Task GetToday_DoctorWithoutPatientAccess_DoesNotReceiveOrthoData()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db);
        db.OrthoCases.Add(new OrthoCase
        {
            PatientId = seeded.Patient.Id,
            CaseNumber = "OR-PRIVATE",
            Status = OrthoCaseStatus.Active,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.IsDoctor).Returns(true);
        access.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync([]);

        var result = await BuildJourneyController(db, access.Object)
            .GetToday(null, null, null, null, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<object>>()
            .Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateNextAppointment_DefaultsToTwentyOneDays_AndLinksOrthoCase()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db);
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var orthoCase = new OrthoCase
        {
            PatientId = seeded.Patient.Id,
            DoctorId = seeded.Doctor.Id,
            CaseNumber = "OR-FOLLOW-UP",
            Status = OrthoCaseStatus.Active,
            IsActive = true
        };
        var visit = new OrthoVisit
        {
            OrthoCaseId = orthoCase.Id,
            VisitNumber = 1,
            VisitDate = visitDate,
            DoctorId = seeded.Doctor.Id,
            IsActive = true
        };
        db.OrthoCases.Add(orthoCase);
        db.OrthoVisits.Add(visit);
        await db.SaveChangesAsync();

        var controller = BuildOrthoController(db, canAccess: true);
        var result = await controller.CreateNextAppointment(
            orthoCase.Id,
            visit.Id,
            new CreateOrthoFollowUpAppointmentRequest());

        result.Should().BeOfType<OkObjectResult>();
        var appointment = await db.Appointments.SingleAsync(a => a.OrthoCaseId == orthoCase.Id);
        appointment.PatientId.Should().Be(seeded.Patient.Id);
        appointment.AppointmentDate.Should().Be(visitDate.AddDays(21));
        appointment.Specialty.Should().Be(Specialty.Orthodontics);
        visit.NextAppointmentDate.Should().Be(appointment.AppointmentDate);
    }

    [Fact]
    public async Task CreateNextAppointment_UnauthorizedUser_IsForbiddenWithoutWrite()
    {
        await using var db = CreateDb();
        var seeded = SeedAppointment(db);
        var orthoCase = new OrthoCase
        {
            PatientId = seeded.Patient.Id,
            DoctorId = seeded.Doctor.Id,
            CaseNumber = "OR-DENIED",
            Status = OrthoCaseStatus.Active,
            IsActive = true
        };
        var visit = new OrthoVisit
        {
            OrthoCaseId = orthoCase.Id,
            VisitNumber = 1,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            DoctorId = seeded.Doctor.Id,
            IsActive = true
        };
        db.OrthoCases.Add(orthoCase);
        db.OrthoVisits.Add(visit);
        await db.SaveChangesAsync();

        var result = await BuildOrthoController(db, canAccess: false)
            .CreateNextAppointment(orthoCase.Id, visit.Id, new CreateOrthoFollowUpAppointmentRequest());

        result.Should().BeOfType<ForbidResult>();
        (await db.Appointments.CountAsync(a => a.OrthoCaseId == orthoCase.Id)).Should().Be(0);
    }

    private static async Task<object> GetSingleJourneyItemAsync(
        AppDbContext db,
        IPatientAccessService access)
    {
        var result = await BuildJourneyController(db, access)
            .GetToday(null, null, null, null, null);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeAssignableTo<IEnumerable<object>>()
            .Subject.Should().ContainSingle().Subject;
    }

    private static PatientJourneyController BuildJourneyController(
        AppDbContext db,
        IPatientAccessService access)
    {
        // CLIN-22: PatientJourneyController now delegates to PatientJourneyService +
        // CheckoutService. Build real service instances against the InMemory db so
        // the GetToday aggregate path executes end-to-end.
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns((Guid?)null);
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);

        var commission = new Mock<ICommissionService>();
        var finance = new Mock<IFinanceService>();
        var audit = new Mock<IAuditService>();

        var journeyService = new PatientJourneyService(
            db, access, finance.Object,
            NullLogger<PatientJourneyService>.Instance);
        var checkoutService = new CheckoutService(
            db, commission.Object, currentUser.Object, access,
            new Mock<IRealTimePushService>().Object,
            NullLogger<CheckoutService>.Instance);

        return new PatientJourneyController(journeyService, checkoutService);
    }

    private static OrthoCasesController BuildOrthoController(AppDbContext db, bool canAccess)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);

        var access = new Mock<IPatientAccessService>();
        access.Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(canAccess);

        var audit = new Mock<IAuditService>();

        return new OrthoCasesController(
            new OrthoService(db, currentUser.Object),
            db,
            currentUser.Object,
            access.Object,
            audit.Object);
    }

    private static IPatientAccessService BuildFullAccess()
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.IsDoctor).Returns(false);
        access.SetupGet(x => x.HasFullAccess).Returns(true);
        access.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync((HashSet<Guid>?)null);
        return access.Object;
    }

    private static (Patient Patient, Doctor Doctor, Appointment Appointment) SeedAppointment(AppDbContext db)
    {
        var user = new User
        {
            Username = Guid.NewGuid().ToString("N"),
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.Orthodontist,
            IsActive = true
        };
        var doctor = new Doctor
        {
            UserId = user.Id,
            User = user,
            Name = "Ortho Doctor",
            IsActive = true
        };
        var patient = new Patient
        {
            PatientNumber = $"P-{Guid.NewGuid():N}",
            FirstName = "Ortho",
            LastName = "Patient",
            IsActive = true
        };
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            Patient = patient,
            DoctorId = doctor.Id,
            Doctor = doctor,
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(8, 30),
            AppointmentType = "OrthoFollowUp",
            Status = AppointmentStatus.Scheduled,
            IsActive = true
        };

        db.AddRange(user, doctor, patient, appointment);
        return (patient, doctor, appointment);
    }

    private static T Get<T>(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"journey item should include {propertyName}");
        return (T)property!.GetValue(item)!;
    }
}
