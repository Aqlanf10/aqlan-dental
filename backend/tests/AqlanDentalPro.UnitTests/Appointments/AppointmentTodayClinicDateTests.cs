using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Repositories;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

public class AppointmentTodayClinicDateTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"appointments-clinic-day-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static async Task<(Doctor Doctor, Patient Patient)> SeedPrincipalsAsync(AppDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = $"u-{Guid.NewGuid():N}"[..16],
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.GeneralDentist,
            IsActive = true,
        };
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Name = "د. اختبار اليوم",
            IsActive = true,
        };
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = "P-DAY-1",
            FirstName = "مريض",
            LastName = "اليوم",
            IsActive = true,
        };

        db.AddRange(user, doctor, patient);
        await db.SaveChangesAsync();
        return (doctor, patient);
    }

    private static Appointment CreateAppointment(
        Doctor doctor,
        Patient patient,
        DateOnly date,
        TimeOnly start) => new()
    {
        Id = Guid.NewGuid(),
        DoctorId = doctor.Id,
        Doctor = doctor,
        PatientId = patient.Id,
        Patient = patient,
        AppointmentDate = date,
        StartTime = start,
        EndTime = start.AddMinutes(30),
        DurationMinutes = 30,
        AppointmentType = "كشف",
        Status = AppointmentStatus.Scheduled,
        IsActive = true,
    };

    [Fact]
    public async Task GetTodayAsync_UsesExplicitClinicDate_NotHostCalendarDate()
    {
        await using var db = CreateDb();
        var (doctor, patient) = await SeedPrincipalsAsync(db);
        var clinicDate = new DateOnly(2037, 3, 15);

        var expected = CreateAppointment(doctor, patient, clinicDate, new TimeOnly(9, 0));
        var otherDay = CreateAppointment(doctor, patient, clinicDate.AddDays(-1), new TimeOnly(8, 0));
        db.Appointments.AddRange(expected, otherDay);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new AppointmentRepository(db);
        var result = (await repository.GetTodayAsync(null, null, clinicDate)).ToList();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(expected.Id);
        result[0].Patient.Should().NotBeNull();
        result[0].Doctor.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTodayAsync_DefaultsToConfiguredClinicToday()
    {
        await using var db = CreateDb();
        var (doctor, patient) = await SeedPrincipalsAsync(db);
        var clinicToday = ClinicTimeProvider.ClinicToday();

        var expected = CreateAppointment(doctor, patient, clinicToday, new TimeOnly(10, 0));
        db.Appointments.Add(expected);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new AppointmentRepository(db);
        var result = (await repository.GetTodayAsync(null, null)).ToList();

        result.Should().ContainSingle(a => a.Id == expected.Id);
    }
}