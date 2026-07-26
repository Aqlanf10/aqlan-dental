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
    private static AppDbContext CreateDb(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
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

    [Fact]
    public async Task TryCreate_HydratesPatientAndDoctorForImmediateResponseMapping()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "GM-2026-100",
            FirstName = "أحمد",
            LastName = "علي",
            IsActive = true,
        };
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "د. عقلان الكامل",
            Color = "#2563eb",
            IsActive = true,
        };
        db.AddRange(patient, doctor);
        await db.SaveChangesAsync();

        // Ensure the appointment begins with IDs only, matching the create service.
        db.ChangeTracker.Clear();
        var appointment = Slot(doctor.Id, new TimeOnly(12, 0), new TimeOnly(12, 30));
        appointment.PatientId = patient.Id;
        appointment.Patient.Should().BeNull();
        appointment.Doctor.Should().BeNull();

        var repo = new AppointmentRepository(db);
        (await repo.TryCreateWithConflictGuardAsync(appointment)).Should().BeTrue();

        appointment.Patient.Should().NotBeNull();
        appointment.Patient!.FirstName.Should().Be("أحمد");
        appointment.Patient.LastName.Should().Be("علي");
        appointment.Doctor.Should().NotBeNull();
        appointment.Doctor!.Name.Should().Be("د. عقلان الكامل");
        appointment.Doctor.Color.Should().Be("#2563eb");
    }

    // ── CORE-APPT-002: TryUpdateWithConflictGuardAsync (reschedule) ────────────

    [Fact]
    public async Task TryUpdate_RejectsOverlapWithAnotherAppointment_AndDoesNotSave()
    {
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        var fixedSlot = Slot(doctorId, new TimeOnly(10, 0), new TimeOnly(10, 30));
        var toMove = Slot(doctorId, new TimeOnly(14, 0), new TimeOnly(14, 30));
        db.Appointments.AddRange(fixedSlot, toMove);
        await db.SaveChangesAsync();
        var repo = new AppointmentRepository(db);

        // Try to move `toMove` onto a slot overlapping the OTHER appointment's time.
        toMove.StartTime = new TimeOnly(10, 15);
        toMove.EndTime = new TimeOnly(10, 45);

        (await repo.TryUpdateWithConflictGuardAsync(toMove)).Should().BeFalse();

        db.ChangeTracker.Clear();
        var stored = await db.Appointments.SingleAsync(a => a.Id == toMove.Id);
        stored.StartTime.Should().Be(new TimeOnly(14, 0), "a rejected reschedule must not persist the new time");
        stored.EndTime.Should().Be(new TimeOnly(14, 30));
    }

    [Fact]
    public async Task TryUpdate_ExcludesItsOwnRow_AllowsRescheduleWithinItsOwnUnchangedSlot()
    {
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        var appointment = Slot(doctorId, new TimeOnly(10, 0), new TimeOnly(10, 30));
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        var repo = new AppointmentRepository(db);

        // "Reschedule" onto the exact same slot (e.g. only Notes changed) — must not
        // conflict with itself.
        appointment.Notes = "ملاحظة محدّثة";

        (await repo.TryUpdateWithConflictGuardAsync(appointment)).Should().BeTrue();

        db.ChangeTracker.Clear();
        var stored = await db.Appointments.SingleAsync(a => a.Id == appointment.Id);
        stored.Notes.Should().Be("ملاحظة محدّثة");
    }

    [Fact]
    public async Task TryUpdate_AllowsMovingIntoClearSlot_ForSameDoctor()
    {
        await using var db = CreateDb();
        var doctorId = Guid.NewGuid();
        var toMove = Slot(doctorId, new TimeOnly(10, 0), new TimeOnly(10, 30));
        db.Appointments.Add(toMove);
        await db.SaveChangesAsync();
        var repo = new AppointmentRepository(db);

        toMove.StartTime = new TimeOnly(11, 0);
        toMove.EndTime = new TimeOnly(11, 30);

        (await repo.TryUpdateWithConflictGuardAsync(toMove)).Should().BeTrue();

        db.ChangeTracker.Clear();
        var stored = await db.Appointments.SingleAsync(a => a.Id == toMove.Id);
        stored.StartTime.Should().Be(new TimeOnly(11, 0));
        stored.EndTime.Should().Be(new TimeOnly(11, 30));
    }

    [Fact]
    public async Task TryUpdate_AllowsOverlapWithADifferentDoctorsAppointment()
    {
        await using var db = CreateDb();
        var doctorA = Guid.NewGuid();
        var doctorB = Guid.NewGuid();
        var otherDoctorsAppointment = Slot(doctorB, new TimeOnly(10, 0), new TimeOnly(10, 30));
        var toMove = Slot(doctorA, new TimeOnly(14, 0), new TimeOnly(14, 30));
        db.Appointments.AddRange(otherDoctorsAppointment, toMove);
        await db.SaveChangesAsync();
        var repo = new AppointmentRepository(db);

        // Same time as doctorB's appointment, but this one belongs to doctorA — no conflict.
        toMove.StartTime = new TimeOnly(10, 0);
        toMove.EndTime = new TimeOnly(10, 30);

        (await repo.TryUpdateWithConflictGuardAsync(toMove)).Should().BeTrue();
    }
}