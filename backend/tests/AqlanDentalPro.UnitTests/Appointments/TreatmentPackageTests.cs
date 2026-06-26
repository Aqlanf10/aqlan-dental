using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// YOLO-S1: TreatmentPackage entity CRUD + DbContext integration.
/// Uses InMemory provider to verify create/read/update/soft-delete without PostgreSQL.
/// Mirrors the pattern in <c>ClinicServiceTests</c>.
/// </summary>
public class TreatmentPackageTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task TreatmentPackage_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var pkg = new TreatmentPackage
        {
            Name = "باقة تبييض كاملة",
            Description = "ثلاث جلسات تبييض + تنظيف",
            TotalPrice = 30000,
            SessionCount = 3,
            Color = "#f59e0b",
            IsActive = true,
        };

        db.TreatmentPackages.Add(pkg);
        await db.SaveChangesAsync();

        var saved = await db.TreatmentPackages.FirstAsync(p => p.Id == pkg.Id);
        saved.Name.Should().Be("باقة تبييض كاملة");
        saved.TotalPrice.Should().Be(30000);
        saved.SessionCount.Should().Be(3);
        saved.Color.Should().Be("#f59e0b");
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public void TreatmentPackage_DefaultValues_AreCorrect()
    {
        var pkg = new TreatmentPackage();
        pkg.IsActive.Should().BeTrue();
        pkg.SessionCount.Should().Be(1);
        pkg.TotalPrice.Should().Be(0);
        pkg.Name.Should().BeEmpty();
        pkg.Description.Should().BeNull();
        pkg.Color.Should().BeNull();
    }

    [Fact]
    public async Task TreatmentPackage_GlobalFilter_ExcludesInactive()
    {
        await using var db = CreateContext();
        db.TreatmentPackages.Add(new TreatmentPackage { Name = "نشطة", IsActive = true });
        db.TreatmentPackages.Add(new TreatmentPackage { Name = "معطّلة", IsActive = false });
        await db.SaveChangesAsync();

        // Default query (with global filter) returns only active.
        var active = await db.TreatmentPackages.ToListAsync();
        active.Should().HaveCount(1);
        active[0].Name.Should().Be("نشطة");
    }

    [Fact]
    public async Task TreatmentPackage_IgnoreQueryFilters_ReturnsAll()
    {
        await using var db = CreateContext();
        db.TreatmentPackages.Add(new TreatmentPackage { Name = "نشطة", IsActive = true });
        db.TreatmentPackages.Add(new TreatmentPackage { Name = "معطّلة", IsActive = false });
        await db.SaveChangesAsync();

        var all = await db.TreatmentPackages.IgnoreQueryFilters().ToListAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task TreatmentPackage_CanBeUpdated()
    {
        await using var db = CreateContext();
        var pkg = new TreatmentPackage { Name = "باقة 1", TotalPrice = 10000, SessionCount = 1 };
        db.TreatmentPackages.Add(pkg);
        await db.SaveChangesAsync();

        pkg.TotalPrice = 15000;
        pkg.SessionCount = 2;
        pkg.Description = "وصف محدّث";
        await db.SaveChangesAsync();

        var saved = await db.TreatmentPackages.FirstAsync(p => p.Id == pkg.Id);
        saved.TotalPrice.Should().Be(15000);
        saved.SessionCount.Should().Be(2);
        saved.Description.Should().Be("وصف محدّث");
    }

    [Fact]
    public async Task TreatmentPackage_CanBeSoftDeleted()
    {
        await using var db = CreateContext();
        var pkg = new TreatmentPackage { Name = "للحذف", IsActive = true };
        db.TreatmentPackages.Add(pkg);
        await db.SaveChangesAsync();

        pkg.IsActive = false;
        pkg.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Should not appear in default (filtered) query.
        var exists = await db.TreatmentPackages.AnyAsync(p => p.Id == pkg.Id);
        exists.Should().BeFalse();

        // Should still exist when filters are ignored.
        var stillThere = await db.TreatmentPackages.IgnoreQueryFilters().AnyAsync(p => p.Id == pkg.Id);
        stillThere.Should().BeTrue();
    }

    [Fact]
    public async Task TreatmentPackage_CanBeLinkedFromAppointment()
    {
        await using var db = CreateContext();
        var patient = new Patient { FirstName = "مريض", LastName = "اختبار", PatientNumber = "P-100" };
        var doctor = new Doctor { Name = "طبيب" };
        var pkg = new TreatmentPackage { Name = "باقة تقويم", TotalPrice = 200000, SessionCount = 12, Color = "#8b5cf6" };
        db.Patients.Add(patient);
        db.Doctors.Add(doctor);
        db.TreatmentPackages.Add(pkg);
        await db.SaveChangesAsync();

        var appt = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            AppointmentDate = new DateOnly(2026, 8, 1),
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(9, 30),
            DurationMinutes = 30,
            AppointmentType = pkg.Name,
            Status = Domain.Enums.AppointmentStatus.Scheduled,
            PackageId = pkg.Id,
            AppointmentColor = pkg.Color,
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Navigation-property round-trip
        var loaded = await db.Appointments
            .Include(a => a.Package)
            .FirstAsync(a => a.Id == appt.Id);

        loaded.Package.Should().NotBeNull();
        loaded.Package!.Id.Should().Be(pkg.Id);
        loaded.Package.Name.Should().Be("باقة تقويم");
    }

    [Fact]
    public async Task TreatmentPackage_SeedMultiple_DistinctIds()
    {
        await using var db = CreateContext();
        for (var i = 1; i <= 3; i++)
        {
            db.TreatmentPackages.Add(new TreatmentPackage { Name = $"باقة {i}", TotalPrice = i * 1000 });
        }
        await db.SaveChangesAsync();

        var all = await db.TreatmentPackages.ToListAsync();
        all.Should().HaveCount(3);
        all.Select(p => p.Id).Distinct().Count().Should().Be(3);
    }
}
