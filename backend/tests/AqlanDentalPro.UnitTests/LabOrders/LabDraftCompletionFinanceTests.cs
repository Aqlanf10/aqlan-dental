using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

// CS0118: this file lives in AqlanDentalPro.UnitTests.LabOrders, and name lookup walks
// up to AqlanDentalPro.UnitTests.Lab — a NAMESPACE (the Lab/ test folder) — before it
// ever reaches Domain.Entities.Lab. Alias the entity so the two can never collide.
using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// CORE-LAB-001/002: an incomplete lab-order draft used to be a dead end.
///
/// The create modal happily saves a draft with no lab and no cost. The backend only
/// ever built the SupplierBill + LabPayable + journal entry INSIDE Create, gated on
/// "has a lab AND cost &gt; 0", and Update had no equivalent — so attaching the lab or
/// the cost afterwards left the order financially invisible for good: no supplier
/// credit, no expense in the books, and no lab cost to deduct before the doctor's
/// earned commission. Nothing stopped that same empty draft being pushed to "sent"
/// either, so it could sit in the lab queue forever, unbilled.
///
/// These tests pin both halves of the fix: the send gate, and the idempotent
/// create-or-update of the financial trail.
/// </summary>
public class LabDraftCompletionFinanceTests
{
    private static async Task<(AppDbContext db, LabOrdersController controller, Patient patient, LabEntity lab)>
        SetupAsync(bool seedLabSupplier = false)
    {
        var db = LabOrdersTestData.CreateDb();

        var patient = LabOrdersTestData.BuildPatient();
        db.Patients.Add(patient);

        var lab = new LabEntity { Name = "معمل الأمل", IsActive = true };
        if (seedLabSupplier)
        {
            var supplier = new Supplier { Name = lab.Name, Type = SupplierType.DentalLab, IsActive = true };
            db.Suppliers.Add(supplier);
            lab.SupplierId = supplier.Id;
        }
        db.Labs.Add(lab);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.BranchId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);

        var access = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupNonDoctor(access);

