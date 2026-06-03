using Xunit;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace AqlanDentalPro.UnitTests.DailyOperations;

/// <summary>
/// Unit tests for the LeftWithoutCompletion calculation in DailyOperationsController.
///
/// The formula is intentionally conservative: it only counts appointments/visits
/// with an EXPLICIT terminal non-completed status (e.g. "LeftWithoutCompletion",
/// "CancelledAfterArrival", "Incomplete", "Abandoned").
///
/// These tests verify that broad/active statuses are never miscounted as
/// "left without completion", and that when explicit statuses are introduced,
/// the formula will correctly include them.
/// </summary>
public class LeftWithoutCompletionTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DailyOperationsController CreateController(AppDbContext db)
    {
        var logger = new Mock<ILogger<DailyOperationsController>>().Object;
        return new DailyOperationsController(db, logger);
    }

    /// <summary>
    /// Helper: seeds an appointment + visit for a past date, then calls the
    /// daily report endpoint and extracts the LeftWithoutCompletion count.
    /// </summary>
    private async Task<int> GetLeftWithoutCompletionCount(
        AppDbContext db,
        DateOnly reportDate,
        AppointmentStatus appointmentStatus,
        string? checkoutStatus = null)
    {
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        // Seed patient (required by FK)
        db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "Test",
            LastName = "Patient"
        });

        // Seed appointment
        db.Appointments.Add(new Appointment
        {
            Id = appointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = reportDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = appointmentStatus
        });

        // Seed visit
        db.Visits.Add(new Visit
        {
            PatientId = patientId,
            AppointmentId = appointmentId,
            DoctorId = doctorId,
            VisitDate = reportDate,
            CheckoutStatus = checkoutStatus
        });

        await db.SaveChangesAsync();

        // Call the controller
        var controller = CreateController(db);
        var result = await controller.GetDailyReport(reportDate.ToString("yyyy-MM-dd"));

        // Extract LeftWithoutCompletion from the anonymous result
        var okResult = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value!);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var patientCounts = root.GetProperty("PatientCounts");
        return patientCounts.GetProperty("LeftWithoutCompletion").GetInt32();
    }

    // ─── Exclusion tests: these statuses should NOT be counted ─────────────

    [Fact]
    public async Task LeftWithoutCompletion_NoShow_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.NoShow);
        count.Should().Be(0, "NoShow should never be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_CancelledBeforeArrival_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.Cancelled);
        count.Should().Be(0, "Cancelled (before arrival) should not be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_Completed_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.Completed, "CheckedOut");
        count.Should().Be(0, "Completed appointments should not be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_Scheduled_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.Scheduled);
        count.Should().Be(0, "Scheduled (never arrived) should not be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_Confirmed_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.Confirmed);
        count.Should().Be(0, "Confirmed (not yet arrived) should not be counted as LeftWithoutCompletion");
    }

    // ─── Active flow statuses: should NOT be counted (patient still in clinic) ─

    [Fact]
    public async Task LeftWithoutCompletion_Waiting_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.Waiting);
        count.Should().Be(0, "Waiting patients should not be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_InRoom_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.InRoom);
        count.Should().Be(0, "InRoom patients should not be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_InProgress_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.InProgress);
        count.Should().Be(0, "InProgress patients should not be counted as LeftWithoutCompletion");
    }

    [Fact]
    public async Task LeftWithoutCompletion_ReadyForCheckout_NotCounted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var count = await GetLeftWithoutCompletionCount(db, pastDate, AppointmentStatus.InProgress, "ReadyForCheckout");
        count.Should().Be(0, "ReadyForCheckout patients should not be counted as LeftWithoutCompletion");
    }

    // ─── Today's report: should always be 0 (day not over) ────────────────

    [Fact]
    public async Task LeftWithoutCompletion_Today_AlwaysZero()
    {
        await using var db = CreateContext();
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Even with an active appointment, today should return 0
        var count = await GetLeftWithoutCompletionCount(db, today, AppointmentStatus.InProgress);
        count.Should().Be(0, "Today's report should always have LeftWithoutCompletion = 0 since the day is not over");
    }

    // ─── Explicit terminal status: SHOULD be counted ───────────────────────
    // These tests simulate a future state where explicit statuses exist.
    // Since AppointmentStatus doesn't yet have LeftWithoutCompletion/CancelledAfterArrival,
    // we test via Visit.CheckoutStatus string which can hold any value.

    [Fact]
    public async Task LeftWithoutCompletion_ExplicitCheckoutStatus_Counted()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient" });

        // Appointment is InProgress but visit has explicit "LeftWithoutCompletion" checkout status
        db.Appointments.Add(new Appointment
        {
            Id = appointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.InProgress
        });

        db.Visits.Add(new Visit
        {
            PatientId = patientId,
            AppointmentId = appointmentId,
            DoctorId = doctorId,
            VisitDate = pastDate,
            CheckoutStatus = "LeftWithoutCompletion"  // explicit terminal status
        });

        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetDailyReport(pastDate.ToString("yyyy-MM-dd"));

        var okResult = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value!);
        using var doc = JsonDocument.Parse(json);
        var count = doc.RootElement.GetProperty("PatientCounts").GetProperty("LeftWithoutCompletion").GetInt32();

        // NOTE: This will currently be 0 because the HashSet in the controller
        // does not yet include "LeftWithoutCompletion". When the status is added
        // to the system and the HashSet is updated, this test should be updated
        // to assert count == 1.
        // For now, this test documents the expected behavior and serves as a
        // reminder to update the controller when the status is introduced.
        count.Should().Be(1, "LeftWithoutCompletion checkout status is now the explicit terminal marker");
    }

    // ─── Multiple statuses mixed: only explicit ones counted ───────────────

    [Fact]
    public async Task LeftWithoutCompletion_MixedStatuses_OnlyCountsExplicit()
    {
        await using var db = CreateContext();
        var pastDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1);
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient" });

        // NoShow — should NOT be counted
        db.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.NoShow
        });

        // Cancelled — should NOT be counted
        db.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.Cancelled
        });

        // Completed — should NOT be counted
        db.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(11, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.Completed
        });

        // InProgress — should NOT be counted (active flow, not explicit left)
        db.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(12, 0),
            EndTime = new TimeOnly(12, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.InProgress
        });

        // Waiting — should NOT be counted
        db.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(13, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.Waiting
        });

        var explicitAppointmentId = Guid.NewGuid();
        db.Appointments.Add(new Appointment
        {
            Id = explicitAppointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentDate = pastDate,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(14, 30),
            AppointmentType = "كشف",
            Status = AppointmentStatus.InProgress
        });
        db.Visits.Add(new Visit
        {
            PatientId = patientId,
            AppointmentId = explicitAppointmentId,
            DoctorId = doctorId,
            VisitDate = pastDate,
            CheckoutStatus = "LeftWithoutCompletion"
        });

        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetDailyReport(pastDate.ToString("yyyy-MM-dd"));

        var okResult = result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value!);
        using var doc = JsonDocument.Parse(json);
        var count = doc.RootElement.GetProperty("PatientCounts").GetProperty("LeftWithoutCompletion").GetInt32();

        count.Should().Be(1, "Only the explicit LeftWithoutCompletion status should be counted");
    }
}
