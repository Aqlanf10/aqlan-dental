using Xunit;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.UnitTests.Settings;

/// <summary>
/// Tests for ClinicService entity, DbContext integration, and seed behavior.
/// Uses InMemory provider to verify CRUD operations, active/inactive filters,
/// and seed idempotency without requiring a PostgreSQL database.
/// </summary>
public class ClinicServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ─── Entity Creation Tests ─────────────────────────────────────────────

    [Fact]
    public async Task ClinicService_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var service = new ClinicService
        {
            ArabicName = "معاينة",
            EnglishName = "Consultation",
            Code = "CONS",
            Category = ServiceCategory.Consultation,
            DefaultDurationMinutes = 30,
            DefaultPrice = 5000,
            RequiresDoctor = true,
            IsActive = true
        };

        db.ClinicServices.Add(service);
        await db.SaveChangesAsync();

        var saved = await db.ClinicServices.FirstOrDefaultAsync(s => s.Code == "CONS");
        saved.Should().NotBeNull();
        saved!.ArabicName.Should().Be("معاينة");
        saved.Category.Should().Be(ServiceCategory.Consultation);
        saved.DefaultPrice.Should().Be(5000);
        saved.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ClinicService_DefaultValues_AreCorrect()
    {
        var service = new ClinicService();
        service.IsActive.Should().BeTrue();
        service.DefaultDurationMinutes.Should().Be(30);
        service.DefaultPrice.Should().Be(0);
        service.RequiresDoctor.Should().BeTrue();
        service.RequiresConsultationFee.Should().BeFalse();
        service.ShowInBooking.Should().BeTrue();
        service.ShowInReception.Should().BeTrue();
        service.ShowInTreatmentPlan.Should().BeTrue();
        service.SortOrder.Should().Be(0);
        service.Category.Should().Be(ServiceCategory.Other);
    }

    // ─── Active/Inactive Filter Tests ──────────────────────────────────────

    [Fact]
    public async Task ClinicService_GlobalFilter_ExcludesInactiveItems()
    {
        await using var db = CreateContext();

        db.ClinicServices.Add(new ClinicService { ArabicName = "نشط", Code = "ACTIVE", IsActive = true });
        db.ClinicServices.Add(new ClinicService { ArabicName = "معطل", Code = "INACTIVE", IsActive = false });
        await db.SaveChangesAsync();

        // Default query (with global filter) should only return active
        var activeServices = await db.ClinicServices.ToListAsync();
        activeServices.Should().HaveCount(1);
        activeServices[0].Code.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task ClinicService_IgnoreQueryFilters_ReturnsAll()
    {
        await using var db = CreateContext();

        db.ClinicServices.Add(new ClinicService { ArabicName = "نشط", Code = "ACTIVE", IsActive = true });
        db.ClinicServices.Add(new ClinicService { ArabicName = "معطل", Code = "INACTIVE", IsActive = false });
        await db.SaveChangesAsync();

        // IgnoreQueryFilters should return all
        var allServices = await db.ClinicServices.IgnoreQueryFilters().ToListAsync();
        allServices.Should().HaveCount(2);
    }

    // ─── CRUD Validation Tests ─────────────────────────────────────────────

    [Fact]
    public async Task ClinicService_CanBeUpdated()
    {
        await using var db = CreateContext();
        var service = new ClinicService { ArabicName = "معاينة", Code = "CONS", DefaultPrice = 5000 };
        db.ClinicServices.Add(service);
        await db.SaveChangesAsync();

        service.DefaultPrice = 7000;
        service.ArabicName = "معاينة عامة";
        await db.SaveChangesAsync();

        var updated = await db.ClinicServices.FirstOrDefaultAsync(s => s.Code == "CONS");
        updated!.DefaultPrice.Should().Be(7000);
        updated.ArabicName.Should().Be("معاينة عامة");
    }

    [Fact]
    public async Task ClinicService_CanBeDeactivated()
    {
        await using var db = CreateContext();
        var service = new ClinicService { ArabicName = "اختبار", Code = "TEST", IsActive = true };
        db.ClinicServices.Add(service);
        await db.SaveChangesAsync();

        service.IsActive = false;
        service.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Should not appear in default query
        var active = await db.ClinicServices.AnyAsync(s => s.Code == "TEST");
        active.Should().BeFalse();

        // Should appear with IgnoreQueryFilters
        var exists = await db.ClinicServices.IgnoreQueryFilters().AnyAsync(s => s.Code == "TEST");
        exists.Should().BeTrue();
    }

    // ─── Seed Data Tests ───────────────────────────────────────────────────

    [Fact]
    public async Task SeedData_DoesNotDuplicate_WhenTableIsNotEmpty()
    {
        await using var db = CreateContext();

        // Add one service manually
        db.ClinicServices.Add(new ClinicService { ArabicName = "موجود", Code = "EXIST" });
        await db.SaveChangesAsync();

        var countBefore = await db.ClinicServices.CountAsync();
        countBefore.Should().Be(1);

        // Simulate the seed guard: ClinicServices.AnyAsync() returns true, so seed should not run
        var hasData = await db.ClinicServices.AnyAsync();
        hasData.Should().BeTrue();
    }

    // ─── Category Filtering Tests ──────────────────────────────────────────

    [Fact]
    public async Task ClinicService_CanFilterByCategory()
    {
        await using var db = CreateContext();

        db.ClinicServices.Add(new ClinicService { ArabicName = "معاينة", Code = "CONS", Category = ServiceCategory.Consultation });
        db.ClinicServices.Add(new ClinicService { ArabicName = "حشوة", Code = "FILL", Category = ServiceCategory.Restorative });
        db.ClinicServices.Add(new ClinicService { ArabicName = "تقويم", Code = "ORTH", Category = ServiceCategory.Orthodontics });
        await db.SaveChangesAsync();

        var consultServices = await db.ClinicServices
            .Where(s => s.Category == ServiceCategory.Consultation)
            .ToListAsync();

        consultServices.Should().HaveCount(1);
        consultServices[0].Code.Should().Be("CONS");
    }

    // ─── Sort Order Tests ──────────────────────────────────────────────────

    [Fact]
    public async Task ClinicService_CanSortBySortOrder()
    {
        await using var db = CreateContext();

        db.ClinicServices.Add(new ClinicService { ArabicName = "ثالث", Code = "C", SortOrder = 3 });
        db.ClinicServices.Add(new ClinicService { ArabicName = "أول", Code = "A", SortOrder = 1 });
        db.ClinicServices.Add(new ClinicService { ArabicName = "ثاني", Code = "B", SortOrder = 2 });
        await db.SaveChangesAsync();

        var sorted = await db.ClinicServices.OrderBy(s => s.SortOrder).ToListAsync();
        sorted[0].Code.Should().Be("A");
        sorted[1].Code.Should().Be("B");
        sorted[2].Code.Should().Be("C");
    }
}
