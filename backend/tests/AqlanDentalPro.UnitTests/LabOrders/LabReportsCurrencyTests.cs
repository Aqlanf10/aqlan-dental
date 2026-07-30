using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// CORE-LAB-010 / CORE-LAB-011. Two rules the lab reports were breaking at once:
/// <list type="bullet">
/// <item>
/// Amounts in different currencies were added together. 300,000 YER plus 100,000 SAR was
/// reported as "400,000" — a figure that is not money in any currency and understates the SAR
/// part by roughly sixty-eight times.
/// </item>
/// <item>
/// Draft and cancelled orders counted toward "total lab cost". A draft was never sent and a
/// cancelled order is not owed, so both inflate what the clinic believes it spent — the same
/// boundary the supplier bill and the commission deduction already draw.
/// </item>
/// </list>
/// </summary>
public class LabReportsCurrencyTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"lab-reports-{Guid.NewGuid()}")
            .Options);

    private static LabReportsController BuildController(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUser.SetupGet(c => c.BranchId).Returns(Guid.NewGuid());

        // The real reader against the same in-memory db — a mock would let the shared
        // derivation drift away from what these tests assert about it.
        return new LabReportsController(db, currentUser.Object, new SupplierBalanceReader(db));
    }

    private static Guid SeedLab(AppDbContext db, string name, out Guid supplierId)
    {
        var supplier = new Supplier { Name = name, Type = SupplierType.DentalLab, IsActive = true };
        db.Suppliers.Add(supplier);
        var lab = new LabEntity { Name = name, SupplierId = supplier.Id, IsActive = true };
        db.Labs.Add(lab);
        supplierId = supplier.Id;
        return lab.Id;
    }

    private static Guid SeedPatient(AppDbContext db)
    {
        var patient = new Patient
        {
            PatientNumber = $"P-{Guid.NewGuid():N}"[..12],
            FirstName = "سالم",
            LastName = "المريض",
            IsActive = true,
        };
        db.Patients.Add(patient);
        return patient.Id;
    }

    private static LabOrder Order(
        Guid patientId, Guid labId, decimal cost, string currency = "YER",
        decimal rate = 1m, string status = "sent")
        => new()
        {
            PatientId = patientId,
            LabId = labId,
            OrderNumber = $"LO-{Guid.NewGuid():N}"[..14],
            ApplianceType = "تاج زيركون",
            Status = status,
            Priority = "normal",
            Cost = cost,
            TotalCost = cost,
            Currency = currency,
            ExchangeRateToYer = rate,
            IsActive = true,
        };

    // ── Reflection helpers over the anonymous response shapes ────────────────

    private static object Payload(IActionResult result, string property)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value!.GetType().GetProperty(property)!.GetValue(ok.Value)!;
    }

    private static List<object> Rows(object value)
        => ((System.Collections.IEnumerable)value).Cast<object>().ToList();

    private static T Read<T>(object row, string property)
        => (T)row.GetType().GetProperty(property)!.GetValue(row)!;

    /// <summary>Flattens a per-currency amount list into "YER" → amount for easy assertions.</summary>
    private static Dictionary<string, decimal> ByCurrency(object amounts)
        => Rows(amounts).ToDictionary(
            a => Read<string>(a, "Currency"),
            a => Read<decimal>(a, "Amount"));

    // ── Costs ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LabCosts_KeepsEachCurrencySeparate_InsteadOfAddingThemTogether()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        var labId = SeedLab(db, "معمل الأمل", out _);

        db.LabOrders.Add(Order(patientId, labId, 300_000m, "YER"));
        db.LabOrders.Add(Order(patientId, labId, 100_000m, "SAR", rate: 68m));
        await db.SaveChangesAsync();

        var result = await BuildController(db).GetLabCosts(null, null);

        var row = Rows(Payload(result, "data")).Should().ContainSingle().Subject;
        var costs = ByCurrency(Read<object>(row, "TotalCostByCurrency"));

        costs.Should().HaveCount(2, "a YER cost and a SAR cost are two different totals");
        costs["YER"].Should().Be(300_000m);
        costs["SAR"].Should().Be(100_000m);

        // And the grand total must never collapse to a single scalar either.
        ByCurrency(Payload(result, "totalsByCurrency"))
            .Should().BeEquivalentTo(new Dictionary<string, decimal> { ["YER"] = 300_000m, ["SAR"] = 100_000m });
    }

    [Fact]
    public async Task LabCosts_ExcludeDraftAndCancelled_ButStillCountThemAsOrders()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        var labId = SeedLab(db, "معمل الأمل", out _);

        db.LabOrders.Add(Order(patientId, labId, 50_000m, status: "sent"));
        db.LabOrders.Add(Order(patientId, labId, 90_000m, status: "draft"));
        db.LabOrders.Add(Order(patientId, labId, 70_000m, status: "cancelled"));
        await db.SaveChangesAsync();

        var result = await BuildController(db).GetLabCosts(null, null);
        var row = Rows(Payload(result, "data")).Should().ContainSingle().Subject;

        ByCurrency(Read<object>(row, "TotalCostByCurrency"))["YER"]
            .Should().Be(50_000m, "only the committed order is a cost the clinic owes");

        Read<int>(row, "TotalOrders").Should().Be(3,
            "order counts describe everything — it is the money that is restricted to commitments");
    }

    [Fact]
    public async Task LabCosts_ReportsEachLabSeparately()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        var first = SeedLab(db, "معمل الأمل", out _);
        var second = SeedLab(db, "معمل النور", out _);

        db.LabOrders.Add(Order(patientId, first, 40_000m));
        db.LabOrders.Add(Order(patientId, second, 25_000m));
        await db.SaveChangesAsync();

        var rows = Rows(Payload(await BuildController(db).GetLabCosts(null, null), "data"));

        rows.Should().HaveCount(2);
        rows.Select(r => ByCurrency(Read<object>(r, "TotalCostByCurrency"))["YER"])
            .Should().BeEquivalentTo(new[] { 40_000m, 25_000m });
    }

    // ── Debts ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LabDebts_CarryTheirOwnCurrency_AndAreTotalledPerCurrency()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        var labId = SeedLab(db, "معمل الأمل", out var supplierId);

        foreach (var (amount, paid, currency) in new[]
                 {
                     (200_000m, 50_000m, "YER"),
                     (1_000m, 0m, "SAR"),
                 })
        {
            var order = Order(patientId, labId, amount, currency);
            db.LabOrders.Add(order);

            var bill = new SupplierBill
            {
                BillNumber = $"BILL-{Guid.NewGuid():N}"[..14],
                SupplierId = supplierId,
                Description = "طلب معمل",
                TotalAmount = amount,
                Currency = currency,
                ExchangeRateToYer = currency == "YER" ? 1m : 68m,
                Status = BillStatus.Unpaid,
                BillDate = new DateOnly(2026, 7, 1),
                BranchId = Guid.NewGuid(),
                CreatedBy = Guid.NewGuid(),
                LabOrderId = order.Id,
                IsActive = true,
            };
            db.SupplierBills.Add(bill);

            db.LabPayables.Add(new LabPayable
            {
                LabOrderId = order.Id,
                LabId = labId,
                SupplierBillId = bill.Id,
                Amount = amount,
                PaidAmount = paid,
                Status = paid > 0 ? "partial" : "pending",
                IsActive = true,
            });
        }
        await db.SaveChangesAsync();

        var result = await BuildController(db).GetLabDebts(null);

        var rows = Rows(Payload(result, "data"));
        rows.Should().HaveCount(2);
        rows.Select(r => Read<string>(r, "Currency")).Should().BeEquivalentTo(["YER", "SAR"]);

        var summary = Payload(result, "summary");
        var byCurrency = ByCurrency(summary.GetType().GetProperty("TotalDebtByCurrency")!.GetValue(summary)!);

        byCurrency["YER"].Should().Be(150_000m);
        byCurrency["SAR"].Should().Be(1_000m);
    }

    // ── Dashboard ────────────────────────────────────────────────────────────

    // ── Lab accounts (CORE-LAB-016) ──────────────────────────────────────────

    [Fact]
    public async Task LabAccounts_ShowABalanceForALabThatInvoicesInSaudiRiyals()
    {
        // Supplier.Balance is a single scalar and the lab finance sync only moves it for YER
        // bills, so a lab invoicing in SAR shows zero owed there no matter the debt. The
        // account is derived from the bills, which carry the currency.
        await using var db = CreateDb();
        SeedLab(db, "معمل الرياض", out var supplierId);

        db.SupplierBills.Add(new SupplierBill
        {
            BillNumber = "BILL-SAR-1", SupplierId = supplierId, Description = "تركيبات",
            TotalAmount = 4_000m, PaidAmount = 1_500m, Currency = "SAR", ExchangeRateToYer = 68m,
            Status = BillStatus.PartiallyPaid, BillDate = new DateOnly(2026, 7, 1),
            BranchId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), IsActive = true,
        });
        db.SupplierBills.Add(new SupplierBill
        {
            BillNumber = "BILL-YER-1", SupplierId = supplierId, Description = "تركيبات",
            TotalAmount = 90_000m, PaidAmount = 0m, Currency = "YER", ExchangeRateToYer = 1m,
            Status = BillStatus.Unpaid, BillDate = new DateOnly(2026, 7, 2),
            BranchId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), IsActive = true,
        });
        await db.SaveChangesAsync();

        // The scalar the suppliers module reads is untouched — that is the gap being worked around.
        (await db.Suppliers.FirstAsync(x => x.Id == supplierId)).Balance.Should().Be(0m);

        var account = Rows(Payload(await BuildController(db).GetLabAccounts(), "data"))
            .Should().ContainSingle().Subject;

        Read<string>(account, "LabName").Should().Be("معمل الرياض");

        var balance = ByCurrency(Read<object>(account, "BalanceByCurrency"));
        balance["SAR"].Should().Be(2_500m, "4,000 billed less 1,500 paid, in riyals — not converted");
        balance["YER"].Should().Be(90_000m);

        ByCurrency(Read<object>(account, "BilledByCurrency"))["SAR"].Should().Be(4_000m);
        ByCurrency(Read<object>(account, "PaidByCurrency"))["SAR"].Should().Be(1_500m);
    }

    [Fact]
    public async Task LabAccounts_ExcludeSuppliersThatAreNotDentalLabs()
    {
        await using var db = CreateDb();
        var vendor = new Supplier { Name = "مورد مستلزمات", Type = SupplierType.MedicalVendor, IsActive = true };
        db.Suppliers.Add(vendor);
        db.SupplierBills.Add(new SupplierBill
        {
            BillNumber = "BILL-V-1", SupplierId = vendor.Id, Description = "مواد",
            TotalAmount = 10_000m, Currency = "YER", ExchangeRateToYer = 1m,
            Status = BillStatus.Unpaid, BillDate = new DateOnly(2026, 7, 1),
            BranchId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), IsActive = true,
        });
        await db.SaveChangesAsync();

        Rows(Payload(await BuildController(db).GetLabAccounts(), "data")).Should().BeEmpty();
    }

    [Fact]
    public async Task Performance_ServesItsThresholdsFromSettings_NotFromTheScreen()
    {
        await using var db = CreateDb();
        db.Settings.Add(new Setting
        {
            Key = "finance.lab.remake_rate_alarm", Value = "3",
            Category = "finance",
        });
        await db.SaveChangesAsync();

        var thresholds = Payload(await BuildController(db).GetLabPerformance(null, null), "thresholds");

        thresholds.GetType().GetProperty("RemakeRateAlarm")!.GetValue(thresholds)
            .Should().Be(3m, "the owner's configured value must win over the old literal");
        thresholds.GetType().GetProperty("TurnaroundDaysTarget")!.GetValue(thresholds)
            .Should().Be(7m, "an unset key keeps the previous hardcoded default");
    }

    [Fact]
    public async Task Dashboard_TotalCosts_ArePerCurrencyAndCommittedOnly()
    {
        await using var db = CreateDb();
        var patientId = SeedPatient(db);
        var labId = SeedLab(db, "معمل الأمل", out _);

        db.LabOrders.Add(Order(patientId, labId, 80_000m, "YER"));
        db.LabOrders.Add(Order(patientId, labId, 500m, "USD", rate: 530m));
        db.LabOrders.Add(Order(patientId, labId, 999_000m, "YER", status: "draft"));
        await db.SaveChangesAsync();

        var dashboard = Payload(await BuildController(db).GetLabDashboard(), "data");
        var kpis = dashboard.GetType().GetProperty("KPIs")!.GetValue(dashboard)!;
        var costs = ByCurrency(kpis.GetType().GetProperty("TotalLabCostsByCurrency")!.GetValue(kpis)!);

        costs["YER"].Should().Be(80_000m, "the 999,000 draft is not a committed cost");
        costs["USD"].Should().Be(500m);
        costs.Should().NotContainKey("SAR");
    }
}
