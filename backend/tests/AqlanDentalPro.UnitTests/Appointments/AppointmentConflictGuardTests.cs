using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// C-15: booking must atomically re-check for a doctor time-conflict and insert.
/// TryCreateWithConflictGuardAsync rejects an overlapping slot and inserts a clear
/// one. (On PostgreSQL it also takes a per-doctor advisory lock to serialize
/// concurrent bookings; the InMemory provider here exercises the conflict logic.)
/// </summary>
public class AppointmentConflictGuardTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static Appointment Slot(Guid doctorId, TimeOnly start, TimeOnly end) => new()
    {
        Id = Guid.NewGuid(),
        DoctorId = doctorId,
        PatientId = Guid.NewGuid(),
        AppointmentDate = new DateOnly(2026, 6, 20),
        StartTime = start,
        EndTime = end,
        DurationMinutes = (int)(end - start).TotalMinutes,
        Status = AppointmentStatus.Scheduled,
        IsActive = true,
    };

    [Fact]
    public async Task TryCreate_RejectsOverlap_AndInsertsClearSlot()
    {
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        db.Appointments.Add(Slot(doctorId, new TimeOnly(10, 0), new TimeOnly(10, 30)));
        await db.SaveChangesAsync();
        var repo = new AppointmentRepository(db);

        // Overlaps 10:00–10:30 → rejected, not inserted.
        var conflicting = Slot(doctorId, new TimeOnly(10, 15), new TimeOnly(10, 45));
        (await repo.TryCreateWithConflictGuardAsync(conflicting)).Should().BeFalse();
        (await db.Appointments.CountAsync()).Should().Be(1);

        // Non-overlapping slot for the same doctor → inserted.
        var clear = Slot(doctorId, new TimeOnly(11, 0), new TimeOnly(11, 30));
        (await repo.TryCreateWithConflictGuardAsync(clear)).Should().BeTrue();
        (await db.Appointments.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task TryCreate_AllowsSameSlotForDifferentDoctor()
    {
        await using var db = CreateDb();
        var doctorA = Guid.NewGuid();
        db.Appointments.Add(Slot(doctorA, new TimeOnly(10, 0), new TimeOnly(10, 30)));
        await db.SaveChangesAsync();
        var repo = new AppointmentRepository(db);

        // Same time, different doctor → no conflict.
        var otherDoctor = Slot(Guid.NewGuid(), new TimeOnly(10, 0), new TimeOnly(10, 30));
        (await repo.TryCreateWithConflictGuardAsync(otherDoctor)).Should().BeTrue();
        (await db.Appointments.CountAsync()).Should().Be(2);
    }
}
