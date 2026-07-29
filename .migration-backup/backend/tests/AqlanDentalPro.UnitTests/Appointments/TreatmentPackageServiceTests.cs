using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Appointments;

/// <summary>
/// YOLO-S2: TreatmentPackageService link-table entity (package ↔ service) +
/// Contract.PackageId nullable FK. Uses InMemory provider to verify create/read/
/// update/delete and navigation loading without PostgreSQL. Mirrors the pattern
/// in <c>TreatmentPackageTests</c> / <c>ServiceConsumableTests</c>.
/// </summary>
public class TreatmentPackageServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (TreatmentPackage pkg, ClinicService svc) SeedPackageAndService(AppDbContext db, string code = "SVC-PKG-1")
    {
        var pkg = new TreatmentPackage
        {
            Name = "باقة تبييض كاملة",
            TotalPrice = 30000,
            SessionCount = 3,
            Color = "#f59e0b",
        };
        var svc = new ClinicService
        {
            ArabicName = "تنظيف وتلميع",
            EnglishName = "Cleaning & Polishing",
            Code = code,
            DefaultPrice = 5000,
        };
        db.TreatmentPackages.Add(pkg);
        db.ClinicServices.Add(svc);
        db.SaveChanges();
        return (pkg, svc);
    }

    [Fact]
    public async Task PackageServiceLink_CanBeCreated_WithRequiredFields()
    {
        await using var db = CreateContext();
        var (pkg, svc) = SeedPackageAndService(db);

        var link = new TreatmentPackageService
        {
            PackageId = pkg.Id,
            ClinicServiceId = svc.Id,
            Quantity = 3,
            OverridePrice = 4500m,
        };
        db.TreatmentPackageServices.Add(link);
        await db.SaveChangesAsync();

        var saved = await db.TreatmentPackageServices.FirstAsync(l => l.Id == link.Id);
        saved.PackageId.Should().Be(pkg.Id);
        saved.ClinicServiceId.Should().Be(svc.Id);
        saved.Quantity.Should().Be(3);
        saved.OverridePrice.Should().Be(4500m);
    }

    [Fact]
    public void PackageServiceLink_DefaultValues_AreCorrect()
    {
        var link = new TreatmentPackageService();
        link.Quantity.Should().Be(1);
        link.OverridePrice.Should().BeNull();
    }

    [Fact]
    public async Task PackageServiceLink_CanBeLoadedViaPackageNavigation()
    {
        await using var db = CreateContext();
        var (pkg, svc) = SeedPackageAndService(db, "SVC-NAV-1");

        db.TreatmentPackageServices.Add(new TreatmentPackageService
        {
            PackageId = pkg.Id,
            ClinicServiceId = svc.Id,
            Quantity = 2,
        });
        await db.SaveChangesAsync();

        var loaded = await db.TreatmentPackages
            .Include(p => p.Services)
            .FirstAsync(p => p.Id == pkg.Id);

        loaded.Services.Should().HaveCount(1);
        loaded.Services.First().Quantity.Should().Be(2);
        loaded.Services.First().ClinicServiceId.Should().Be(svc.Id);
    }

    [Fact]
    public async Task PackageServiceLink_CanBeLoadedViaServiceReverseNavigation()
    {
        await using var db = CreateContext();
        var (pkg, svc) = SeedPackageAndService(db, "SVC-REV-1");

        db.TreatmentPackageServices.Add(new TreatmentPackageService
        {
            PackageId = pkg.Id,
            ClinicServiceId = svc.Id,
            Quantity = 1,
        });
        await db.SaveChangesAsync();

        var loaded = await db.ClinicServices
            .Include(s => s.PackageLinks)
            .FirstAsync(s => s.Id == svc.Id);

        loaded.PackageLinks.Should().HaveCount(1);
        loaded.PackageLinks.First().PackageId.Should().Be(pkg.Id);
    }

    [Fact]
    public async Task Package_CascadeDelete_RemovesLinks()
    {
        await using var db = CreateContext();
        var (pkg, svc) = SeedPackageAndService(db, "SVC-CASC-1");

        var link = new TreatmentPackageService { PackageId = pkg.Id, ClinicServiceId = svc.Id, Quantity = 1 };
        db.TreatmentPackageServices.Add(link);
        await db.SaveChangesAsync();
        var linkId = link.Id;

        db.TreatmentPackages.Remove(pkg);
        await db.SaveChangesAsync();

        // With OnDelete(Cascade) on the Package FK, the link should be physically removed.
        var stillThere = await db.TreatmentPackageServices.IgnoreQueryFilters().AnyAsync(l => l.Id == linkId);
        stillThere.Should().BeFalse();
    }

    [Fact]
    public async Task PackageService_SupportsMultipleServicesPerPackage()
    {
        await using var db = CreateContext();
        var (pkg, svc1) = SeedPackageAndService(db, "SVC-MULTI-1");
        var svc2 = new ClinicService { ArabicName = "حشو ضوئي", EnglishName = "Light Filling", Code = "SVC-MULTI-2", DefaultPrice = 8000 };
        var svc3 = new ClinicService { ArabicName = "علاج عصب", EnglishName = "Root Canal", Code = "SVC-MULTI-3", DefaultPrice = 15000 };
        db.ClinicServices.AddRange(svc2, svc3);
        await db.SaveChangesAsync();

        db.TreatmentPackageServices.AddRange(
            new TreatmentPackageService { PackageId = pkg.Id, ClinicServiceId = svc1.Id, Quantity = 2 },
            new TreatmentPackageService { PackageId = pkg.Id, ClinicServiceId = svc2.Id, Quantity = 1 },
            new TreatmentPackageService { PackageId = pkg.Id, ClinicServiceId = svc3.Id, Quantity = 1, OverridePrice = 12000m }
        );
        await db.SaveChangesAsync();

        var loaded = await db.TreatmentPackages
            .Include(p => p.Services)
            .FirstAsync(p => p.Id == pkg.Id);

        loaded.Services.Should().HaveCount(3);
        loaded.Services.Select(s => s.ClinicServiceId).Should().Contain([svc1.Id, svc2.Id, svc3.Id]);
        loaded.Services.First(s => s.ClinicServiceId == svc3.Id).OverridePrice.Should().Be(12000m);
    }

    // ─── Contract.PackageId (nullable FK to TreatmentPackages) ──────────────────

    [Fact]
    public async Task Contract_PackageId_IsNullable_DefaultsNull()
    {
        await using var db = CreateContext();
        var patient = new Patient { FirstName = "محمد", LastName = "عقلان", PatientNumber = "P-001" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            PatientId = patient.Id,
            TotalAmount = 30000,
            DownPayment = 0,
            InstallmentsCount = 1,
            Status = ContractStatus.Active,
            // PackageId not set
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var saved = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        saved.PackageId.Should().BeNull();
    }

    [Fact]
    public async Task Contract_PackageId_CanBeSetAndRead()
    {
        await using var db = CreateContext();
        var (pkg, _) = SeedPackageAndService(db, "SVC-CONTRACT-1");
        var patient = new Patient { FirstName = "سعيد", LastName = "العمري", PatientNumber = "P-002" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            PatientId = patient.Id,
            TotalAmount = 30000,
            DownPayment = 5000,
            InstallmentsCount = 5,
            Status = ContractStatus.Active,
            PackageId = pkg.Id,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var saved = await db.Contracts
            .Include(c => c.Package)
            .FirstAsync(c => c.Id == contract.Id);
        saved.PackageId.Should().Be(pkg.Id);
        saved.Package.Should().NotBeNull();
        saved.Package!.Name.Should().Be("باقة تبييض كاملة");
    }

    [Fact]
    public async Task Contract_PackageId_CanBeCleared_BySettingNull()
    {
        await using var db = CreateContext();
        var (pkg, _) = SeedPackageAndService(db, "SVC-CLEAR-1");
        var patient = new Patient { FirstName = "أحمد", LastName = "الحاج", PatientNumber = "P-003" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            PatientId = patient.Id,
            TotalAmount = 10000,
            DownPayment = 0,
            InstallmentsCount = 1,
            Status = ContractStatus.Active,
            PackageId = pkg.Id,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        contract.PackageId = null;
        await db.SaveChangesAsync();

        var saved = await db.Contracts.FirstAsync(c => c.Id == contract.Id);
        saved.PackageId.Should().BeNull();
    }
}
