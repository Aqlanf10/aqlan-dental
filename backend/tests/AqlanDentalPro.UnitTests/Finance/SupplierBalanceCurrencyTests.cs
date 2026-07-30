using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// CORE-XMOD-001 — "what does the clinic owe this supplier" is one figure PER CURRENCY.
/// <para>
/// <c>Supplier.Balance</c> is a single scalar with no currency. Three of the four writers
/// already guarded themselves with <c>if (currency == "YER")</c>; the legacy
/// <c>SupplierBillsController</c> did not, so a SAR bill added its raw riyal figure into a
/// column holding Yemeni riyals. These tests pin the derived balance, which is what every
/// screen now reads, and the fold rule that keeps pre-currency rows out of a nameless bucket.
/// </para>
/// </summary>
public class SupplierBalanceCurrencyTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"supplier-balance-{Guid.NewGuid()}")
            .Options);

    private static Supplier SeedSupplier(AppDbContext db, string name = "معمل الأمل")
    {
        var supplier = new Supplier { Name = name, Type = SupplierType.DentalLab, IsActive = true };
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

    [Fact]
    public async Task ThreeCurrencies_ProduceThreeBalances_NeverOneTotal()
    {
        // The clinic's real situation: one lab bills in Yemeni riyals, one in Saudi riyals,
        // one in dollars. 200,000 + 1,000 + 500 is not 201,500 of anything.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        db.SupplierBills.Add(Bill(supplier.Id, 200_000m, 50_000m, "YER"));
        db.SupplierBills.Add(Bill(supplier.Id, 1_000m, 0m, "SAR"));
        db.SupplierBills.Add(Bill(supplier.Id, 500m, 200m, "USD"));
        await db.SaveChangesAsync();

        var balances = await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id);

        balances.Should().HaveCount(3);
        balances.Single(b => b.Currency == "YER").Balance.Should().Be(150_000m);
        balances.Single(b => b.Currency == "SAR").Balance.Should().Be(1_000m);
        balances.Single(b => b.Currency == "USD").Balance.Should().Be(300m);
    }

    [Fact]
    public async Task BilledAndPaidAreReportedSeparately_NotJustTheNet()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        db.SupplierBills.Add(Bill(supplier.Id, 4_000m, 1_500m, "SAR", BillStatus.PartiallyPaid));
        await db.SaveChangesAsync();

        var row = (await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id)).Single();

        row.TotalBilled.Should().Be(4_000m);
        row.TotalPaid.Should().Be(1_500m);
        row.Balance.Should().Be(2_500m);
        row.OpenBills.Should().Be(1);
    }

    [Fact]
    public async Task ABillWithNoCurrency_FoldsIntoYer_NotIntoANamelessBucket()
    {
        // Rows written before the currency column existed read as empty. The entity default is
        // YER, so they belong there rather than in a separate "" group the UI cannot label.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        db.SupplierBills.Add(Bill(supplier.Id, 30_000m, 0m, ""));
        db.SupplierBills.Add(Bill(supplier.Id, 20_000m, 0m, "YER"));
        await db.SaveChangesAsync();

        var balances = await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id);

        balances.Should().ContainSingle();
        balances[0].Currency.Should().Be("YER");
        balances[0].Balance.Should().Be(50_000m);
    }

    [Fact]
    public async Task FullyPaidBills_StillCountAsBilled_ButNotAsOpen()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        db.SupplierBills.Add(Bill(supplier.Id, 10_000m, 10_000m, "YER", BillStatus.FullyPaid));
        await db.SaveChangesAsync();

        var row = (await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id)).Single();

        row.TotalBilled.Should().Be(10_000m);
        row.Balance.Should().Be(0m, "a settled bill owes nothing");
        row.OpenBills.Should().Be(0);
    }

    [Fact]
    public async Task CancelledBills_AreNotOpen_SoTheyCannotLookLikeADebt()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        db.SupplierBills.Add(Bill(supplier.Id, 70_000m, 0m, "YER", BillStatus.Cancelled));
        await db.SaveChangesAsync();

        var row = (await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id)).Single();

        row.OpenBills.Should().Be(0);
    }

    [Fact]
    public async Task OneSuppliersBillsNeverLeakIntoAnothersBalance()
    {
        await using var db = CreateDb();
        var first = SeedSupplier(db, "معمل الأمل");
        var second = SeedSupplier(db, "معمل النور");
        db.SupplierBills.Add(Bill(first.Id, 80_000m, 0m, "YER"));
        db.SupplierBills.Add(Bill(second.Id, 25_000m, 0m, "YER"));
        await db.SaveChangesAsync();

        var balances = await new SupplierBalanceReader(db).GetAsync([first.Id, second.Id]);

        balances.Single(b => b.SupplierId == first.Id).Balance.Should().Be(80_000m);
        balances.Single(b => b.SupplierId == second.Id).Balance.Should().Be(25_000m);
    }

    [Fact]
    public async Task BranchScoping_LimitsTheBalanceToThatBranchesBills()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        var branch = Guid.NewGuid();

        var mine = Bill(supplier.Id, 40_000m, 0m, "YER");
        mine.BranchId = branch;
        db.SupplierBills.Add(mine);
        db.SupplierBills.Add(Bill(supplier.Id, 90_000m, 0m, "YER"));   // another branch
        await db.SaveChangesAsync();

        var scoped = await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id, branchId: branch);

        scoped.Should().ContainSingle();
        scoped[0].Balance.Should().Be(40_000m);
    }

    [Fact]
    public async Task ASupplierWithNoBills_ReturnsNoRows_RatherThanAFabricatedZero()
    {
        // Inventing a "0 YER" row for a supplier who only ever invoices in dollars would be
        // exactly the confusion the per-currency model exists to remove.
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        await db.SaveChangesAsync();

        (await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task SoftDeletedBills_AreExcluded()
    {
        await using var db = CreateDb();
        var supplier = SeedSupplier(db);
        var voided = Bill(supplier.Id, 55_000m, 0m, "YER");
        voided.IsActive = false;
        db.SupplierBills.Add(voided);
        await db.SaveChangesAsync();

        (await new SupplierBalanceReader(db).GetForSupplierAsync(supplier.Id)).Should().BeEmpty();
    }
}