        var controller = LabOrdersTestData.BuildController(db, access, currentUser);
        return (db, controller, patient, lab);
    }

    private static async Task<LabOrder> SeedDraftAsync(AppDbContext db, Guid patientId, Guid? labId = null, decimal? cost = null)
    {
        var order = LabOrdersTestData.BuildLabOrder(patientId);
        order.LabId = labId;
        order.Cost = cost;
        order.TotalCost = cost;
        order.Currency = "YER";
        order.ExchangeRateToYer = 1m;
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static string Message(IActionResult result)
    {
        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var prop = bad.Value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("every 4xx must carry an Arabic 'message' field");
        return (string)prop!.GetValue(bad.Value)!;
    }

    // ── The send gate (CORE-LAB-002) ──────────────────────────────────────────

    [Fact]
    public async Task Draft_WithoutLab_CannotBeSent()
    {
        var (db, controller, patient, _) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id, labId: null, cost: 5000m);

        var result = await controller.UpdateStatus(order.Id, new UpdateLabOrderStatusRequest { Status = "sent" });

        Message(result).Should().Contain("المعمل");
        (await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status.Should().Be("draft");
    }

    [Fact]
    public async Task Draft_WithoutCost_CannotBeSent()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id, labId: lab.Id, cost: null);

        var result = await controller.UpdateStatus(order.Id, new UpdateLabOrderStatusRequest { Status = "sent" });

        Message(result).Should().Contain("تكلفة");
        (await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status.Should().Be("draft");
    }

    [Fact]
    public async Task Draft_WithLabAndCost_CanBeSent_AndBuildsTheFinancialTrail()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id, labId: lab.Id, cost: 7500m);

        var result = await controller.UpdateStatus(order.Id, new UpdateLabOrderStatusRequest { Status = "sent" });

        result.Should().BeOfType<OkObjectResult>();
        (await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status.Should().Be("sent");
        (await db.SupplierBills.CountAsync(b => b.LabOrderId == order.Id)).Should().Be(1);
        (await db.LabPayables.CountAsync(p => p.LabOrderId == order.Id)).Should().Be(1);
    }

    // ── Completing the draft through Update (CORE-LAB-001) ────────────────────

    [Fact]
    public async Task CompletingADraft_CreatesSupplierBillAndPayable_Once()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id);

        // The order was created with neither lab nor cost — exactly the production case.
        (await db.SupplierBills.CountAsync(b => b.LabOrderId == order.Id)).Should().Be(0);

        var result = await controller.Update(order.Id, new UpdateLabOrderRequest
        {
            LabId = lab.Id,
            Cost = 12000m,
            Currency = "YER",
        });

        result.Should().BeOfType<OkObjectResult>();

        var bill = await db.SupplierBills.AsNoTracking().SingleAsync(b => b.LabOrderId == order.Id);
        bill.TotalAmount.Should().Be(12000m);
        bill.Currency.Should().Be("YER");
        bill.Status.Should().Be(BillStatus.Unpaid);

        var payable = await db.LabPayables.AsNoTracking().SingleAsync(p => p.LabOrderId == order.Id);
        payable.Amount.Should().Be(12000m);
        payable.LabId.Should().Be(lab.Id);
        payable.SupplierBillId.Should().Be(bill.Id);
    }

    [Fact]
    public async Task RepeatedUpdates_DoNotDuplicateTheBillOrPayable()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id);

        for (var i = 0; i < 3; i++)
        {
            var res = await controller.Update(order.Id, new UpdateLabOrderRequest
            {
                LabId = lab.Id,
                Cost = 9000m,
                Currency = "YER",
            });
            res.Should().BeOfType<OkObjectResult>();
        }

        (await db.SupplierBills.CountAsync(b => b.LabOrderId == order.Id)).Should().Be(1);
        (await db.LabPayables.CountAsync(p => p.LabOrderId == order.Id)).Should().Be(1);
    }

    [Fact]
    public async Task ChangingTheCost_UpdatesTheExistingBill_InsteadOfAddingAnother()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id);

        await controller.Update(order.Id, new UpdateLabOrderRequest { LabId = lab.Id, Cost = 5000m, Currency = "YER" });
        await controller.Update(order.Id, new UpdateLabOrderRequest { Cost = 8000m, Currency = "YER" });

        var bill = await db.SupplierBills.AsNoTracking().SingleAsync(b => b.LabOrderId == order.Id);
        bill.TotalAmount.Should().Be(8000m);

        var payable = await db.LabPayables.AsNoTracking().SingleAsync(p => p.LabOrderId == order.Id);
        payable.Amount.Should().Be(8000m);
    }

    [Fact]
    public async Task SupplierBalance_TracksTheNetChange_NotTheSumOfEveryEdit()
    {
        var (db, controller, patient, lab) = await SetupAsync(seedLabSupplier: true);
        var order = await SeedDraftAsync(db, patient.Id);

        await controller.Update(order.Id, new UpdateLabOrderRequest { LabId = lab.Id, Cost = 5000m, Currency = "YER" });
        await controller.Update(order.Id, new UpdateLabOrderRequest { Cost = 8000m, Currency = "YER" });

        // Naively adding on every save would leave 13000 here.
        var supplierId = (await db.SupplierBills.AsNoTracking().SingleAsync(b => b.LabOrderId == order.Id)).SupplierId;
        (await db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == supplierId)).Balance.Should().Be(8000m);
    }

    // ── Currency (CORE-LAB-001) ───────────────────────────────────────────────

    [Fact]
    public async Task ForeignCurrency_WithoutRate_IsRejected_WithArabicMessage()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id);

        var result = await controller.Update(order.Id, new UpdateLabOrderRequest
        {
            LabId = lab.Id,
            Cost = 300m,
            Currency = "SAR",
        });

        Message(result).Should().Contain("سعر الصرف");
        (await db.SupplierBills.CountAsync(b => b.LabOrderId == order.Id)).Should().Be(0);
    }

    [Fact]
    public async Task ForeignCurrency_WithRate_IsStoredOnTheBill_WithoutMixingCurrencies()
    {
        var (db, controller, patient, lab) = await SetupAsync(seedLabSupplier: true);
        var order = await SeedDraftAsync(db, patient.Id);

        var result = await controller.Update(order.Id, new UpdateLabOrderRequest
        {
            LabId = lab.Id,
            Cost = 300m,
            Currency = "SAR",
            ExchangeRateToYer = 66m,
        });

        result.Should().BeOfType<OkObjectResult>();

        var bill = await db.SupplierBills.AsNoTracking().SingleAsync(b => b.LabOrderId == order.Id);
        bill.Currency.Should().Be("SAR");
        bill.TotalAmount.Should().Be(300m, "the bill keeps its own currency — amounts are never converted into a mixed total");
        bill.ExchangeRateToYer.Should().Be(66m);

        // Supplier.Balance is a single YER column, so a SAR bill must not move it.
        (await db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == bill.SupplierId)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task UnsupportedCurrency_IsRejected()
    {
        var (db, controller, patient, lab) = await SetupAsync();
        var order = await SeedDraftAsync(db, patient.Id);

        var result = await controller.Update(order.Id, new UpdateLabOrderRequest
        {
            LabId = lab.Id,
            Cost = 100m,
            Currency = "EUR",
            ExchangeRateToYer = 300m,
        });

        Message(result).Should().Contain("العملة");
    }

    // ── Money already moved (CORE-LAB-001) ────────────────────────────────────

    [Fact]
    public async Task ChangingCost_AfterAPaymentExists_IsRefused_NotSilentlyRewritten()
    {
        var (db, controller, patient, lab) = await SetupAsync(seedLabSupplier: true);
        var order = await SeedDraftAsync(db, patient.Id);

        await controller.Update(order.Id, new UpdateLabOrderRequest { LabId = lab.Id, Cost = 5000m, Currency = "YER" });

        // Simulate a partial payment recorded against the payable.
        var payable = await db.LabPayables.FirstAsync(p => p.LabOrderId == order.Id);
        payable.PaidAmount = 2000m;
        payable.Status = "partial";
        await db.SaveChangesAsync();

        var result = await controller.Update(order.Id, new UpdateLabOrderRequest { Cost = 9000m, Currency = "YER" });

        Message(result).Should().Contain("دفعات");

        // The bill must be untouched — rewriting it would desynchronise the ledger.
        (await db.SupplierBills.AsNoTracking().SingleAsync(b => b.LabOrderId == order.Id))
            .TotalAmount.Should().Be(5000m);
    }

    // ── CORE-LAB-004: never persist an unusable branch ────────────────────────

    [Fact]
    public async Task NoResolvableBranch_IsRefused_RatherThanWritingAnOrphanBill()
    {
        var db = LabOrdersTestData.CreateDb();
        var patient = LabOrdersTestData.BuildPatient();
        db.Patients.Add(patient);
        var lab = new LabEntity { Name = "معمل الأمل", IsActive = true };
        db.Labs.Add(lab);
        await db.SaveChangesAsync();

        // Neither the order nor the current user resolves a branch.
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.BranchId).Returns((Guid?)null);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        var access = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupNonDoctor(access);
        var controller = LabOrdersTestData.BuildController(db, access, currentUser);

        var order = LabOrdersTestData.BuildLabOrder(patient.Id);
        order.BranchId = null;
        order.Currency = "YER";
        order.ExchangeRateToYer = 1m;
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();

        var result = await controller.Update(order.Id, new UpdateLabOrderRequest
        {
            LabId = lab.Id,
            Cost = 4000m,
            Currency = "YER",
        });

        Message(result).Should().Contain("فرع");

        // SupplierBill.BranchId is non-nullable — a Guid.Empty row would belong to no
        // branch and vanish from every branch-scoped finance report.
        (await db.SupplierBills.CountAsync(b => b.LabOrderId == order.Id)).Should().Be(0);
        (await db.LabPayables.CountAsync(p => p.LabOrderId == order.Id)).Should().Be(0);
    }
}
