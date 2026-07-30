using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.UnitTests.Commissions;

/// <summary>
/// CORE-FIN-LAB-ADJ. The rule under test, stated once:
/// <list type="bullet">
/// <item>An UNPAID commission is recalculated in place when the actual lab cost changes.</item>
/// <item>A PAID commission is never rewritten — it gets a separate signed correction line.</item>
/// <item>Running the resync again with nothing changed must produce nothing.</item>
/// </list>
/// Every number below is checked against hand-computed arithmetic rather than against whatever
/// the service happens to return, so a sign flip or a double-count fails loudly.
/// </summary>
public class CommissionAdjustmentServiceTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"commission-adjust-{Guid.NewGuid()}")
            .Options);

    private static CommissionAdjustmentService BuildService(AppDbContext db)
        => new(db, new Mock<ILogger<CommissionAdjustmentService>>().Object);

    // ── Fixture ───────────────────────────────────────────────────────────────

    private sealed record Fixture(
        Guid DoctorId, Guid InvoiceId, Guid LineItemId, Guid LabOrderId);

    /// <summary>
    /// A 100,000 service, 40% doctor share, lab cost 20,000 → commission 32,000.
    /// Deliberately round numbers so every expectation below can be verified by hand.
    /// </summary>
    private static async Task<Fixture> SeedAsync(
        AppDbContext db,
        decimal labOrderCost = 20_000m,
        string labOrderStatus = "sent",
        CommissionStatus commissionStatus = CommissionStatus.Approved,
        string invoiceCurrency = "YER",
        string labCurrency = "YER",
        decimal labRate = 1m,
        decimal? frozenLabCostOverride = null)
    {
        var doctor = new Doctor { Name = "د. عقلان", UserId = Guid.NewGuid(), IsActive = true };
        db.Doctors.Add(doctor);

        var patient = new Patient
        {
            PatientNumber = $"P-{Guid.NewGuid():N}"[..12],
            FirstName = "سالم",
            LastName = "المريض",
            IsActive = true,
        };
        db.Patients.Add(patient);

        var supplier = new Supplier { Name = "معمل الأمل", Type = SupplierType.DentalLab, IsActive = true };
        db.Suppliers.Add(supplier);
        var lab = new LabEntity { Name = "معمل الأمل", SupplierId = supplier.Id, IsActive = true };
        db.Labs.Add(lab);

        var labOrder = new LabOrder
        {
            PatientId = patient.Id,
            OrderNumber = $"LO-{Guid.NewGuid():N}"[..14],
            ApplianceType = "تاج زيركون",
            Status = labOrderStatus,
            Priority = "normal",
            LabId = lab.Id,
            Cost = labOrderCost,
            TotalCost = labOrderCost,
            Currency = labCurrency,
            ExchangeRateToYer = labRate,
            IsActive = true,
        };
        db.LabOrders.Add(labOrder);

        var invoice = new Invoice
        {
            PatientId = patient.Id,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..14],
            Currency = invoiceCurrency,
            Status = InvoiceStatus.Issued,
            Subtotal = 100_000m,
            TotalAmount = 100_000m,
            IsActive = true,
        };
        db.Invoices.Add(invoice);

        // The lab cost baked into the line at the moment it reached its current status. The
        // whole point of the service is what happens when the order later disagrees with it.
        var frozenLabCost = frozenLabCostOverride ?? 20_000m;

        var line = new InvoiceLineItem
        {
            InvoiceId = invoice.Id,
            ServiceNameSnapshot = "تركيب تاج",
            Description = "تركيب تاج",
            Quantity = 1,
            UnitPrice = 100_000m,
            TotalPrice = 100_000m,
            DoctorId = doctor.Id,
            LabOrderId = labOrder.Id,
            LabCost = frozenLabCost,
            MaterialCost = 0m,
            OtherDirectCost = 0m,
            DoctorCommissionPercentage = 40m,
            CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts,
            CommissionStatus = commissionStatus,
            NetCommissionableAmount = 100_000m - frozenLabCost,
            DoctorCommissionAmount = (100_000m - frozenLabCost) * 0.40m,
            CenterShareAmount = (100_000m - frozenLabCost) * 0.60m,
            IsActive = true,
        };
        db.InvoiceLineItems.Add(line);

        await db.SaveChangesAsync();
        return new Fixture(doctor.Id, invoice.Id, line.Id, labOrder.Id);
    }

    // ── Unpaid: corrected in place ────────────────────────────────────────────

    [Fact]
    public async Task UnpaidCommission_IsRecalculatedInPlace_AndRaisesNoAdjustment()
    {
        await using var db = CreateDb();
        // Line frozen at 20,000 lab cost; the order actually costs 35,000.
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Approved);

        var result = await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        result.Recalculated.Should().Be(1);
        result.AdjustmentsRaised.Should().Be(0, "an unpaid commission has no history to protect");

        var line = await db.InvoiceLineItems.FindAsync(f.LineItemId);
        line!.LabCost.Should().Be(35_000m);
        // (100,000 − 35,000) × 40% = 26,000
        line.DoctorCommissionAmount.Should().Be(26_000m);
        line.NetCommissionableAmount.Should().Be(65_000m);

        (await db.DoctorCommissionAdjustments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CancelledLabOrder_ReleasesTheDeduction_OnAnUnpaidCommission()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 20_000m, labOrderStatus: "cancelled");

        await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        var line = await db.InvoiceLineItems.FindAsync(f.LineItemId);
        line!.LabCost.Should().Be(0m, "a cancelled order is not a cost the clinic owes");
        line.DoctorCommissionAmount.Should().Be(40_000m, "the full 100,000 is commissionable again");
    }

    [Fact]
    public async Task DraftLabOrder_LeavesTheLineExactlyAsItIs()
    {
        // A draft is not a decision in either direction. Writing its provisional cost would
        // deduct a bill the clinic has not committed to; writing zero would throw away the
        // service's own cost estimate and inflate the commission. So: don't touch it.
        // The status table makes this safe — draft only ever leads to sent or cancelled, so a
        // draft is always the initial state and never a retreat from a committed one.
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 55_000m, labOrderStatus: "draft");

        var result = await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        result.Recalculated.Should().Be(0);
        result.AdjustmentsRaised.Should().Be(0);
        result.Skipped.Should().BeEmpty("a draft is the ordinary case, not something to report");

        var line = await db.InvoiceLineItems.FindAsync(f.LineItemId);
        line!.LabCost.Should().Be(20_000m, "the line keeps whatever estimate it already carried");
        line.DoctorCommissionAmount.Should().Be(32_000m);
    }

    // ── Paid: never rewritten, corrected by a separate line ───────────────────

    [Fact]
    public async Task PaidCommission_IsNeverRewritten_AndGetsASignedAdjustmentInstead()
    {
        await using var db = CreateDb();
        // Paid at a 20,000 lab cost → 32,000 commission. The lab actually charged 35,000,
        // so the correct commission is 26,000 and the doctor was OVERPAID by 6,000.
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Paid);

        var result = await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        result.Recalculated.Should().Be(0);
        result.AdjustmentsRaised.Should().Be(1);
        result.NetAdjustmentAmount.Should().Be(-6_000m);

        var line = await db.InvoiceLineItems.FindAsync(f.LineItemId);
        line!.LabCost.Should().Be(20_000m, "the paid line is history and must not be rewritten");
        line.DoctorCommissionAmount.Should().Be(32_000m, "the doctor was handed money against this exact number");

        var adjustment = await db.DoctorCommissionAdjustments.SingleAsync();
        adjustment.DoctorId.Should().Be(f.DoctorId);
        adjustment.InvoiceLineItemId.Should().Be(f.LineItemId);
        adjustment.LabOrderId.Should().Be(f.LabOrderId);
        adjustment.AdjustmentAmount.Should().Be(-6_000m);
        adjustment.PaidCommissionAmount.Should().Be(32_000m);
        adjustment.RecalculatedCommissionAmount.Should().Be(26_000m);
        adjustment.PreviousLabCost.Should().Be(20_000m);
        adjustment.CurrentLabCost.Should().Be(35_000m);
        adjustment.Status.Should().Be(CommissionAdjustmentStatus.Pending);
        adjustment.Reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PaidCommission_WhenTheLabChargedLess_RaisesAPositiveAdjustment()
    {
        await using var db = CreateDb();
        // Paid at 20,000; the lab actually charged 12,000 → correct commission 35,200,
        // so the clinic still owes the doctor 3,200.
        var f = await SeedAsync(db, labOrderCost: 12_000m, commissionStatus: CommissionStatus.Paid);

        await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        var adjustment = await db.DoctorCommissionAdjustments.SingleAsync();
        adjustment.AdjustmentAmount.Should().Be(3_200m);
        adjustment.RecalculatedCommissionAmount.Should().Be(35_200m);
    }

    [Fact]
    public async Task CancelledLabOrder_AfterThePaymentWasMade_RefundsTheWholeDeduction()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderStatus: "cancelled", commissionStatus: CommissionStatus.Paid);

        await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        // Correct commission with no lab cost = 40,000; paid 32,000 → owed 8,000.
        var adjustment = await db.DoctorCommissionAdjustments.SingleAsync();
        adjustment.AdjustmentAmount.Should().Be(8_000m);
        adjustment.CurrentLabCost.Should().Be(0m);
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunningTheResyncTwice_DoesNotDuplicateTheAdjustment()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Paid);
        var service = BuildService(db);

        await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        var second = await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        second.AdjustmentsRaised.Should().Be(0, "the gap was already closed by the first run");
        (await db.DoctorCommissionAdjustments.CountAsync()).Should().Be(1);
        (await db.DoctorCommissionAdjustments.SumAsync(a => a.AdjustmentAmount)).Should().Be(-6_000m);
    }

    [Fact]
    public async Task SuccessiveCostChanges_AccumulateToTheCorrectTotal_NotToADoubleCount()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Paid);
        var service = BuildService(db);

        // First correction: 32,000 paid vs 26,000 correct → −6,000.
        await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        // The lab renegotiates down to 30,000 → correct commission 28,000, so the TOTAL
        // correction should now be −4,000, i.e. this run adds +2,000 on top of the −6,000.
        var order = await db.LabOrders.FindAsync(f.LabOrderId);
        order!.TotalCost = 30_000m;
        order.Cost = 30_000m;
        await db.SaveChangesAsync();

        var second = await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        second.AdjustmentsRaised.Should().Be(1);
        second.NetAdjustmentAmount.Should().Be(2_000m);

        var rows = await db.DoctorCommissionAdjustments.OrderBy(a => a.CreatedAt).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Sum(a => a.AdjustmentAmount).Should().Be(-4_000m,
            "the sum of corrections must close the gap exactly once");
        rows[1].PreviousLabCost.Should().Be(35_000m, "each correction starts where the last one left off");
        rows[1].CurrentLabCost.Should().Be(30_000m);
    }

    [Fact]
    public async Task NoChangeAtAll_ProducesNothing()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 20_000m, commissionStatus: CommissionStatus.Paid);

        var result = await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        result.Recalculated.Should().Be(0);
        result.AdjustmentsRaised.Should().Be(0);
        (await db.DoctorCommissionAdjustments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CancelledAdjustment_IsExcluded_SoTheGapReopens()
    {
        // A voided correction must stop counting — otherwise cancelling one would leave the
        // doctor's balance permanently wrong with no way to re-raise it.
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Paid);
        var service = BuildService(db);

        await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        var raised = await db.DoctorCommissionAdjustments.SingleAsync();
        await service.CancelAdjustmentAsync(raised.Id, "أُلغي بقرار إداري", Guid.NewGuid());

        var second = await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        second.AdjustmentsRaised.Should().Be(1);
        (await db.DoctorCommissionAdjustments
            .Where(a => a.Status != CommissionAdjustmentStatus.Cancelled)
            .SumAsync(a => a.AdjustmentAmount)).Should().Be(-6_000m);
    }

    // ── Currency ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForeignCurrencyLabOrder_IsConvertedToTheInvoiceCurrency_NotSummedRaw()
    {
        await using var db = CreateDb();
        // 100 SAR at 68 YER/SAR = 6,800 YER. Deducting the raw 100 would hand the doctor
        // commission on 99,900 instead of 93,200 — a 2,680 overpayment on one line.
        var f = await SeedAsync(db,
            labOrderCost: 100m, labCurrency: "SAR", labRate: 68m,
            invoiceCurrency: "YER", commissionStatus: CommissionStatus.Approved);

        await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        var line = await db.InvoiceLineItems.FindAsync(f.LineItemId);
        line!.LabCost.Should().Be(6_800m);
        line.DoctorCommissionAmount.Should().Be(37_280m); // (100,000 − 6,800) × 40%
    }

    [Fact]
    public async Task TwoForeignCurrenciesWithNoRateBetweenThem_AreSkipped_NotGuessed()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db,
            labOrderCost: 100m, labCurrency: "SAR", labRate: 68m,
            invoiceCurrency: "USD", commissionStatus: CommissionStatus.Approved);

        var result = await BuildService(db).ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        result.Recalculated.Should().Be(0);
        result.Skipped.Should().ContainSingle("an unresolvable currency pair belongs in the fix-up list");

        var line = await db.InvoiceLineItems.FindAsync(f.LineItemId);
        line!.LabCost.Should().Be(20_000m, "the line is left exactly as it was rather than corrupted");
    }

    // ── Settlement arithmetic ─────────────────────────────────────────────────

    [Fact]
    public async Task SettlementSummary_FoldsCorrectionsIntoWhatIsStillOwed()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Paid);

        db.DoctorCommissionPayments.Add(new DoctorCommissionPayment
        {
            DoctorId = f.DoctorId,
            Amount = 32_000m,
            PaymentDate = new DateOnly(2026, 7, 1),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var service = BuildService(db);
        await service.ResyncLabOrderAsync(f.LabOrderId, Guid.NewGuid());
        await db.SaveChangesAsync();

        var summary = await service.GetSettlementSummaryAsync(f.DoctorId);

        summary.Should().NotBeNull();
        summary!.EarnedFromLineItems.Should().Be(32_000m);
        summary.AdjustmentsTotal.Should().Be(-6_000m);
        summary.TotalDue.Should().Be(26_000m);
        summary.AlreadyPaid.Should().Be(32_000m);
        summary.Remaining.Should().Be(-6_000m, "the doctor was overpaid and owes 6,000 back");
        summary.PendingAdjustmentCount.Should().Be(1);
    }

    [Fact]
    public async Task BackfillSweep_TouchesOnlyLinesThatAlreadyCarryALabOrderLink()
    {
        await using var db = CreateDb();
        var f = await SeedAsync(db, labOrderCost: 35_000m, commissionStatus: CommissionStatus.Approved);

        // An unlinked line on the same invoice: ambiguous history the sweep must not guess at.
        var unlinked = new InvoiceLineItem
        {
            InvoiceId = f.InvoiceId,
            ServiceNameSnapshot = "حشوة",
            Description = "حشوة",
            Quantity = 1,
            UnitPrice = 50_000m,
            TotalPrice = 50_000m,
            DoctorId = f.DoctorId,
            LabOrderId = null,
            LabCost = 9_999m,
            DoctorCommissionPercentage = 40m,
            CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts,
            CommissionStatus = CommissionStatus.Approved,
            DoctorCommissionAmount = 16_000m,
            IsActive = true,
        };
        db.InvoiceLineItems.Add(unlinked);
        await db.SaveChangesAsync();

        var result = await BuildService(db).ResyncAllLinkedAsync(doctorId: null, performedBy: Guid.NewGuid());
        await db.SaveChangesAsync();

        result.LineItemsExamined.Should().Be(1, "only the linked line is in scope");
        result.Recalculated.Should().Be(1);

        var untouched = await db.InvoiceLineItems.FindAsync(unlinked.Id);
        untouched!.LabCost.Should().Be(9_999m);
        untouched.DoctorCommissionAmount.Should().Be(16_000m);
    }
}
