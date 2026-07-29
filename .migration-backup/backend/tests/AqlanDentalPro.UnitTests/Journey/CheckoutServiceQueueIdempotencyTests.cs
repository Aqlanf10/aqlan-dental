using AqlanDentalPro.API.Hubs;
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

public class CheckoutServiceQueueIdempotencyTests
{
    [Fact]
    public async Task SendToQueue_ReusesLegacyWaitingRow_AndAdvancesArrivedAppointment()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"queue-idempotency-{Guid.NewGuid()}")
                .Options);

        var patient = new Patient
        {
            PatientNumber = "GM-QUEUE-001",
            FirstName = "مريض",
            LastName = "اختبار"
        };
        var doctor = new Doctor { Name = "د. اختبار", Specialty = "عام" };
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = ClinicTimeProvider.ClinicToday(),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            DurationMinutes = 30,
            AppointmentType = "معاينة",
            Status = AppointmentStatus.Arrived,
            IsActive = true
        };
        var legacyQueueItem = new ClinicQueueItem
        {
            PatientId = patient.Id,
            AppointmentId = appointment.Id,
            DoctorId = doctor.Id,
            QueueDate = ClinicTimeProvider.ClinicToday(),
            Status = ClinicQueueStatus.Waiting,
            IsActive = true
        };

        db.AddRange(patient, doctor, appointment, legacyQueueItem);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.HasFullAccess).Returns(true);
        var push = new Mock<IRealTimePushService>();
        var service = new CheckoutService(
            db,
            new Mock<ICommissionService>().Object,
            currentUser.Object,
            access.Object,
            push.Object,
            NullLogger<CheckoutService>.Instance);

        var result = await service.SendToQueueAsync(appointment.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await db.Appointments.SingleAsync(x => x.Id == appointment.Id))
            .Status.Should().Be(AppointmentStatus.Waiting);
        (await db.ClinicQueueItems.CountAsync(x => x.AppointmentId == appointment.Id))
            .Should().Be(1, "the legacy queue row must be reused instead of duplicated");
        push.Verify(
            x => x.PushToAllAsync(
                MessagingHubEvents.JourneyUpdated,
                It.IsAny<object>()),
            Times.Once);
    }
}
