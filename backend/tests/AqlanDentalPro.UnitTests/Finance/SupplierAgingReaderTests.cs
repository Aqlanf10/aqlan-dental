using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// CORE-LAB-020 — the ageing of unpaid supplier bills.
/// <para>
/// The assertions worth having here are the ones about money going missing or landing in the
/// wrong column: a bill with no due date, a bill due exactly today, a bill sitting exactly on a
/// bucket boundary, and two currencies that must never be added together.
/// </para>
/// </summary>
public class SupplierAgingReaderTests
{
    private static readonly DateOnly AsOf = new(2026, 8, 9);

    /// <summary>30 / 60 / 90, the default terms.</summary>
    private static readonly SupplierAgingReader.AgingBuckets Buckets = new(30, 60, 90);

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"aging-{Guid.NewGuid()}")
            .Options);

    private static Guid SeedSupplier(AppDbContext db, string name, SupplierType type = SupplierType.DentalLab)
    {
        var supplier = new Supplier { Name = name, Type = type, IsActive = true };
        db.Suppliers.Add(supplier);
        db.SaveChanges();
        return supplier.Id;
    }

    private static void SeedBill(
        AppDbContext db, Guid supplierId, decimal total, decimal paid,
        DateOnly? dueDate, string currency = "YER", BillStatus status = BillStatus.Unpaid)
    {
        db.SupplierBills.Add(new SupplierBill
        {
            SupplierId = supplierId,
            BillNumber = $"SB-{Guid.NewGuid():N}"[..12],
            TotalAmount = total,
            PaidAmount = paid,
            Currency = currency,
            ExchangeRateToYer = 1m,
            BillDate = AsOf.AddDays(-120),
            DueDate = dueDate,
            Status = status,
            BranchId = Guid.Empty,
            IsActive = true,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task BillDueExactlyToday_IsNotYetDue_NotOverdueByZeroDays()
    {
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل اليوم");
        SeedBill(db, supplierId, 100_000m, 0m, AsOf);

        var rows = await new SupplierAgingReader(db).GetAsync(AsOf, Buckets);

        var row = rows.Should().ContainSingle().Subject;
        row.NotYetDue.Should().Be(100_000m);
        row.Bucket1.Should().Be(0m);
        row.OldestOverdueDays.Should().BeNull("a bill due today is due today, not one day late");
    }

    [Theory]
    // Exactly on each boundary, and one day past it — this is where an off-by-one puts real
    // money in the wrong column and makes a supplier look healthier than they are.
    [InlineData(1, "b1")]
    [InlineData(30, "b1")]
    [InlineData(31, "b2")]
    [InlineData(60, "b2")]
    [InlineData(61, "b3")]
    [InlineData(90, "b3")]
    [InlineData(91, "b4")]
    public async Task BillLandsInTheBucketItsAgeBelongsTo(int daysPastDue, string expectedBucket)
    {
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل الحدود");
        SeedBill(db, supplierId, 50_000m, 0m, AsOf.AddDays(-daysPastDue));

        var row = (await new SupplierAgingReader(db).GetAsync(AsOf, Buckets)).Should().ContainSingle().Subject;

        var actual = new Dictionary<string, decimal>
        {
            ["b1"] = row.Bucket1,
            ["b2"] = row.Bucket2,
            ["b3"] = row.Bucket3,
            ["b4"] = row.Bucket4Plus,
        };

        actual[expectedBucket].Should().Be(50_000m);
        actual.Where(kv => kv.Key != expectedBucket).Should().OnlyContain(kv => kv.Value == 0m);
        row.OldestOverdueDays.Should().Be(daysPastDue);
    }

    [Fact]
    public async Task BillWithNoDueDate_IsReportedSeparately_NotTreatedAsCurrent()
    {
        // Folding it into "not yet due" would let a bill nobody agreed a date for sit in the
        // healthiest column forever, which is precisely how an old debt stops being visible.
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل بلا تاريخ");
        SeedBill(db, supplierId, 80_000m, 0m, dueDate: null);

        var row = (await new SupplierAgingReader(db).GetAsync(AsOf, Buckets)).Should().ContainSingle().Subject;

        row.NoDueDate.Should().Be(80_000m);
        row.NotYetDue.Should().Be(0m);
        row.Total.Should().Be(80_000m);
    }

    [Fact]
    public async Task CurrenciesAreAgedSeparately_NeverSummedTogether()
    {
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل بعملتين");
        SeedBill(db, supplierId, 300_000m, 0m, AsOf.AddDays(-40), "YER");
        SeedBill(db, supplierId, 4_000m, 0m, AsOf.AddDays(-40), "SAR");

        var rows = await new SupplierAgingReader(db).GetAsync(AsOf, Buckets);

        rows.Should().HaveCount(2, "one supplier billing in two currencies is two ageing rows, not one");
        rows.Single(r => r.Currency == "YER").Bucket2.Should().Be(300_000m);
        rows.Single(r => r.Currency == "SAR").Bucket2.Should().Be(4_000m);
        rows.Should().NotContain(r => r.Total == 304_000m,
            "304,000 is not an amount of money in any currency the clinic uses");
    }

    [Fact]
    public async Task OnlyTheUnpaidRemainderIsAged_NotTheFullBill()
    {
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل مدفوع جزئيًا");
        SeedBill(db, supplierId, 100_000m, 70_000m, AsOf.AddDays(-45), status: BillStatus.PartiallyPaid);

        var row = (await new SupplierAgingReader(db).GetAsync(AsOf, Buckets)).Should().ContainSingle().Subject;

        row.Bucket2.Should().Be(30_000m, "the clinic owes the remainder, not the original amount");
        row.Total.Should().Be(30_000m);
    }

    [Fact]
    public async Task SettledAndCancelledBillsAreNotAged()
    {
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل مغلق");
        SeedBill(db, supplierId, 100_000m, 100_000m, AsOf.AddDays(-200), status: BillStatus.FullyPaid);
        SeedBill(db, supplierId, 500_000m, 0m, AsOf.AddDays(-200), status: BillStatus.Cancelled);

        var rows = await new SupplierAgingReader(db).GetAsync(AsOf, Buckets);

        rows.Should().BeEmpty("a paid bill is not owed and a cancelled bill was never owed");
    }

    [Fact]
    public async Task SupplierTypeFilter_KeepsTheLabSliceToLabs()
    {
        // The lab workspace route is gated on lab_reports.view; the all-vendors route is
        // AdminOnly. If the filter leaked, a lab-reports viewer would learn what the clinic
        // owes every other vendor.
        using var db = NewDb();
        var labId = SeedSupplier(db, "معمل", SupplierType.DentalLab);
        var vendorId = SeedSupplier(db, "مورد مواد", SupplierType.MedicalVendor);
        SeedBill(db, labId, 100_000m, 0m, AsOf.AddDays(-10));
        SeedBill(db, vendorId, 900_000m, 0m, AsOf.AddDays(-10));

        var labsOnly = await new SupplierAgingReader(db).GetAsync(AsOf, Buckets, SupplierType.DentalLab);

        labsOnly.Should().ContainSingle().Which.SupplierId.Should().Be(labId);
    }

    [Fact]
    public async Task BucketBoundariesComeFromSettings_SoShorterTermsAgeFaster()
    {
        // Same bill, same day, different credit terms — a lab on net-7 is a month late where a
        // vendor on net-30 is not yet in the second bucket at all.
        using var db = NewDb();
        var supplierId = SeedSupplier(db, "معمل بشروط قصيرة");
        SeedBill(db, supplierId, 60_000m, 0m, AsOf.AddDays(-20));

        var reader = new SupplierAgingReader(db);

        var lenient = (await reader.GetAsync(AsOf, new SupplierAgingReader.AgingBuckets(30, 60, 90)))
            .Should().ContainSingle().Subject;
        lenient.Bucket1.Should().Be(60_000m);

        var strict = (await reader.GetAsync(AsOf, new SupplierAgingReader.AgingBuckets(7, 14, 21)))
            .Should().ContainSingle().Subject;
        strict.Bucket3.Should().Be(60_000m);
        strict.Bucket1.Should().Be(0m);
    }
}
