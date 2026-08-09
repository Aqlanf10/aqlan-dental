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

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// CORE-XMOD-003 — repairing the legacy YER scalar on rows written before the currency guard
/// reached <c>SupplierBillsController</c>.
/// <para>
/// The repair recomputes rather than adjusts. Under the rule every writer now follows, the
/// scalar is the unpaid total of the supplier's YER bills and nothing else — a fully derived
/// value, so the operation is idempotent and cannot invent a number.
/// </para>
/// </summary>
public class SupplierBalanceDriftRepairTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"balance-drift-{Guid.NewGuid()}")
            .Options);

    private static SuppliersController BuildController(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        return new SuppliersController(
            db,
            new Mock<ILogger<SuppliersController>>().Object,
            new SupplierBalanceReader(db),
            new SupplierAgingReader(db),
            currentUser.Object);
    }

    private static Supplier SeedSupplier(AppDbContext db, decimal storedBalance, string name = "معمل الأمل")
    {
        var supplier = new Supplier
        {
            Name = name,
            Type = SupplierType.DentalLab,
            Balance = storedBalance,
            IsActive = true,
        };
        db.Suppliers.Add(supplier);
        return supplier;
    }

    private static SupplierBill Bill(
        Guid supplierId, decimal total, decimal paid, string currency,
        BillStatus status = BillStatus.Unpaid)
        => new()
        {
            BillNumber = $"BILL-{Guid.NewGuid():N}"[..14],
            SupplierId = supplierId,
            Description = "تركيبات",
            TotalAmount = total,
            PaidAmount = paid,
            Currency = currency,
            ExchangeRateToYer = currency == "YER" ? 1m : 68m,
            Status = status,
            BillDate = new DateOnly(2026, 7, 1),
            BranchId = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            IsActive = true,
        };

    private static int Count(IActionResult result, string property)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return (int)ok.Value!.GetType().GetProperty(property)!.GetValue(ok.Value)!;
    }

    [Fact]
    public async Task DriftPreview_FindsAScalarThatSwallowedAForeignBill()
    {
        // The production shape: 90,000 YER owed, plus a 1,000 SAR bill whose raw figure was
        // added into the same column before the guard existed.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 91_000m);
        db.SupplierBills.Add(Bill(supplier.Id, 90_000m, 0m, "YER"));
        db.SupplierBills.Add(Bill(supplier.Id, 1_000m, 0m, "SAR"));
        await db.SaveChangesAsync();

        var result = await BuildController(db).GetBalanceDrift();

        Count(result, "count").Should().Be(1);
    }

    [Fact]
    public async Task Repair_RestoresTheScalarToTheYerBillsOnly()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 91_000m);
        db.SupplierBills.Add(Bill(supplier.Id, 90_000m, 0m, "YER"));
        db.SupplierBills.Add(Bill(supplier.Id, 1_000m, 0m, "SAR"));
        await db.SaveChangesAsync();

        Count(await BuildController(db).RepairBalanceDrift(), "repaired").Should().Be(1);

        (await db.Suppliers.FirstAsync(s => s.Id == supplier.Id)).Balance
            .Should().Be(90_000m, "the SAR bill never belonged in a Yemeni-riyal column");
    }

    [Fact]
    public async Task Repair_IsIdempotent_SoRunningItTwiceIsSafe()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 91_000m);
        db.SupplierBills.Add(Bill(supplier.Id, 90_000m, 0m, "YER"));
        db.SupplierBills.Add(Bill(supplier.Id, 1_000m, 0m, "SAR"));
        await db.SaveChangesAsync();

        await BuildController(db).RepairBalanceDrift();
        var second = await BuildController(db).RepairBalanceDrift();

        Count(second, "repaired").Should().Be(0, "the value is derived, so the second run has nothing to do");
        (await db.Suppliers.FirstAsync(s => s.Id == supplier.Id)).Balance.Should().Be(90_000m);
    }

    [Fact]
    public async Task Repair_AccountsForPaymentsAndIgnoresCancelledBills()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 0m);
        db.SupplierBills.Add(Bill(supplier.Id, 100_000m, 40_000m, "YER", BillStatus.PartiallyPaid));
        db.SupplierBills.Add(Bill(supplier.Id, 70_000m, 0m, "YER", BillStatus.Cancelled));
        await db.SaveChangesAsync();

        await BuildController(db).RepairBalanceDrift();

        (await db.Suppliers.FirstAsync(s => s.Id == supplier.Id)).Balance
            .Should().Be(60_000m, "a cancelled bill is not owed, and 40,000 of the rest is paid");
    }

    [Fact]
    public async Task ASupplierAlreadyCorrect_IsLeftAlone()
    {
        // Guards against a repair that rewrites every row whether it needed it or not, which
        // would bury the real corrections in an audit log full of no-ops.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 50_000m);
        db.SupplierBills.Add(Bill(supplier.Id, 50_000m, 0m, "YER"));
        await db.SaveChangesAsync();

        Count(await BuildController(db).GetBalanceDrift(), "count").Should().Be(0);
        Count(await BuildController(db).RepairBalanceDrift(), "repaired").Should().Be(0);
    }

    [Fact]
    public async Task ASupplierWithOnlyForeignBills_IsRepairedToZero_NotLeftInflated()
    {
        // The worst case: every bill is foreign, so the correct YER scalar is zero and
        // whatever the column holds is pure contamination.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 4_000m);
        db.SupplierBills.Add(Bill(supplier.Id, 4_000m, 0m, "SAR"));
        await db.SaveChangesAsync();

        await BuildController(db).RepairBalanceDrift();

        (await db.Suppliers.FirstAsync(s => s.Id == supplier.Id)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task Repair_WritesAnAuditEntryCarryingBothNumbers()
    {
        // A silent rewrite of a financial column is indistinguishable from corruption when
        // someone looks at it a year later.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db, storedBalance: 91_000m);
        db.SupplierBills.Add(Bill(supplier.Id, 90_000m, 0m, "YER"));
        await db.SaveChangesAsync();

        await BuildController(db).RepairBalanceDrift();

        var log = await db.AuditLogs.SingleAsync(a => a.Resource == "Supplier.Balance");
        log.ResourceId.Should().Be(supplier.Id);

        var payload = log.NewData!.RootElement;
        payload.GetProperty("storedBalance").GetDecimal().Should().Be(91_000m);
        payload.GetProperty("correctBalance").GetDecimal().Should().Be(90_000m);
    }

    [Fact]
    public async Task Repair_TouchesEverySupplierThatDrifted_NotJustTheFirst()
    {
        await using var db = CreateDb();
        var first = SeedSupplier(db, storedBalance: 91_000m, name: "معمل الأمل");
        var second = SeedSupplier(db, storedBalance: 500m, name: "معمل النور");
        db.SupplierBills.Add(Bill(first.Id, 90_000m, 0m, "YER"));
        db.SupplierBills.Add(Bill(second.Id, 500m, 0m, "USD"));
        await db.SaveChangesAsync();

        Count(await BuildController(db).RepairBalanceDrift(), "repaired").Should().Be(2);

        (await db.Suppliers.FirstAsync(s => s.Id == first.Id)).Balance.Should().Be(90_000m);
        (await db.Suppliers.FirstAsync(s => s.Id == second.Id)).Balance.Should().Be(0m);
    }
}
