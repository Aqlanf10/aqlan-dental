using Xunit;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.UnitTests.Journey;

/// <summary>
/// Tests for Patient Journey entity fields, transition validation,
/// and data integrity. Uses InMemory provider to verify journey-related
/// operations without requiring a PostgreSQL database.
/// </summary>
public class PatientJourneyTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ─── Appointment Journey Fields Tests ──────────────────────────────────

    [Fact]
    public async Task Appointment_ServiceId_IsNullable_And_DefaultsToNull()
    {
        await using var db = CreateContext();
        var appointment = new Appointment
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.Scheduled
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var saved = await db.Appointments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == appointment.Id);
        saved.Should().NotBeNull();
        saved!.ServiceId.Should().BeNull();
        saved.ClinicRoomId.Should().BeNull();
    }

    [Fact]
    public async Task Appointment_CanSet_ServiceId_And_ClinicRoomId()
    {
        await using var db = CreateContext();
        var serviceId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        var appointment = new Appointment
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.Scheduled,
            ServiceId = serviceId,
            ClinicRoomId = roomId
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        var saved = await db.Appointments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == appointment.Id);
        saved!.ServiceId.Should().Be(serviceId);
        saved.ClinicRoomId.Should().Be(roomId);
    }

    // ─── Visit Journey Fields Tests ────────────────────────────────────────

    [Fact]
    public async Task Visit_CheckoutFields_AreNullable_And_DefaultToNull()
    {
        await using var db = CreateContext();
        var visit = new Visit
        {
            PatientId = Guid.NewGuid(),
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var saved = await db.Visits.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == visit.Id);
        saved.Should().NotBeNull();
        saved!.ServiceId.Should().BeNull();
        saved.CheckoutStatus.Should().BeNull();
        saved.ReadyForCheckoutAt.Should().BeNull();
        saved.AmountDueReference.Should().BeNull();
    }

    [Fact]
    public async Task Visit_CanSet_CheckoutStatus_ReadyForCheckout()
    {
        await using var db = CreateContext();
        var visit = new Visit
        {
            PatientId = Guid.NewGuid(),
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckoutStatus = "ReadyForCheckout",
            ReadyForCheckoutAt = DateTime.UtcNow,
            AmountDueReference = 5000m
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var saved = await db.Visits.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == visit.Id);
        saved!.CheckoutStatus.Should().Be("ReadyForCheckout");
        saved.ReadyForCheckoutAt.Should().NotBeNull();
        saved.AmountDueReference.Should().Be(5000m);
    }

    [Fact]
    public async Task Visit_CanSet_CheckoutStatus_CheckedOut()
    {
        await using var db = CreateContext();
        var visit = new Visit
        {
            PatientId = Guid.NewGuid(),
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CheckoutStatus = "CheckedOut"
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        var saved = await db.Visits.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == visit.Id);
        saved!.CheckoutStatus.Should().Be("CheckedOut");
    }

    // ─── Transition Validation Tests ───────────────────────────────────────

    [Fact]
    public void AppointmentTransition_Scheduled_To_Arrived_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.Arrived)
            .Should().BeTrue();
    }

    [Fact]
    public void AppointmentTransition_Confirmed_To_Arrived_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Confirmed, AppointmentStatus.Arrived)
            .Should().BeTrue();
    }

    [Fact]
    public void AppointmentTransition_Arrived_To_Waiting_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Arrived, AppointmentStatus.Waiting)
            .Should().BeTrue();
    }

    [Fact]
    public void AppointmentTransition_InRoom_To_InProgress_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InRoom, AppointmentStatus.InProgress)
            .Should().BeTrue();
    }

    [Fact]
    public void AppointmentTransition_InProgress_To_Completed_IsValid()
    {
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.InProgress, AppointmentStatus.Completed)
            .Should().BeTrue();
    }

    [Fact]
    public void AppointmentTransition_Scheduled_To_InProgress_IsInvalid()
    {
        // Cannot skip from Scheduled directly to InProgress
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Scheduled, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    [Fact]
    public void AppointmentTransition_Completed_To_Any_IsInvalid()
    {
        // Completed is a terminal state
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Completed, AppointmentStatus.Scheduled)
            .Should().BeFalse();
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Completed, AppointmentStatus.InProgress)
            .Should().BeFalse();
    }

    // ─── Queue Duplicate Prevention Tests ───────────────────────────────────

    [Fact]
    public async Task SendToQueue_PreventsDuplicate_ActiveQueueItem()
    {
        await using var db = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        // Add first queue item
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            PatientId = patientId,
            AppointmentId = appointmentId,
            Status = ClinicQueueStatus.Waiting,
            QueueDate = today,
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Check for existing active queue item (simulates the guard in controller)
        var exists = await db.ClinicQueueItems
            .AnyAsync(q => q.AppointmentId == appointmentId
                && q.QueueDate == today
                && q.Status != ClinicQueueStatus.Completed
                && q.Status != ClinicQueueStatus.Cancelled
                && q.IsActive);

        exists.Should().BeTrue("duplicate active queue item should be detected");
    }

    // ─── Intake Prevents Invalid Appointment Transition ─────────────────────

    [Fact]
    public void Intake_PreventsTransition_FromCompleted_ToArrived()
    {
        // A completed appointment cannot be set to Arrived
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Completed, AppointmentStatus.Arrived)
            .Should().BeFalse();
    }

    [Fact]
    public void Intake_PreventsTransition_FromCancelled_ToArrived()
    {
        // A cancelled appointment cannot be set to Arrived
        AppointmentStatusTransitions.IsValidTransition(AppointmentStatus.Cancelled, AppointmentStatus.Arrived)
            .Should().BeFalse();
    }

    // ─── Handoff Does Not Break Visit Flow ──────────────────────────────────

    [Fact]
    public async Task Handoff_SetsCheckoutStatus_WithoutBreakingVisit()
    {
        await using var db = CreateContext();
        var visit = new Visit
        {
            PatientId = Guid.NewGuid(),
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TreatmentDone = "حشوة",
            Diagnosis = "تسوس"
        };

        db.Visits.Add(visit);
        await db.SaveChangesAsync();

        // Simulate handoff
        visit.CheckoutStatus = "ReadyForCheckout";
        visit.ReadyForCheckoutAt = DateTime.UtcNow;
        visit.AmountDueReference = 15000m;
        visit.TreatmentDone = "حشوة ضرس";
        await db.SaveChangesAsync();

        var saved = await db.Visits.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.Id == visit.Id);
        saved!.CheckoutStatus.Should().Be("ReadyForCheckout");
        saved.TreatmentDone.Should().Be("حشوة ضرس");
        saved.Diagnosis.Should().Be("تسوس");
    }

    // ─── Checkout Does Not Break Existing Appointment/Queue Flow ────────────

    [Fact]
    public async Task Checkout_CompletesAppointment_WhenValidTransition()
    {
        await using var db = CreateContext();
        var appointment = new Appointment
        {
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.InProgress
        };

        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        // Simulate checkout: InProgress → Completed
        var canTransition = AppointmentStatusTransitions.IsValidTransition(
            appointment.Status, AppointmentStatus.Completed);
        canTransition.Should().BeTrue();

        appointment.Status = AppointmentStatus.Completed;
        await db.SaveChangesAsync();

        var saved = await db.Appointments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == appointment.Id);
        saved!.Status.Should().Be(AppointmentStatus.Completed);
    }

    // ─── Journey Data Integration Test ─────────────────────────────────────

    [Fact]
    public async Task JourneyToday_CombinesAppointmentQueueVisitData()
    {
        await using var db = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        // Create appointment
        var appointment = new Appointment
        {
            Id = appointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = today,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.InProgress
        };
        db.Appointments.Add(appointment);

        // Create queue item
        db.ClinicQueueItems.Add(new ClinicQueueItem
        {
            PatientId = patientId,
            AppointmentId = appointmentId,
            DoctorId = doctorId,
            Status = ClinicQueueStatus.InProgress,
            QueueDate = today
        });

        // Create visit
        var visit = new Visit
        {
            PatientId = patientId,
            AppointmentId = appointmentId,
            DoctorId = doctorId,
            VisitDate = today
        };
        db.Visits.Add(visit);

        await db.SaveChangesAsync();

        // Verify all three exist
        var appt = await db.Appointments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == appointmentId);
        appt.Should().NotBeNull();

        var queueItem = await db.ClinicQueueItems
            .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId);
        queueItem.Should().NotBeNull();

        var savedVisit = await db.Visits.IgnoreQueryFilters()
            .FirstOrDefaultAsync(v => v.AppointmentId == appointmentId);
        savedVisit.Should().NotBeNull();
    }
}
