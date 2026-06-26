using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// YOLO-S1: verifies that Appointment companion/guardian fields (Name/Phone/Relationship),
/// AppointmentColor, and PackageId are persisted and read back correctly.
/// Uses InMemory provider — no PostgreSQL dependency.
///
/// Also covers the no-behavior-change guarantee: an Appointment created WITHOUT the
/// new fields must read back as null (existing callers see no change).
/// </summary>
public class AppointmentCompanionTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Appointment_WithCompanionFields_PersistsAndReadsBack()
    {
        await using var db = CreateContext();
        var patient = new Patient { FirstName = "أحمد", LastName = "محمد", PatientNumber = "P-001" };
        var doctor = new Doctor { Name = "د. عقلان" };
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        var appt = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = new DateOnly(2026, 7, 15),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(10, 30),
            DurationMinutes = 30,
            AppointmentType = "تقويم",
            Status = AppointmentStatus.Scheduled,
            // YOLO-S1 fields
            CompanionName = "فاطمة أحمد",
            CompanionPhone = "967777123456",
            CompanionRelationship = "الأم",
            AppointmentColor = "#8b5cf6",
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Read back from a fresh tracking context to ensure persistence (not just in-memory state).
        var saved = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appt.Id);
        saved.CompanionName.Should().Be("فاطمة أحمد");
        saved.CompanionPhone.Should().Be("967777123456");
        saved.CompanionRelationship.Should().Be("الأم");
        saved.AppointmentColor.Should().Be("#8b5cf6");
        saved.PackageId.Should().BeNull();
    }

    [Fact]
    public async Task Appointment_WithoutCompanionFields_AllNewFieldsAreNull_NoBehaviorChange()
    {
        await using var db = CreateContext();
        var patient = new Patient { FirstName = "سالم", LastName = "علي", PatientNumber = "P-002" };
        var doctor = new Doctor { Name = "د. سعيد" };
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();

        // Create WITHOUT the new YOLO-S1 fields — simulates an existing caller that
        // has not been updated. All new columns must default to null.
        var appt = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = new DateOnly(2026, 7, 16),
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(11, 30),
            DurationMinutes = 30,
            AppointmentType = "كشف",
            Status = AppointmentStatus.Scheduled,
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var saved = await db.Appointments.AsNoTracking().FirstAsync(a => a.Id == appt.Id);
        saved.CompanionName.Should().BeNull();
        saved.CompanionPhone.Should().BeNull();
        saved.CompanionRelationship.Should().BeNull();
        saved.AppointmentColor.Should().BeNull();
        saved.PackageId.Should().BeNull();
    }

    [Fact]
    public async Task Appointment_WithPackageLink_PersistsPackageId()
    {
        await using var db = CreateContext();
        var patient = new Patient { FirstName = "نورا", LastName = "حسن", PatientNumber = "P-003" };
        var doctor = new Doctor { Name = "د. عقلان" };
        var pkg = new TreatmentPackage
        {
            Name = "باقة تبييض كاملة",
            TotalPrice = 30000,
            SessionCount = 3,
            Color = "#f59e0b",
            IsActive = true,
        };
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        db.TreatmentPackages.Add(pkg);
        await db.SaveChangesAsync();

        var appt = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = new DateOnly(2026, 7, 20),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(14, 30),
            DurationMinutes = 30,
            AppointmentType = pkg.Name,
            Status = AppointmentStatus.Scheduled,
            PackageId = pkg.Id,
            AppointmentColor = pkg.Color,
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var saved = await db.Appointments
            .AsNoTracking()
            .Include(a => a.Package)
            .FirstAsync(a => a.Id == appt.Id);

        saved.PackageId.Should().Be(pkg.Id);
        saved.Package.Should().NotBeNull();
        saved.Package!.Name.Should().Be("باقة تبييض كاملة");
        saved.AppointmentColor.Should().Be("#f59e0b");
    }

    [Fact]
    public void Appointment_NewInstance_DefaultsAllNewFieldsToNull()
    {
        var appt = new Appointment();
        appt.CompanionName.Should().BeNull();
        appt.CompanionPhone.Should().BeNull();
        appt.CompanionRelationship.Should().BeNull();
        appt.AppointmentColor.Should().BeNull();
        appt.PackageId.Should().BeNull();
    }
}
