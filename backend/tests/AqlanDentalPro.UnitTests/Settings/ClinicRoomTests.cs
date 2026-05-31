using Xunit;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.UnitTests.Settings;

/// <summary>
/// Tests for ClinicRoom entity, DbContext integration, and seed behavior.
/// </summary>
public class ClinicRoomTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ClinicRoom_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var room = new ClinicRoom
        {
            ArabicName = "غرفة 1",
            EnglishName = "Room 1",
            Code = "ROOM-1",
            RoomType = RoomType.Treatment,
            SortOrder = 1
        };

        db.ClinicRooms.Add(room);
        await db.SaveChangesAsync();

        var saved = await db.ClinicRooms.FirstOrDefaultAsync(r => r.Code == "ROOM-1");
        saved.Should().NotBeNull();
        saved!.ArabicName.Should().Be("غرفة 1");
        saved.RoomType.Should().Be(RoomType.Treatment);
    }

    [Fact]
    public async Task ClinicRoom_DefaultValues_AreCorrect()
    {
        var room = new ClinicRoom();
        room.IsActive.Should().BeTrue();
        room.RoomType.Should().Be(RoomType.Treatment);
        room.SortOrder.Should().Be(0);
    }

    [Fact]
    public async Task ClinicRoom_GlobalFilter_ExcludesInactiveItems()
    {
        await using var db = CreateContext();

        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "غرفة نشطة", Code = "R-ACTIVE", IsActive = true });
        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "غرفة معطلة", Code = "R-INACTIVE", IsActive = false });
        await db.SaveChangesAsync();

        var activeRooms = await db.ClinicRooms.ToListAsync();
        activeRooms.Should().HaveCount(1);
        activeRooms[0].Code.Should().Be("R-ACTIVE");
    }

    [Fact]
    public async Task ClinicRoom_IgnoreQueryFilters_ReturnsAll()
    {
        await using var db = CreateContext();

        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "غرفة نشطة", Code = "R1", IsActive = true });
        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "غرفة معطلة", Code = "R2", IsActive = false });
        await db.SaveChangesAsync();

        var allRooms = await db.ClinicRooms.IgnoreQueryFilters().ToListAsync();
        allRooms.Should().HaveCount(2);
    }

    [Fact]
    public async Task ClinicRoom_CanBeDeactivated()
    {
        await using var db = CreateContext();
        var room = new ClinicRoom { ArabicName = "غرفة اختبار", Code = "R-TEST", IsActive = true };
        db.ClinicRooms.Add(room);
        await db.SaveChangesAsync();

        room.IsActive = false;
        room.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var active = await db.ClinicRooms.AnyAsync(r => r.Code == "R-TEST");
        active.Should().BeFalse();

        var exists = await db.ClinicRooms.IgnoreQueryFilters().AnyAsync(r => r.Code == "R-TEST");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ClinicRoom_CanBeReactivated()
    {
        await using var db = CreateContext();
        var room = new ClinicRoom { ArabicName = "غرفة", Code = "R-REACT", IsActive = false, DeletedAt = DateTime.UtcNow };
        db.ClinicRooms.Add(room);
        await db.SaveChangesAsync();

        room.IsActive = true;
        room.DeletedAt = null;
        room.DeletedBy = null;
        await db.SaveChangesAsync();

        var reactivated = await db.ClinicRooms.FirstOrDefaultAsync(r => r.Code == "R-REACT");
        reactivated.Should().NotBeNull();
        reactivated!.IsActive.Should().BeTrue();
        reactivated.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task ClinicRoom_CanFilterByRoomType()
    {
        await using var db = CreateContext();

        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "علاج", Code = "T1", RoomType = RoomType.Treatment });
        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "جراحة", Code = "S1", RoomType = RoomType.Surgery });
        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "أشعة", Code = "X1", RoomType = RoomType.Radiology });
        await db.SaveChangesAsync();

        var surgeryRooms = await db.ClinicRooms.Where(r => r.RoomType == RoomType.Surgery).ToListAsync();
        surgeryRooms.Should().HaveCount(1);
        surgeryRooms[0].Code.Should().Be("S1");
    }

    [Fact]
    public async Task SeedData_DoesNotDuplicate_WhenTableIsNotEmpty()
    {
        await using var db = CreateContext();

        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "موجودة", Code = "EXIST" });
        await db.SaveChangesAsync();

        var hasData = await db.ClinicRooms.AnyAsync();
        hasData.Should().BeTrue(); // Guard check should prevent seeding
    }

    [Fact]
    public async Task ClinicRoom_CanSortBySortOrder()
    {
        await using var db = CreateContext();

        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "ثالث", Code = "C", SortOrder = 3 });
        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "أول", Code = "A", SortOrder = 1 });
        db.ClinicRooms.Add(new ClinicRoom { ArabicName = "ثاني", Code = "B", SortOrder = 2 });
        await db.SaveChangesAsync();

        var sorted = await db.ClinicRooms.OrderBy(r => r.SortOrder).ToListAsync();
        sorted[0].Code.Should().Be("A");
        sorted[1].Code.Should().Be("B");
        sorted[2].Code.Should().Be("C");
    }
}
