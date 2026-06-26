using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// YOLO-S2: ServiceConsumable entity (service ↔ inventory item) CRUD +
/// DbContext integration. Uses InMemory provider to verify create/read/
/// update/soft-delete without PostgreSQL. Mirrors the pattern in
/// <c>TreatmentPackageTests</c>.
/// </summary>
public class ServiceConsumableTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (ClinicService svc, Inventory item) SeedServiceAndItem(AppDbContext db, string serviceCode = "SVC-CONS-1")
    {
        var svc = new ClinicService
        {
            ArabicName = "حشو ضوئي",
            EnglishName = "Light-cured filling",
            Code = serviceCode,
            DefaultPrice = 5000,
        };
        var item = new Inventory
        {
            Name = "راتنج مركب",
            Category = "مواد ترميمية",
            Quantity = 100,
            Unit = "كرتوشة",
        };
        db.ClinicServices.Add(svc);
        db.Inventory.Add(item);
        db.SaveChanges();
        return (svc, item);
    }

    [Fact]
    public async Task ServiceConsumable_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var (svc, item) = SeedServiceAndItem(db);

        var consumable = new ServiceConsumable
        {
            ClinicServiceId = svc.Id,
            InventoryItemId = item.Id,
            Quantity = 2,
            Notes = "يُستخدم في الحالات البسيطة فقط",
        };
        db.ServiceConsumables.Add(consumable);
        await db.SaveChangesAsync();

        var saved = await db.ServiceConsumables.FirstAsync(c => c.Id == consumable.Id);
        saved.ClinicServiceId.Should().Be(svc.Id);
        saved.InventoryItemId.Should().Be(item.Id);
        saved.Quantity.Should().Be(2);
        saved.Notes.Should().Be("يُستخدم في الحالات البسيطة فقط");
    }

    [Fact]
    public void ServiceConsumable_DefaultValues_AreCorrect()
    {
        var c = new ServiceConsumable();
        c.Quantity.Should().Be(1);
        c.Notes.Should().BeNull();
    }

    [Fact]
    public async Task ServiceConsumable_CanBeLoadedViaServiceNavigation()
    {
        await using var db = CreateContext();
        var (svc, item) = SeedServiceAndItem(db, "SVC-NAV-1");

        db.ServiceConsumables.Add(new ServiceConsumable
        {
            ClinicServiceId = svc.Id,
            InventoryItemId = item.Id,
            Quantity = 3,
        });
        await db.SaveChangesAsync();

        var loaded = await db.ClinicServices
            .Include(s => s.Consumables)
            .FirstAsync(s => s.Id == svc.Id);

        loaded.Consumables.Should().HaveCount(1);
        loaded.Consumables.First().Quantity.Should().Be(3);
        loaded.Consumables.First().InventoryItemId.Should().Be(item.Id);
    }

    [Fact]
    public async Task ServiceConsumable_CanBeLoadedViaInventoryItemNavigation()
    {
        await using var db = CreateContext();
        var (svc, item) = SeedServiceAndItem(db, "SVC-INV-1");

        db.ServiceConsumables.Add(new ServiceConsumable
        {
            ClinicServiceId = svc.Id,
            InventoryItemId = item.Id,
            Quantity = 1,
        });
        await db.SaveChangesAsync();

        // ServiceConsumable references InventoryItem (many-to-one from the consumable side);
        // verify the FK can be navigated.
        var loaded = await db.ServiceConsumables
            .Include(c => c.InventoryItem)
            .FirstAsync(c => c.ClinicServiceId == svc.Id);

        loaded.InventoryItem.Should().NotBeNull();
        loaded.InventoryItem!.Name.Should().Be("راتنج مركب");
    }

    [Fact]
    public async Task ServiceConsumable_CanBeSoftDeleted()
    {
        await using var db = CreateContext();
        var (svc, item) = SeedServiceAndItem(db, "SVC-DEL-1");

        var consumable = new ServiceConsumable
        {
            ClinicServiceId = svc.Id,
            InventoryItemId = item.Id,
            Quantity = 1,
        };
        db.ServiceConsumables.Add(consumable);
        await db.SaveChangesAsync();

        consumable.IsActive = false;
        consumable.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Should not appear in default (filtered) query.
        var exists = await db.ServiceConsumables.AnyAsync(c => c.Id == consumable.Id);
        exists.Should().BeFalse();

        // Should still exist when filters are ignored.
        var stillThere = await db.ServiceConsumables.IgnoreQueryFilters().AnyAsync(c => c.Id == consumable.Id);
        stillThere.Should().BeTrue();
    }

    [Fact]
    public async Task ServiceConsumable_CanBeUpdated()
    {
        await using var db = CreateContext();
        var (svc, item) = SeedServiceAndItem(db, "SVC-UPD-1");

        var consumable = new ServiceConsumable
        {
            ClinicServiceId = svc.Id,
            InventoryItemId = item.Id,
            Quantity = 1,
            Notes = "ملاحظة أولية",
        };
        db.ServiceConsumables.Add(consumable);
        await db.SaveChangesAsync();

        consumable.Quantity = 5;
        consumable.Notes = "ملاحظة محدّثة";
        await db.SaveChangesAsync();

        var saved = await db.ServiceConsumables.FirstAsync(c => c.Id == consumable.Id);
        saved.Quantity.Should().Be(5);
        saved.Notes.Should().Be("ملاحظة محدّثة");
    }

    [Fact]
    public async Task ServiceConsumable_SupportsMultiplePerService()
    {
        await using var db = CreateContext();
        var (svc, item1) = SeedServiceAndItem(db, "SVC-MULTI-1");
        var item2 = new Inventory { Name = "مخدر موضعي", Quantity = 50, Unit = "كاربولة" };
        var item3 = new Inventory { Name = "قفاز طبي", Quantity = 200, Unit = "قطعة" };
        db.Inventory.AddRange(item2, item3);
        await db.SaveChangesAsync();

        db.ServiceConsumables.AddRange(
            new ServiceConsumable { ClinicServiceId = svc.Id, InventoryItemId = item1.Id, Quantity = 2 },
            new ServiceConsumable { ClinicServiceId = svc.Id, InventoryItemId = item2.Id, Quantity = 1 },
            new ServiceConsumable { ClinicServiceId = svc.Id, InventoryItemId = item3.Id, Quantity = 2 }
        );
        await db.SaveChangesAsync();

        var loaded = await db.ClinicServices
            .Include(s => s.Consumables)
            .FirstAsync(s => s.Id == svc.Id);

        loaded.Consumables.Should().HaveCount(3);
        loaded.Consumables.Select(c => c.InventoryItemId).Should().Contain([item1.Id, item2.Id, item3.Id]);
    }

    [Fact]
    public async Task ClinicService_Color_CanBeSetAndRead()
    {
        await using var db = CreateContext();
        var svc = new ClinicService
        {
            ArabicName = "تنظيف",
            EnglishName = "Cleaning",
            Code = "CLEAN-COLOR-1",
            DefaultPrice = 3000,
            Color = "#3b82f6",
        };
        db.ClinicServices.Add(svc);
        await db.SaveChangesAsync();

        var saved = await db.ClinicServices.FirstAsync(s => s.Id == svc.Id);
        saved.Color.Should().Be("#3b82f6");
    }

    [Fact]
    public async Task ClinicService_Color_IsNullable_DefaultsNull()
    {
        await using var db = CreateContext();
        var svc = new ClinicService
        {
            ArabicName = "فحص",
            EnglishName = "Exam",
            Code = "EXAM-COLOR-1",
            DefaultPrice = 2000,
            // Color not set
        };
        db.ClinicServices.Add(svc);
        await db.SaveChangesAsync();

        var saved = await db.ClinicServices.FirstAsync(s => s.Id == svc.Id);
        saved.Color.Should().BeNull();
    }

    [Fact]
    public async Task ServiceConsumable_ServiceCascadeDelete_RemovesConsumables()
    {
        await using var db = CreateContext();
        var (svc, item) = SeedServiceAndItem(db, "SVC-CASC-1");

        var consumable = new ServiceConsumable
        {
            ClinicServiceId = svc.Id,
            InventoryItemId = item.Id,
            Quantity = 1,
        };
        db.ServiceConsumables.Add(consumable);
        await db.SaveChangesAsync();

        var consumableId = consumable.Id;
        db.ClinicServices.Remove(svc);
        await db.SaveChangesAsync();

        // With OnDelete(Cascade) on the Service FK, the consumable should be physically removed.
        var stillThere = await db.ServiceConsumables.IgnoreQueryFilters().AnyAsync(c => c.Id == consumableId);
        stillThere.Should().BeFalse();
    }
}
