using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.ClinicQueue;

/// <summary>
/// Doctor room assignments ("تعيينات غرف الأطباء" — CLAUDE.md priority).
///
/// Covers the two halves of the feature:
///   1. DoctorRoomResolver — the call/recall fallback that pre-fills the room from the
///      doctor's standing assignment. Extracted to a helper because CallPatient itself
///      can't run under EF InMemory (advisory-lock raw SQL, same constraint documented
///      in SurgeryControllerCreateTests / QueueConcurrencyTests).
///   2. DoctorsController Create/Update — assigning, validating and clearing the
///      DefaultClinicRoomId (Guid.Empty = clear, per the partial-update convention).
/// </summary>
public class DoctorRoomAssignmentTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DoctorsController BuildDoctorsController(AppDbContext db)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        user.SetupGet(u => u.Role).Returns(UserRole.Admin);
        user.SetupGet(u => u.IsAdmin).Returns(true);
        return new DoctorsController(db, user.Object);
    }

    private static async Task<(Doctor Doctor, ClinicRoom Room)> SeedDoctorAndRoomAsync(
        AppDbContext db, bool assignRoom = false)
    {
        var user = new User { Username = $"dr-{Guid.NewGuid():N}"[..20], PasswordHash = "h", PasswordSalt = "s", Role = UserRole.Orthodontist, IsActive = true };
        db.Users.Add(user);
        var room = new ClinicRoom { ArabicName = "غرفة التقويم 1", EnglishName = "Ortho 1", Code = "R1", IsActive = true };
        db.ClinicRooms.Add(room);
        var doctor = new Doctor { UserId = user.Id, Name = "د. عقلان", IsActive = true };
        if (assignRoom) doctor.DefaultClinicRoomId = room.Id;
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();
        return (doctor, room);
    }

    // ── DoctorRoomResolver (the call/recall fallback) ────────────────────────────

    [Fact]
    public async Task Resolver_DoctorWithAssignment_ReturnsRoomArabicName()
    {
        await using var db = CreateDb();
        var (doctor, room) = await SeedDoctorAndRoomAsync(db, assignRoom: true);

        var name = await DoctorRoomResolver.ResolveDefaultRoomNameAsync(db, doctor.Id);

        name.Should().Be(room.ArabicName);
    }

    [Fact]
    public async Task Resolver_DoctorWithoutAssignment_ReturnsNull()
    {
        await using var db = CreateDb();
        var (doctor, _) = await SeedDoctorAndRoomAsync(db, assignRoom: false);

        var name = await DoctorRoomResolver.ResolveDefaultRoomNameAsync(db, doctor.Id);

        name.Should().BeNull("a doctor with no standing room keeps the old manual-selection behavior");
    }

    [Fact]
    public async Task Resolver_NullDoctorId_ReturnsNull()
    {
        await using var db = CreateDb();

        var name = await DoctorRoomResolver.ResolveDefaultRoomNameAsync(db, null);

        name.Should().BeNull();
    }

    [Fact]
    public async Task Resolver_SoftDeletedRoom_ReturnsNull()
    {
        await using var db = CreateDb();
        var (doctor, room) = await SeedDoctorAndRoomAsync(db, assignRoom: true);
        room.IsActive = false; // soft-delete the room; the FK column still points at it
        await db.SaveChangesAsync();

        var name = await DoctorRoomResolver.ResolveDefaultRoomNameAsync(db, doctor.Id);

        name.Should().BeNull("the global IsActive query filter must null the navigation for deleted rooms");
    }

    // ── DoctorsController: assign / validate / clear ─────────────────────────────

    [Fact]
    public async Task Update_AssignValidRoom_Persists()
    {
        await using var db = CreateDb();
        var (doctor, room) = await SeedDoctorAndRoomAsync(db);
        var controller = BuildDoctorsController(db);

        var result = await controller.Update(doctor.Id, new UpdateDoctorRequest { DefaultClinicRoomId = room.Id });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Doctors.FindAsync(doctor.Id))!.DefaultClinicRoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task Update_NonexistentRoom_Returns400Arabic_AndDoesNotPersist()
    {
        await using var db = CreateDb();
        var (doctor, _) = await SeedDoctorAndRoomAsync(db);
        var controller = BuildDoctorsController(db);

        var result = await controller.Update(doctor.Id, new UpdateDoctorRequest { DefaultClinicRoomId = Guid.NewGuid() });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value).Should().Be("الغرفة المحددة غير موجودة");
        (await db.Doctors.FindAsync(doctor.Id))!.DefaultClinicRoomId.Should().BeNull();
    }

    [Fact]
    public async Task Update_GuidEmpty_ClearsAssignment()
    {
        await using var db = CreateDb();
        var (doctor, _) = await SeedDoctorAndRoomAsync(db, assignRoom: true);
        var controller = BuildDoctorsController(db);

        var result = await controller.Update(doctor.Id, new UpdateDoctorRequest { DefaultClinicRoomId = Guid.Empty });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Doctors.FindAsync(doctor.Id))!.DefaultClinicRoomId.Should().BeNull();
    }

    [Fact]
    public async Task Update_OmittedField_LeavesAssignmentUnchanged()
    {
        await using var db = CreateDb();
        var (doctor, room) = await SeedDoctorAndRoomAsync(db, assignRoom: true);
        var controller = BuildDoctorsController(db);

        var result = await controller.Update(doctor.Id, new UpdateDoctorRequest { Specialty = "تقويم الأسنان" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.Doctors.FindAsync(doctor.Id))!.DefaultClinicRoomId.Should().Be(room.Id,
            "omitting DefaultClinicRoomId in a partial update must not clear the standing assignment");
    }

    [Fact]
    public async Task Create_WithValidRoom_Persists()
    {
        await using var db = CreateDb();
        var room = new ClinicRoom { ArabicName = "غرفة 2", EnglishName = "Room 2", Code = "R2", IsActive = true };
        db.ClinicRooms.Add(room);
        await db.SaveChangesAsync();
        var controller = BuildDoctorsController(db);

        var result = await controller.Create(new CreateDoctorRequest { Name = "د. جديد", DefaultClinicRoomId = room.Id });

        result.Should().BeOfType<CreatedResult>();
        (await db.Doctors.SingleAsync(d => d.Name == "د. جديد")).DefaultClinicRoomId.Should().Be(room.Id);
    }

    [Fact]
    public async Task Create_WithNonexistentRoom_Returns400Arabic()
    {
        await using var db = CreateDb();
        var controller = BuildDoctorsController(db);

        var result = await controller.Create(new CreateDoctorRequest { Name = "د. جديد", DefaultClinicRoomId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.Doctors.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetById_IncludesDefaultRoomIdAndName()
    {
        await using var db = CreateDb();
        var (doctor, room) = await SeedDoctorAndRoomAsync(db, assignRoom: true);
        var controller = BuildDoctorsController(db);

        var result = await controller.GetById(doctor.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var t = ok.Value!.GetType();
        t.GetProperty("DefaultClinicRoomId")!.GetValue(ok.Value).Should().Be(room.Id);
        t.GetProperty("DefaultRoomName")!.GetValue(ok.Value).Should().Be(room.ArabicName);
    }
}
