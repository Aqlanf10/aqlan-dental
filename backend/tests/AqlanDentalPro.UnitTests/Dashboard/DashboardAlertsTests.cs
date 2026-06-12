using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Dashboard;

/// <summary>
/// Tests for DashboardService.GetAlertsAsync — the operational attention
/// counters (overdue lab work, no-shows, long-waiting, unconfirmed tomorrow,
/// recall candidates).
/// </summary>
public class DashboardAlertsTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static DashboardService CreateService(AppDbContext db, bool isAdmin = true, Guid? branchId = null)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.IsAdmin).Returns(isAdmin);
        user.Setup(u => u.BranchId).Returns(branchId);
        return new DashboardService(db, user.Object);
    }

    private static Patient NewPatient(Guid? branchId = null) => new()
    {
        Id = Guid.NewGuid(),
        FirstName = "مريض",
        LastName = "اختبار",
        PatientNumber = $"P-{Guid.NewGuid().ToString()[..6]}",
        BranchId = branchId ?? Guid.NewGuid(),
        IsActive = true,
    };

    [Fact]
    public async Task EmptyDatabase_ReturnsAllZero()
    {
        await using var db = CreateDb();
        var alerts = await CreateService(db).GetAlertsAsync();

        alerts.OverdueLabOrdersCount.Should().Be(0);
        alerts.MaxLabDaysOverdue.Should().Be(0);
        alerts.TodayNoShowCount.Should().Be(0);
        alerts.LongWaitingCount.Should().Be(0);
        alerts.UnconfirmedTomorrowCount.Should().Be(0);
        alerts.RecallCandidatesCount.Should().Be(0);
    }

    [Fact]
    public async Task OverdueLabOrders_CountsOnlyPastExpectedAndNotTerminal()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var patient = NewPatient();
        db.Patients.Add(patient);

        db.LabOrders.AddRange(
            new LabOrder { PatientId = patient.Id, ApplianceType = "تاج", Status = "sent", ExpectedDate = today.AddDays(-5), IsActive = true },
            new LabOrder { PatientId = patient.Id, ApplianceType = "تاج", Status = "manufacturing", ExpectedDate = today.AddDays(-2), IsActive = true },
            new LabOrder { PatientId = patient.Id, ApplianceType = "تاج", Status = "delivered", ExpectedDate = today.AddDays(-9), IsActive = true },
            new LabOrder { PatientId = patient.Id, ApplianceType = "تاج", Status = "cancelled", ExpectedDate = today.AddDays(-9), IsActive = true },
            new LabOrder { PatientId = patient.Id, ApplianceType = "تاج", Status = "sent", ExpectedDate = today.AddDays(3), IsActive = true });
        await db.SaveChangesAsync();

        var alerts = await CreateService(db).GetAlertsAsync();

        alerts.OverdueLabOrdersCount.Should().Be(2, "delivered/cancelled and future orders are not overdue");
        alerts.MaxLabDaysOverdue.Should().Be(5);
    }

    [Fact]
    public async Task NoShowToday_And_UnconfirmedTomorrow_AreCounted()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var p1 = NewPatient();
        var p2 = NewPatient();
        db.Patients.AddRange(p1, p2);

        db.Appointments.AddRange(
            new Appointment { PatientId = p1.Id, AppointmentDate = today, Status = AppointmentStatus.NoShow, IsActive = true },
            new Appointment { PatientId = p2.Id, AppointmentDate = today, Status = AppointmentStatus.Completed, IsActive = true },
            new Appointment { PatientId = p1.Id, AppointmentDate = today.AddDays(1), Status = AppointmentStatus.Scheduled, IsActive = true },
            new Appointment { PatientId = p2.Id, AppointmentDate = today.AddDays(1), Status = AppointmentStatus.Confirmed, IsActive = true });
        await db.SaveChangesAsync();

        var alerts = await CreateService(db).GetAlertsAsync();

        alerts.TodayNoShowCount.Should().Be(1);
        alerts.UnconfirmedTomorrowCount.Should().Be(1, "only Scheduled (not Confirmed) counts as unconfirmed");
    }

    [Fact]
    public async Task LongWaiting_CountsOnlyWaitingOlderThanThreshold()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var p = NewPatient();
        db.Patients.Add(p);

        var oldWaiting = new ClinicQueueItem { PatientId = p.Id, QueueDate = today, Status = ClinicQueueStatus.Waiting, IsActive = true };
        var newWaiting = new ClinicQueueItem { PatientId = p.Id, QueueDate = today, Status = ClinicQueueStatus.Waiting, IsActive = true };
        var oldInRoom = new ClinicQueueItem { PatientId = p.Id, QueueDate = today, Status = ClinicQueueStatus.InRoom, IsActive = true };
        db.ClinicQueueItems.AddRange(oldWaiting, newWaiting, oldInRoom);
        await db.SaveChangesAsync();

        // SaveChanges stamps CreatedAt on insert — backdate after the fact.
        oldWaiting.CreatedAt = DateTime.UtcNow.AddMinutes(-45);
        newWaiting.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
        oldInRoom.CreatedAt = DateTime.UtcNow.AddMinutes(-90);
        await db.SaveChangesAsync();

        var alerts = await CreateService(db).GetAlertsAsync(longWaitMinutes: 30);

        alerts.LongWaitingCount.Should().Be(1, "only Waiting items older than the threshold count");
    }

    [Fact]
    public async Task RecallCandidates_ExcludesPatientsWithUpcomingAppointment()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var missedNoFuture = NewPatient();
        var missedWithFuture = NewPatient();
        db.Patients.AddRange(missedNoFuture, missedWithFuture);

        db.Appointments.AddRange(
            // no-show last week, nothing booked → candidate
            new Appointment { PatientId = missedNoFuture.Id, AppointmentDate = today.AddDays(-7), Status = AppointmentStatus.NoShow, IsActive = true },
            // no-show last week but rebooked for next week → not a candidate
            new Appointment { PatientId = missedWithFuture.Id, AppointmentDate = today.AddDays(-7), Status = AppointmentStatus.NoShow, IsActive = true },
            new Appointment { PatientId = missedWithFuture.Id, AppointmentDate = today.AddDays(7), Status = AppointmentStatus.Scheduled, IsActive = true });
        await db.SaveChangesAsync();

        var alerts = await CreateService(db).GetAlertsAsync(recallWindowDays: 30);

        alerts.RecallCandidatesCount.Should().Be(1);
    }

    [Fact]
    public async Task BranchScoping_NonAdminSeesOnlyOwnBranch()
    {
        await using var db = CreateDb();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var myBranch = Guid.NewGuid();
        var otherBranch = Guid.NewGuid();
        var mine = NewPatient(myBranch);
        var other = NewPatient(otherBranch);
        db.Patients.AddRange(mine, other);

        db.Appointments.AddRange(
            new Appointment { PatientId = mine.Id, BranchId = myBranch, AppointmentDate = today, Status = AppointmentStatus.NoShow, IsActive = true },
            new Appointment { PatientId = other.Id, BranchId = otherBranch, AppointmentDate = today, Status = AppointmentStatus.NoShow, IsActive = true });
        await db.SaveChangesAsync();

        var adminAlerts = await CreateService(db, isAdmin: true).GetAlertsAsync();
        var branchAlerts = await CreateService(db, isAdmin: false, branchId: myBranch).GetAlertsAsync();

        adminAlerts.TodayNoShowCount.Should().Be(2);
        branchAlerts.TodayNoShowCount.Should().Be(1);
    }
}
