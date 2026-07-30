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
/// CORE-XMOD-002 — the Create path used to carry its own ~90-line copy of the supplier /
/// bill-number / payable / journal linkage, separate from LabOrderFinanceSyncService which
/// Update and the send transition already used. Two copies of a money path drift, and this one
/// had: the update path grew a branch guard, an advisory lock and a currency check that Create
/// never received.
/// <para>
/// Nothing covered the Create-to-bill path before, so the consolidation could have been a
/// silent no-op and every existing test would still have passed. These tests exist to make
/// that impossible.
/// </para>
/// </summary>
public class LabOrderCreateBillingTests
{
    private static async Task<(AppDbContext db, Guid patientId, Guid labId, Guid supplierId)> SeedAsync()
    {
        var db = LabOrdersTestData.CreateDb();

        var patient = LabOrdersTestData.BuildPatient();
        db.Patients.Add(patient);

        var supplier = new Supplier { Name = "معمل الأمل", Type = SupplierType.DentalLab, IsActive = true };
        db.Suppliers.Add(supplier);

        var lab = new LabEntity { Name = "معمل الأمل", SupplierId = supplier.Id, IsActive = true };
        db.Labs.Add(lab);

        await db.SaveChangesAsync();
        return (db, patient.Id, lab.Id, supplier.Id);
    }

    private static LabOrdersController BuildController(AppDbContext db, Guid branchId)
    {
        var patientAccess = new Mock<IPatientAccessService>();
        LabOrdersTestData.SetupNonDoctor(patientAccess);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUser.SetupGet(c => c.BranchId).Returns(branchId);

        return LabOrdersTestData.BuildController(db, patientAccess, currentUser);
    }

    private static CreateLabOrderRequest Request(
        Guid patientId, Guid labId, decimal cost, string status,
        string currency = "YER", decimal? rate = null)
        => new()
        {
            PatientId = patientId,
            LabId = labId,
            ApplianceType = "تاج زيركون",
            Priority = "normal",
            Cost = cost,
            Currency = currency,
            ExchangeRateToYer = rate,
            // The controller derives "sent" from an explicit SentDate; without one it stays draft.
            SentDate = status == "sent" ? "2026-07-01" : null,
        };

    [Fact]
    public async Task CreatingASentOrder_StillProducesExactlyOneBillAndOnePayable()
    {
        var (db, patientId, labId, supplierId) = await SeedAsync();
        await using var _ = db;
        var branchId = Guid.NewGuid();

        var result = await BuildController(db, branchId)
            .Create(Request(patientId, labId, 60_000m, "sent"));

        result.Should().BeAssignableTo<ObjectResult>();

        var bill = await db.SupplierBills.SingleAsync();
        bill.SupplierId.Should().Be(supplierId);
        bill.TotalAmount.Should().Be(60_000m);
        bill.Currency.Should().Be("YER");
        bill.Status.Should().Be(BillStatus.Unpaid);
        bill.BillNumber.Should().StartWith("BILL-");

        var payable = await db.LabPayables.SingleAsync();
        payable.SupplierBillId.Should().Be(bill.Id);
        payable.Amount.Should().Be(60_000m);
        payable.PaidAmount.Should().Be(0m);
        payable.Status.Should().Be("pending");

        // The bill must point back at the order it came from — that link is what makes the
        // whole sync idempotent.
        var order = await db.LabOrders.SingleAsync();
        bill.LabOrderId.Should().Be(order.Id);
    }

    [Fact]
    public async Task CreatingADraft_BuildsNoFinancialTrailAtAll()
    {
        // A draft is not a commitment. This is the boundary CORE-LAB-008 drew and the
        // consolidation had to preserve it, not just move the code.
        var (db, patientId, labId, _) = await SeedAsync();
        await using var _d = db;

        await BuildController(db, Guid.NewGuid())
            .Create(Request(patientId, labId, 60_000m, "draft"));

        (await db.LabOrders.CountAsync()).Should().Be(1);
        (await db.SupplierBills.CountAsync()).Should().Be(0);
        (await db.LabPayables.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AForeignCurrencyOrder_KeepsItsOwnCurrencyAndRateOnTheBill()
    {
        var (db, patientId, labId, _) = await SeedAsync();
        await using var _d = db;

        await BuildController(db, Guid.NewGuid())
            .Create(Request(patientId, labId, 1_000m, "sent", currency: "SAR", rate: 68m));

        var bill = await db.SupplierBills.SingleAsync();
        bill.Currency.Should().Be("SAR");
        bill.TotalAmount.Should().Be(1_000m, "the bill is denominated in the lab's own currency");
        bill.ExchangeRateToYer.Should().Be(68m);
    }

    [Fact]
    public async Task AForeignCurrencyBill_DoesNotMoveTheLegacyYerScalar()
    {
        // CORE-XMOD-001: Supplier.Balance holds Yemeni riyals. Adding a SAR figure to it
        // produces a number that is not money in any currency.
        var (db, patientId, labId, supplierId) = await SeedAsync();
        await using var _d = db;

        await BuildController(db, Guid.NewGuid())
            .Create(Request(patientId, labId, 1_000m, "sent", currency: "SAR", rate: 68m));

        (await db.Suppliers.FirstAsync(s => s.Id == supplierId)).Balance.Should().Be(0m);
    }

    [Fact]
    public async Task AYerBill_DoesMoveTheLegacyScalar()
    {
        // The mirror of the test above — proving the guard is a currency check and not a
        // blanket "never touch the balance", which would make the previous test vacuous.
        var (db, patientId, labId, supplierId) = await SeedAsync();
        await using var _d = db;

        await BuildController(db, Guid.NewGuid())
            .Create(Request(patientId, labId, 45_000m, "sent"));

        (await db.Suppliers.FirstAsync(s => s.Id == supplierId)).Balance.Should().Be(45_000m);
    }

    [Fact]
    public async Task ASentOrderWithNoLab_IsRefusedRatherThanBilledToNobody()
    {
        var (db, patientId, _, _) = await SeedAsync();
        await using var _d = db;

        var req = new CreateLabOrderRequest
        {
            PatientId = patientId,
            ApplianceType = "تاج زيركون",
            Priority = "normal",
            Cost = 10_000m,
            SentDate = "2026-07-01",
        };

        await BuildController(db, Guid.NewGuid()).Create(req);

        (await db.SupplierBills.CountAsync()).Should().Be(0);
        (await db.LabPayables.CountAsync()).Should().Be(0);
    }
}
