using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Application.Interfaces.Services;
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

namespace AqlanDentalPro.UnitTests.Journey;

public sealed class W05FutureAppointmentIntakeAuditTests
{
    private static readonly DateOnly BusinessDate = new(2026, 8, 1);
    private static readonly DateTime EventUtc = new(2026, 8, 1, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FutureAppointment_WithoutOverride_IsRejectedWithoutMutation()
    {
        await using var db = CreateDb();
        var appointment = SeedFutureAppointment(db);
        var service = CreateService(db, isAdmin: false);

        var result = await service.IntakeAsync(appointment.Id, new IntakeRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
        appointment.Status.Should().Be(AppointmentStatus.Scheduled);
        appointment.ArrivedAt.Should().BeNull();
        (await db.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AdminOverride_WithReason_ChangesStateAndPersistsAuditEvidence()
    {
        await using var db = CreateDb();
        var appointment = SeedFutureAppointment(db);
        var adminId = Guid.NewGuid();
        var service = CreateService(db, isAdmin: true, adminId);

        var result = await service.IntakeAsync(appointment.Id, new IntakeRequest
        {
            OverrideFutureAppointment = true,
            OverrideReason = "  المريض حضر من سفر بعيد وتمت الموافقة  "
        });

        result.Should().BeOfType<OkObjectResult>();
        appointment.Status.Should().Be(AppointmentStatus.Arrived);
        appointment.ArrivedAt.Should().Be(EventUtc);

        var audit = await db.AuditLogs.SingleAsync();
        audit.UserId.Should().Be(adminId);
        audit.Action.Should().Be(AuditAction.Approve);
        audit.Resource.Should().Be("FutureAppointmentJourneyOverride");
        audit.ResourceId.Should().Be(appointment.Id);
        audit.CreatedAt.Should().Be(EventUtc);
        audit.NewData.Should().NotBeNull();
        audit.NewData!.RootElement.GetProperty("operation").GetString().Should().Be("Intake");
        audit.NewData.RootElement.GetProperty("reason").GetString()
            .Should().Be("المريض حضر من سفر بعيد وتمت الموافقة");
        audit.NewData.RootElement.GetProperty("appointmentDate").GetString()
            .Should().Be("2026-08-02");
        audit.NewData.RootElement.GetProperty("businessDate").GetString()
            .Should().Be("2026-08-01");
        audit.NewData.RootElement.GetProperty("timeZone").GetString()
            .Should().Be("Asia/Aden");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static Appointment SeedFutureAppointment(AppDbContext db)
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "W05-001",
            FirstName = "مريض",
            LastName = "مستقبلي",
            IsActive = true
        };
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Patient = patient,
            AppointmentDate = BusinessDate.AddDays(1),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            Status = AppointmentStatus.Scheduled,
            AppointmentType = "Consultation",
            IsActive = true
        };
        db.Patients.Add(patient);
        db.Appointments.Add(appointment);
        db.SaveChanges();
        return appointment;
    }

    private static CheckoutService CreateService(
        AppDbContext db,
        bool isAdmin,
        Guid? userId = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAdmin).Returns(isAdmin);
        currentUser.SetupGet(x => x.Role).Returns(isAdmin ? UserRole.Admin : UserRole.Reception);
        currentUser.SetupGet(x => x.UserId).Returns(userId);

        var access = new Mock<IPatientAccessService>();
        access.Setup(x => x.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        var clock = new FixedClinicClock();
        return new CheckoutService(
            db,
            Mock.Of<ICommissionService>(),
            currentUser.Object,
            clock,
            new JourneyBusinessDatePolicy(clock),
            access.Object,
            Mock.Of<IRealTimePushService>(),
            NullLogger<CheckoutService>.Instance);
    }

    private sealed class FixedClinicClock : IClinicClock
    {
        public DateOnly Today() => BusinessDate;
        public DateTime UtcNow() => EventUtc;
        public DateTime ClinicNow() => EventUtc.AddHours(3);
        public DateOnly DateFromUtc(DateTime utc) => BusinessDate;
        public string TimeZoneId => "Asia/Aden";
    }
}
