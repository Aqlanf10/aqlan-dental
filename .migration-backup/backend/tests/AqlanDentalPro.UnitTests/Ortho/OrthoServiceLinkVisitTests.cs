using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// CLIN-05: Verifies that OrthoService.AddVisitAsync atomically creates a linked
/// Visit row (with OrthoCaseId set) so daily-operations can see ortho clinical
/// activity, and that the same-day idempotency guard prevents duplicate Visit
/// rows when AddVisitAsync is called twice for the same OrthoCase on the same
/// calendar day.
/// </summary>
public class OrthoServiceLinkVisitTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService BuildCurrentUser() =>
        Mock.Of<ICurrentUserService>(s => s.UserId == Guid.NewGuid() && s.IsAdmin == true);

    private static (OrthoCase OrthoCase, Doctor Doctor, Patient Patient) SeedCase(AppDbContext db)
    {
        var user = new User
        {
            Username = Guid.NewGuid().ToString("N"),
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.Orthodontist,
            IsActive = true
        };
        var doctor = new Doctor
        {
            UserId = user.Id,
            User = user,
            Name = "Ortho Doctor",
            IsActive = true
        };
        var patient = new Patient
        {
            PatientNumber = $"P-{Guid.NewGuid():N}",
            FirstName = "Ortho",
            LastName = "Patient",
            IsActive = true
        };
        var orthoCase = new OrthoCase
        {
            CaseNumber = "OR-LINK-001",
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            Status = OrthoCaseStatus.Active,
            IsActive = true
        };
        db.AddRange(user, doctor, patient, orthoCase);
        db.SaveChanges();
        return (orthoCase, doctor, patient);
    }

    private static CreateOrthoVisitRequest BuildRequest(DateOnly visitDate, string? wireUpper = "0.018 NiTi",
        string? wireLower = "0.016 SS", string? currentStage = "Alignment") => new()
    {
        VisitDate = visitDate.ToString("yyyy-MM-dd"),
        VisitType = "Adjustment",
        WireUpper = wireUpper,
        WireLower = wireLower,
        CurrentStage = currentStage,
        ClinicalNotes = "تم تغيير السلك",
        NextAppointmentType = "ضبط"
    };

    [Fact]
    public async Task AddVisitAsync_Creates_Linked_Visit_With_OrthoCaseId_And_MirroredFields()
    {
        await using var db = CreateDb();
        var (orthoCase, doctor, patient) = SeedCase(db);
        var service = new OrthoService(db, BuildCurrentUser());
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var dto = await service.AddVisitAsync(orthoCase.Id, BuildRequest(visitDate));

        // OrthoVisit row created
        dto.Should().NotBeNull();
        var orthoVisit = await db.OrthoVisits.SingleAsync(v => v.OrthoCaseId == orthoCase.Id);
        orthoVisit.WireUpper.Should().Be("0.018 NiTi");
        orthoVisit.WireLower.Should().Be("0.016 SS");
        orthoVisit.CurrentStage.Should().Be("Alignment");

        // Linked Visit row created with OrthoCaseId set and the same clinical fields mirrored
        var linkedVisit = await db.Visits.SingleOrDefaultAsync(v => v.OrthoCaseId == orthoCase.Id);
        linkedVisit.Should().NotBeNull();
        linkedVisit!.OrthoCaseId.Should().Be(orthoCase.Id);
        linkedVisit.PatientId.Should().Be(patient.Id);
        linkedVisit.DoctorId.Should().Be(doctor.Id);
        linkedVisit.Specialty.Should().Be(Specialty.Orthodontics);
        linkedVisit.VisitDate.Should().Be(visitDate);
        linkedVisit.WireUpper.Should().Be("0.018 NiTi");
        linkedVisit.WireLower.Should().Be("0.016 SS");
        linkedVisit.CurrentStage.Should().Be("Alignment");
        linkedVisit.ClinicalNotes.Should().Be("تم تغيير السلك");
        linkedVisit.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AddVisitAsync_Twice_On_Same_Day_Does_Not_Duplicate_Visit()
    {
        await using var db = CreateDb();
        var (orthoCase, doctor, _) = SeedCase(db);
        var service = new OrthoService(db, BuildCurrentUser());
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // First call — creates both OrthoVisit and linked Visit
        await service.AddVisitAsync(orthoCase.Id, BuildRequest(visitDate,
            wireUpper: "0.018 NiTi", wireLower: "0.016 SS", currentStage: "Alignment"));

        // Second call same day — should NOT create a second Visit; should refresh the existing one
        await service.AddVisitAsync(orthoCase.Id, BuildRequest(visitDate,
            wireUpper: "0.020 NiTi", wireLower: "0.018 SS", currentStage: "Leveling"));

        // Two OrthoVisits expected (each AddVisitAsync creates a new OrthoVisit row —
        // that's the source-of-truth record for the ortho case history).
        var orthoVisits = await db.OrthoVisits.Where(v => v.OrthoCaseId == orthoCase.Id).ToListAsync();
        orthoVisits.Should().HaveCount(2);

        // But exactly ONE linked Visit for this OrthoCase (idempotent by VisitDate)
        var linkedVisits = await db.Visits.Where(v => v.OrthoCaseId == orthoCase.Id).ToListAsync();
        linkedVisits.Should().HaveCount(1, "AddVisitAsync must be idempotent by date on the linked Visit");
        var linked = linkedVisits[0];
        linked.VisitDate.Should().Be(visitDate);

        // The mirrored fields should reflect the LATEST call (refresh, not stale)
        linked.WireUpper.Should().Be("0.020 NiTi");
        linked.WireLower.Should().Be("0.018 SS");
        linked.CurrentStage.Should().Be("Leveling");
    }

    [Fact]
    public async Task AddVisitAsync_On_Different_Days_Creates_Separate_LinkedVisits()
    {
        await using var db = CreateDb();
        var (orthoCase, _, _) = SeedCase(db);
        var service = new OrthoService(db, BuildCurrentUser());

        var day1 = DateOnly.FromDateTime(DateTime.UtcNow);
        var day2 = day1.AddDays(7);

        await service.AddVisitAsync(orthoCase.Id, BuildRequest(day1, currentStage: "Alignment"));
        await service.AddVisitAsync(orthoCase.Id, BuildRequest(day2, currentStage: "Space Closing"));

        // Each calendar day gets its own linked Visit (idempotency is per-date, not per-case)
        var linkedVisits = await db.Visits
            .Where(v => v.OrthoCaseId == orthoCase.Id)
            .OrderBy(v => v.VisitDate)
            .ToListAsync();
        linkedVisits.Should().HaveCount(2);
        linkedVisits[0].VisitDate.Should().Be(day1);
        linkedVisits[0].CurrentStage.Should().Be("Alignment");
        linkedVisits[1].VisitDate.Should().Be(day2);
        linkedVisits[1].CurrentStage.Should().Be("Space Closing");
    }

    [Fact]
    public async Task AddVisitAsync_With_Null_Ortho_Fields_Still_Creates_Linked_Visit()
    {
        // Ortho visits with no wire/stage info still produce a linked Visit so the
        // daily-operations screen sees the visit even when only notes are filled in.
        await using var db = CreateDb();
        var (orthoCase, doctor, _) = SeedCase(db);
        var service = new OrthoService(db, BuildCurrentUser());
        var visitDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await service.AddVisitAsync(orthoCase.Id, BuildRequest(visitDate,
            wireUpper: null, wireLower: null, currentStage: null));

        var linked = await db.Visits.SingleAsync(v => v.OrthoCaseId == orthoCase.Id);
        linked.WireUpper.Should().BeNull();
        linked.WireLower.Should().BeNull();
        linked.CurrentStage.Should().BeNull();
        linked.PatientId.Should().Be(orthoCase.PatientId);
        linked.DoctorId.Should().Be(doctor.Id);
        linked.Specialty.Should().Be(Specialty.Orthodontics);
    }
}
