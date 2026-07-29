using AqlanDentalPro.Application.DTOs.Journey;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Journey;

/// <summary>
/// Daily Operations audit (2026-07-29): a Draft invoice is a work-in-progress document with
/// no financial commitment yet (mirrors the Lab module's "sent = commitment, draft = no
/// commitment" rule). ValidateFinancialClosureAsync previously summed ALL non-cancelled
/// invoices — including Draft — into the patient's outstanding balance, so an in-progress
/// draft invoice could block checkout with a phantom debt nobody has actually been billed for
/// yet, or (if it happened to cover the same visit as a real unbilled amount) silently hide the
/// real debt behind the draft's VisitId link. These tests pin the fixed behavior.
/// </summary>
public class CheckoutServiceFinancialClosureDraftInvoiceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CheckoutService BuildService(AppDbContext db, bool isReception = true)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        currentUser.SetupGet(x => x.Role).Returns(UserRole.Admin);

        var access = new Mock<IPatientAccessService>();
        access.SetupGet(x => x.IsDoctor).Returns(false);
        access.SetupGet(x => x.HasFullAccess).Returns(true);
        access.SetupGet(x => x.IsReception).Returns(isReception);

        return new CheckoutService(
            db,
            new Mock<ICommissionService>().Object,
            currentUser.Object,
            access.Object,
            new Mock<IRealTimePushService>().Object,
            NullLogger<CheckoutService>.Instance);
    }

    [Fact]
    public async Task ValidateFinancialClosure_DraftInvoiceOnly_WithNoRealDebt_DoesNotBlockClosure()
    {
        await using var db = CreateDb();

        var patient = new Patient { PatientNumber = "GM-CLOSURE-001", FirstName = "مريض", LastName = "اختبار" };
        var visit = new Visit
        {
            PatientId = patient.Id,
            VisitDate = ClinicTimeProvider.ClinicToday(),
            CheckoutStatus = "ReadyForCheckout",
            AmountDueReference = 0, // fully accounted for elsewhere — no real debt
            IsActive = true
        };
        // An in-progress Draft invoice (e.g. reception started drafting a 5000 line item but
        // has not issued it yet). Before the fix, this alone would have produced an
        // "outstanding balance" of 5000 and blocked checkout.
        var draftInvoice = new Invoice
        {
            PatientId = patient.Id,
            VisitId = visit.Id,
            InvoiceNumber = "DRAFT-TEST-001",
            Status = InvoiceStatus.Draft,
            Subtotal = 5000,
            TotalAmount = 5000,
            IsActive = true
        };

        db.AddRange(patient, visit, draftInvoice);
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.ValidateFinancialClosureAsync(patient.Id, new ValidateFinancialClosureRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var canClose = (bool)ok.Value!.GetType().GetProperty("canClose")!.GetValue(ok.Value)!;
        canClose.Should().BeTrue("a Draft invoice has no financial commitment yet and must not block checkout on its own");
    }

    [Fact]
    public async Task ValidateFinancialClosure_DraftInvoiceHidingRealUnbilledDebt_StillBlocksClosure()
    {
        await using var db = CreateDb();

        var patient = new Patient { PatientNumber = "GM-CLOSURE-002", FirstName = "مريض", LastName = "اختبار" };
        var visit = new Visit
        {
            PatientId = patient.Id,
            VisitDate = ClinicTimeProvider.ClinicToday(),
            CheckoutStatus = "ReadyForCheckout",
            AmountDueReference = 12000, // real work performed, genuinely owed
            IsActive = true
        };
        // A Draft invoice already links to this visit (reception started billing it) but has
        // NOT been issued — so it must not "cover" the visit and hide the real 12000 debt.
        var draftInvoice = new Invoice
        {
            PatientId = patient.Id,
            VisitId = visit.Id,
            InvoiceNumber = "DRAFT-TEST-002",
            Status = InvoiceStatus.Draft,
            Subtotal = 12000,
            TotalAmount = 12000,
            IsActive = true
        };

        db.AddRange(patient, visit, draftInvoice);
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.ValidateFinancialClosureAsync(patient.Id, new ValidateFinancialClosureRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var canClose = (bool)ok.Value!.GetType().GetProperty("canClose")!.GetValue(ok.Value)!;
        canClose.Should().BeFalse(
            "the visit still has a real 12000 unbilled amount — a Draft invoice must not suppress it via billedVisitIdsSet");
    }

    [Fact]
    public async Task ValidateFinancialClosure_IssuedInvoiceUnpaid_StillBlocksClosure()
    {
        await using var db = CreateDb();

        var patient = new Patient { PatientNumber = "GM-CLOSURE-003", FirstName = "مريض", LastName = "اختبار" };
        var visit = new Visit
        {
            PatientId = patient.Id,
            VisitDate = ClinicTimeProvider.ClinicToday(),
            CheckoutStatus = "ReadyForCheckout",
            AmountDueReference = 0,
            IsActive = true
        };
        var issuedInvoice = new Invoice
        {
            PatientId = patient.Id,
            VisitId = visit.Id,
            InvoiceNumber = "ISSUED-TEST-003",
            Status = InvoiceStatus.Issued,
            Subtotal = 8000,
            TotalAmount = 8000,
            IsActive = true
        };

        db.AddRange(patient, visit, issuedInvoice);
        await db.SaveChangesAsync();

        var service = BuildService(db);
        var result = await service.ValidateFinancialClosureAsync(patient.Id, new ValidateFinancialClosureRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var canClose = (bool)ok.Value!.GetType().GetProperty("canClose")!.GetValue(ok.Value)!;
        canClose.Should().BeFalse("a genuinely Issued, unpaid invoice must still block closure — the fix only excludes Draft");
    }
}
