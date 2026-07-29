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

using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// CORE-LAB-007: a paid remake recorded RemakeCost on the order but never folded it into
/// TotalCost/Cost — the field LabOrderFinanceSyncService.SyncAsync reads to bill the
/// supplier and CommissionCalculator reads to deduct lab cost before the doctor's
/// commission. The extra cost was captured on the entity and then went nowhere: no
/// additional supplier debt, no extra expense, no larger commission deduction.
///
/// These tests pin: a paid remake bills the extra cost once it is re-sent, a free remake
/// changes nothing financially, a second remake call is rejected (idempotent), and every
/// remake writes an Audit Log entry.
/// </summary>
public class RemakeFinanceTests
{
    private static async Task<(AppDbContext db, LabOrdersController controller, Patient patient, LabEntity lab, Mock<IAuditService> audit)>
        SetupAsync()
    {
        var db = LabOrdersTestData.CreateDb();

        var patient = LabOrdersTestData.BuildPatient();
        db.Patients.Add(patient);

        var lab = new LabEntity { Name = "معمل الأمل", IsActive = true };
        db.Labs.Add(lab);
        await db.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.BranchId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);

        var access = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupNonDoctor(access);

        var audit = new Mock<IAuditService>();

        var controller = LabOrdersTestData.BuildController(db, access, currentUser, audit);
        return (db, controller, patient, lab, audit);
    }

    private static async Task<LabOrder> SeedReturnedOrderAsync(
        AppDbContext db, Guid patientId, Guid labId, decimal cost)
    {
        var order = LabOrdersTestData.BuildLabOrder(patientId, status: "returned");
        order.LabId = labId;
        order.Cost = cost;
        order.TotalCost = cost;
        order.Currency = "YER";
        order.ExchangeRateToYer = 1m;
        db.LabOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task PaidRemake_AddsCostToTotal_AndBillsItOnceResent()
    {
        var (db, controller, patient, lab, _) = await SetupAsync();
        var order = await SeedReturnedOrderAsync(db, patient.Id, lab.Id, cost: 10000m);

        var result = await controller.Remake(order.Id, new LabOrdersController.RemakeLabOrderRequest
        {
            Reason = "لون غير مطابق",
            IsFreeRemake = false,
            RemakeCost = 3000m,
        });

        result.Should().BeOfType<OkObjectResult>();
        var reloaded = await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        reloaded.Status.Should().Be("remake");
        reloaded.TotalCost.Should().Be(13000m, "the original 10,000 plus the 3,000 paid-remake surcharge");
        reloaded.Cost.Should().Be(13000m);

        // Resending must bill the NEW total, not the original cost — this is the whole point
        // of the fix: the extra cost has to actually reach the supplier bill/ledger.
        await controller.UpdateStatus(order.Id, new UpdateLabOrderStatusRequest { Status = "sent" });
        var bill = await db.SupplierBills.AsNoTracking().SingleAsync(b => b.LabOrderId == order.Id);
        bill.TotalAmount.Should().Be(13000m);
    }

    [Fact]
    public async Task FreeRemake_ChangesNoMoney()
    {
        var (db, controller, patient, lab, _) = await SetupAsync();
        var order = await SeedReturnedOrderAsync(db, patient.Id, lab.Id, cost: 10000m);

        var result = await controller.Remake(order.Id, new LabOrdersController.RemakeLabOrderRequest
        {
            Reason = "خطأ المعمل",
            IsFreeRemake = true,
        });

        result.Should().BeOfType<OkObjectResult>();
        var reloaded = await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        reloaded.TotalCost.Should().Be(10000m, "a free remake must not add any cost");
        reloaded.Cost.Should().Be(10000m);
        reloaded.RemakeCost.Should().BeNull();
        reloaded.IsFreeRemake.Should().BeTrue();
    }

    [Fact]
    public async Task PaidRemake_WithoutAPositiveCost_IsRejected()
    {
        var (db, controller, patient, lab, _) = await SetupAsync();
        var order = await SeedReturnedOrderAsync(db, patient.Id, lab.Id, cost: 10000m);

        var result = await controller.Remake(order.Id, new LabOrdersController.RemakeLabOrderRequest
        {
            Reason = "تكلفة إضافية بدون رقم",
            IsFreeRemake = false,
            RemakeCost = null,
        });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id)).Status.Should().Be("returned");
    }

    [Fact]
    public async Task DoubleSubmittingRemake_IsRejectedTheSecondTime_AndCostIsNotAppliedTwice()
    {
        var (db, controller, patient, lab, _) = await SetupAsync();
        var order = await SeedReturnedOrderAsync(db, patient.Id, lab.Id, cost: 10000m);

        var req = new LabOrdersController.RemakeLabOrderRequest
        {
            Reason = "لون غير مطابق",
            IsFreeRemake = false,
            RemakeCost = 3000m,
        };

        var first = await controller.Remake(order.Id, req);
        first.Should().BeOfType<OkObjectResult>();

        // A second click/retry with the same payload — the order is now "remake", not
        // "returned", so this must be rejected rather than doubling the surcharge.
        var second = await controller.Remake(order.Id, req);
        second.Should().BeOfType<BadRequestObjectResult>();

        var reloaded = await db.LabOrders.AsNoTracking().FirstAsync(o => o.Id == order.Id);
        reloaded.TotalCost.Should().Be(13000m, "the surcharge must be applied exactly once");
        reloaded.RemakeCount.Should().Be(1);
    }

    [Fact]
    public async Task Remake_WritesAnAuditLogEntry()
    {
        var (db, controller, patient, lab, audit) = await SetupAsync();
        var order = await SeedReturnedOrderAsync(db, patient.Id, lab.Id, cost: 10000m);

        await controller.Remake(order.Id, new LabOrdersController.RemakeLabOrderRequest
        {
            Reason = "لون غير مطابق",
            IsFreeRemake = false,
            RemakeCost = 3000m,
        });

        audit.Verify(a => a.LogAsync(
            AuditAction.Update,
            "LabOrder",
            order.Id,
            It.IsAny<object>(),
            It.IsAny<object>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task EditingCostOnASentOrder_WritesAnAuditLogEntry_ButEditingADraftDoesNot()
    {
        var (db, controller, patient, lab, audit) = await SetupAsync();

        var sentOrder = LabOrdersTestData.BuildLabOrder(patient.Id, status: "sent");
        sentOrder.LabId = lab.Id;
        sentOrder.Cost = 5000m;
        sentOrder.TotalCost = 5000m;
        sentOrder.Currency = "YER";
        sentOrder.ExchangeRateToYer = 1m;
        db.LabOrders.Add(sentOrder);

        var draftOrder = LabOrdersTestData.BuildLabOrder(patient.Id, status: "draft");
        db.LabOrders.Add(draftOrder);
        await db.SaveChangesAsync();

        await controller.Update(sentOrder.Id, new UpdateLabOrderRequest { Cost = 6000m, Currency = "YER" });
        await controller.Update(draftOrder.Id, new UpdateLabOrderRequest { LabId = lab.Id, Cost = 4000m, Currency = "YER" });

        audit.Verify(a => a.LogAsync(
            AuditAction.Update, "LabOrder", sentOrder.Id,
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>()), Times.Once);
        audit.Verify(a => a.LogAsync(
            AuditAction.Update, "LabOrder", draftOrder.Id,
            It.IsAny<object>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never);
    }
}
