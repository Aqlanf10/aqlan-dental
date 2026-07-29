using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// CORE-LAB-008: a debt owed to a lab from BEFORE the clinic started using the system is
/// entered as an opening-balance SupplierBill. That bill has no LabOrderId and therefore
/// no LabPayable row — and the lab payables screen queried LabPayables only, so the debt
/// was invisible there while showing correctly on the supplier account.
///
/// The row was not missing. The screen was reading the narrower of the two tables.
/// </summary>
public class LabPayableOpeningBalanceTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"lab-payable-opening-{Guid.NewGuid()}")
            .Options);

    private static LabPayablesController BuildController(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUser.SetupGet(c => c.BranchId).Returns(Guid.NewGuid());

        return new LabPayablesController(
            db,
            currentUser.Object,
            new Mock<ILogger<LabPayablesController>>().Object,
            new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object),
            new JournalEntryService(db, new Mock<ILogger<JournalEntryService>>().Object),
            new Mock<IAuditService>().Object);
    }

    /// <summary>Seeds a dental-lab supplier plus its Lab row, and returns both ids.</summary>
    private static async Task<(Guid supplierId, Guid labId)> SeedLabAsync(AppDbContext db, string name = "معمل الأمل")
    {
        var supplier = new Supplier { Name = name, Type = SupplierType.DentalLab, IsActive = true };
        db.Suppliers.Add(supplier);
        var lab = new LabEntity { Name = name, IsActive = true, SupplierId = supplier.Id };
        db.Labs.Add(lab);
        await db.SaveChangesAsync();
        return (supplier.Id, lab.Id);
    }

    private static SupplierBill BuildOpeningBalance(Guid supplierId, decimal amount) => new()
    {
        BillNumber = $"OB-{Guid.NewGuid():N}"[..12],
        SupplierId = supplierId,
        Description = "رصيد سابق قبل استخدام النظام",
        TotalAmount = amount,
        Currency = "YER",
        ExchangeRateToYer = 1m,
        Status = BillStatus.Unpaid,
        BillDate = new DateOnly(2026, 1, 1),
        BranchId = Guid.NewGuid(),
        CreatedBy = Guid.NewGuid(),
        IsOpeningBalance = true,
        IsActive = true,
    };

    /// <summary>Reads the anonymous `data` collection off the Ok payload by reflection.</summary>
    private static List<object> ExtractRows(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        return ((System.Collections.IEnumerable)data).Cast<object>().ToList();
    }

    private static T Read<T>(object row, string property)
        => (T)row.GetType().GetProperty(property)!.GetValue(row)!;

    [Fact]
    public async Task OpeningBalanceWithNoLabOrder_AppearsInLabPayables()
    {
        await using var db = CreateDb();
        var (supplierId, _) = await SeedLabAsync(db);
        db.SupplierBills.Add(BuildOpeningBalance(supplierId, 250_000m));
        await db.SaveChangesAsync();

        // Nothing in LabPayables at all — this is exactly the production situation.
        (await db.LabPayables.CountAsync()).Should().Be(0);

        var rows = ExtractRows(await BuildController(db).GetAll(labId: null, status: null, page: 1, pageSize: 20));

        rows.Should().HaveCount(1, "the opening balance is a real debt to the lab and must be listed");
        Read<decimal>(rows[0], "Amount").Should().Be(250_000m);
        Read<decimal>(rows[0], "Balance").Should().Be(250_000m);
        Read<string>(rows[0], "LabName").Should().Be("معمل الأمل");
    }

    [Fact]
    public async Task OpeningBalanceRow_IsFlaggedAsNotPayableFromThisScreen()
    {
        // RecordPayment takes a LabPayable id. An opening balance has none, so the row
        // must say so rather than offering a pay action that cannot work.
        await using var db = CreateDb();
        var (supplierId, _) = await SeedLabAsync(db);
        db.SupplierBills.Add(BuildOpeningBalance(supplierId, 100_000m));
        await db.SaveChangesAsync();

        var rows = ExtractRows(await BuildController(db).GetAll(null, null, 1, 20));

        Read<bool>(rows[0], "HasPayable").Should().BeFalse();
        Read<bool>(rows[0], "IsOpeningBalance").Should().BeTrue();
    }

    [Fact]
    public async Task BillThatAlreadyHasAPayable_IsNotListedTwice()
    {
        // The order-driven path creates BOTH a SupplierBill and a LabPayable. Appending
        // orphan bills must not duplicate those.
        await using var db = CreateDb();
        var (supplierId, labId) = await SeedLabAsync(db);

        var bill = BuildOpeningBalance(supplierId, 40_000m);
        bill.IsOpeningBalance = false;
        db.SupplierBills.Add(bill);
        db.LabPayables.Add(new LabPayable
        {
            LabOrderId = Guid.NewGuid(),
            LabId = labId,
            SupplierBillId = bill.Id,
            Amount = 40_000m,
            PaidAmount = 0,
            Status = "pending",
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var rows = ExtractRows(await BuildController(db).GetAll(null, null, 1, 20));

        rows.Should().HaveCount(1, "a bill that already has a payable must appear once, not twice");
        Read<bool>(rows[0], "HasPayable").Should().BeTrue();
    }

    [Fact]
    public async Task NonLabSupplierBill_IsNotListed()
    {
        // Only dental labs belong on this screen — a medical-vendor bill must not leak in.
        await using var db = CreateDb();
        var vendor = new Supplier { Name = "مورد مستلزمات", Type = SupplierType.MedicalVendor, IsActive = true };
        db.Suppliers.Add(vendor);
        db.SupplierBills.Add(BuildOpeningBalance(vendor.Id, 70_000m));
        await db.SaveChangesAsync();

        ExtractRows(await BuildController(db).GetAll(null, null, 1, 20)).Should().BeEmpty();
    }

    [Fact]
    public async Task LabFilter_StillMatchesAnOpeningBalanceThroughItsSupplier()
    {
        // An opening balance carries no LabId of its own, so filtering by lab has to
        // resolve through the lab's SupplierId or the row would vanish when filtered.
        await using var db = CreateDb();
        var (supplierId, labId) = await SeedLabAsync(db);
        db.SupplierBills.Add(BuildOpeningBalance(supplierId, 30_000m));

        var (otherSupplierId, otherLabId) = await SeedLabAsync(db, "معمل آخر");
        db.SupplierBills.Add(BuildOpeningBalance(otherSupplierId, 55_000m));
        await db.SaveChangesAsync();

        var mine = ExtractRows(await BuildController(db).GetAll(labId, null, 1, 20));
        mine.Should().HaveCount(1);
        Read<decimal>(mine[0], "Amount").Should().Be(30_000m);

        var theirs = ExtractRows(await BuildController(db).GetAll(otherLabId, null, 1, 20));
        theirs.Should().HaveCount(1);
        Read<decimal>(theirs[0], "Amount").Should().Be(55_000m);
    }

    [Fact]
    public async Task StatusFilter_AppliesToOpeningBalancesToo()
    {
        await using var db = CreateDb();
        var (supplierId, _) = await SeedLabAsync(db);

        db.SupplierBills.Add(BuildOpeningBalance(supplierId, 10_000m));           // Unpaid
        var settled = BuildOpeningBalance(supplierId, 20_000m);
        settled.Status = BillStatus.FullyPaid;
        settled.PaidAmount = 20_000m;
        db.SupplierBills.Add(settled);
        await db.SaveChangesAsync();

        var pending = ExtractRows(await BuildController(db).GetAll(null, "pending", 1, 20));
        pending.Should().HaveCount(1);
        Read<decimal>(pending[0], "Amount").Should().Be(10_000m);

        var paid = ExtractRows(await BuildController(db).GetAll(null, "paid", 1, 20));
        paid.Should().HaveCount(1);
        Read<decimal>(paid[0], "Amount").Should().Be(20_000m);
    }
}
