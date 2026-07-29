using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// LAB-FINANCE-ROOT-CAUSE-2: nothing in the codebase ever set
/// <see cref="InvoiceLineItem.LabOrderId"/> — the "pull actual lab cost" block in
/// <see cref="CommissionService.AutoFillFromServiceAsync"/> already existed and read
/// <c>LabOrder.TotalCost ?? LabOrder.Cost</c> correctly, but since LabOrderId was never
/// populated, every line item silently fell back to the service's static
/// <c>DefaultLabCost</c> instead of what the lab actually charged for THIS order
/// (e.g. a discounted, renegotiated, or remade cost).
///
/// These tests pin the fix: AutoFillFromServiceAsync now resolves LabOrderId from the
/// line item's linked visit when there is exactly one non-cancelled lab order on it,
/// and deliberately leaves it unresolved (falling back to DefaultLabCost) when the
/// visit has zero or more than one candidate — a single LabCost field cannot
/// unambiguously represent multiple lab orders.
/// </summary>
public class CommissionServiceLabOrderAutoLinkTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CommissionService CreateService(AppDbContext db) =>
        new(db, new JournalEntryService(db, NullLogger<JournalEntryService>.Instance),
            new TreasuryResolutionService(db, NullLogger<TreasuryResolutionService>.Instance),
            NullLogger<CommissionService>.Instance);

    private static (ClinicService service, Doctor doctor, Patient patient, Visit visit) SeedCommon(
        AppDbContext db, decimal defaultLabCost)
    {
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع" });

        var service = new ClinicService
        {
            ArabicName = "تركيبات زيركون",
            EnglishName = "Zirconia Crown",
            Code = "ZRC",
            Category = ServiceCategory.Prosthodontics,
            DefaultPrice = 60_000m,
            DefaultLabCost = defaultLabCost,
            DefaultDoctorCommissionPercentage = 30m,
            CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts,
            CommissionRecognitionMode = CommissionRecognitionMode.OnInvoiceApproval,
            IsActive = true,
        };
        db.ClinicServices.Add(service);

        var doctor = new Doctor { Name = "د. عقلان الكامل", IsActive = true, BranchId = branchId };
        db.Doctors.Add(doctor);

        var patient = new Patient { FirstName = "مريض", LastName = "اختبار", PatientNumber = "GM-0001", IsActive = true };
        db.Patients.Add(patient);

        var visit = new Visit
        {
            Patient = patient,
            DoctorId = doctor.Id,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow),
            IsActive = true,
        };
        db.Visits.Add(visit);

        return (service, doctor, patient, visit);
    }

    private static InvoiceLineItem SeedLineItem(
        AppDbContext db, ClinicService service, Doctor doctor, Visit visit, Patient patient)
    {
        // NOTE: Invoice.Patient must be set — CommissionService.LoadLineItemAsync does
        // .Include(i => i.Invoice).ThenInclude(inv => inv.Patient), and EF Core's InMemory
        // provider drops the whole query result (not just the Patient nav) when a required
        // relationship's FK doesn't resolve to any row. Real Postgres would just LEFT JOIN
        // to null; only InMemory behaves this way, so it must be avoided in these fixtures.
        var invoice = new Invoice { InvoiceNumber = "INV-TEST-001", Status = InvoiceStatus.Draft, IsActive = true, Patient = patient };
        db.Invoices.Add(invoice);

        var lineItem = new InvoiceLineItem
        {
            Invoice = invoice,
            ServiceId = service.Id,
            RelatedVisitId = visit.Id,
            Description = service.ArabicName,
            Quantity = 1,
            UnitPrice = service.DefaultPrice,
            TotalPrice = service.DefaultPrice,
            DoctorId = doctor.Id,
            IsActive = true,
        };
        db.InvoiceLineItems.Add(lineItem);
        return lineItem;
    }

    [Fact]
    public async Task SingleActiveLabOrderOnVisit_LinksItAndUsesItsRealCost_NotTheServiceDefault()
    {
        await using var db = CreateDb();
        var (service, doctor, patient, visit) = SeedCommon(db, defaultLabCost: 15_000m);
        var lineItem = SeedLineItem(db, service, doctor, visit, patient);

        var labOrder = new LabOrder
        {
            Patient = patient,
            VisitId = visit.Id,
            Status = "sent",
            Cost = 12_000m,
            TotalCost = 12_000m,
            IsActive = true,
        };
        db.LabOrders.Add(labOrder);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.AutoFillFromServiceAsync(lineItem.Id);

        var reloaded = await db.InvoiceLineItems.AsNoTracking().FirstAsync(i => i.Id == lineItem.Id);
        reloaded.LabOrderId.Should().Be(labOrder.Id, "the single non-cancelled lab order on the visit should be auto-linked");
        reloaded.LabCost.Should().Be(12_000m, "the linked lab order's real TotalCost must win over the service's DefaultLabCost");
    }

    [Fact]
    public async Task NoLabOrderOnVisit_LeavesLabOrderIdUnset_AndFallsBackToServiceDefault()
    {
        await using var db = CreateDb();
        var (service, doctor, patient, visit) = SeedCommon(db, defaultLabCost: 15_000m);
        var lineItem = SeedLineItem(db, service, doctor, visit, patient);
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.AutoFillFromServiceAsync(lineItem.Id);

        var reloaded = await db.InvoiceLineItems.AsNoTracking().FirstAsync(i => i.Id == lineItem.Id);
        reloaded.LabOrderId.Should().BeNull();
        reloaded.LabCost.Should().Be(15_000m, "with no lab order to pull a real cost from, the service default is the correct fallback");
    }

    [Fact]
    public async Task MultipleActiveLabOrdersOnVisit_IsAmbiguous_LeavesLabOrderIdUnset()
    {
        await using var db = CreateDb();
        var (service, doctor, patient, visit) = SeedCommon(db, defaultLabCost: 15_000m);
        var lineItem = SeedLineItem(db, service, doctor, visit, patient);

        db.LabOrders.Add(new LabOrder { Patient = patient, VisitId = visit.Id, Status = "sent", Cost = 12_000m, TotalCost = 12_000m, IsActive = true });
        db.LabOrders.Add(new LabOrder { Patient = patient, VisitId = visit.Id, Status = "sent", Cost = 8_000m, TotalCost = 8_000m, IsActive = true });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.AutoFillFromServiceAsync(lineItem.Id);

        var reloaded = await db.InvoiceLineItems.AsNoTracking().FirstAsync(i => i.Id == lineItem.Id);
        reloaded.LabOrderId.Should().BeNull("a single LabCost field cannot unambiguously represent two lab orders");
        reloaded.LabCost.Should().Be(15_000m, "falls back to the service default when the real cost is ambiguous");
    }

    [Fact]
    public async Task CancelledLabOrderOnVisit_IsIgnored_FallsBackToServiceDefault()
    {
        await using var db = CreateDb();
        var (service, doctor, patient, visit) = SeedCommon(db, defaultLabCost: 15_000m);
        var lineItem = SeedLineItem(db, service, doctor, visit, patient);

        db.LabOrders.Add(new LabOrder { Patient = patient, VisitId = visit.Id, Status = "cancelled", Cost = 12_000m, TotalCost = 12_000m, IsActive = true });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.AutoFillFromServiceAsync(lineItem.Id);

        var reloaded = await db.InvoiceLineItems.AsNoTracking().FirstAsync(i => i.Id == lineItem.Id);
        reloaded.LabOrderId.Should().BeNull("a cancelled lab order carries no financial commitment and must not be linked");
        reloaded.LabCost.Should().Be(15_000m);
    }

    [Fact]
    public async Task AlreadyLinkedLabOrderId_IsNeverOverwritten()
    {
        await using var db = CreateDb();
        var (service, doctor, patient, visit) = SeedCommon(db, defaultLabCost: 15_000m);
        var lineItem = SeedLineItem(db, service, doctor, visit, patient);

        var firstOrder = new LabOrder { Patient = patient, VisitId = visit.Id, Status = "sent", Cost = 12_000m, TotalCost = 12_000m, IsActive = true };
        var secondOrder = new LabOrder { Patient = patient, VisitId = visit.Id, Status = "sent", Cost = 9_000m, TotalCost = 9_000m, IsActive = true };
        db.LabOrders.Add(firstOrder);
        db.LabOrders.Add(secondOrder);
        await db.SaveChangesAsync();

        lineItem.LabOrderId = firstOrder.Id;
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        await svc.AutoFillFromServiceAsync(lineItem.Id);

        var reloaded = await db.InvoiceLineItems.AsNoTracking().FirstAsync(i => i.Id == lineItem.Id);
        reloaded.LabOrderId.Should().Be(firstOrder.Id, "an explicit existing link must not be replaced by the auto-resolver, even when other candidates exist");
        reloaded.LabCost.Should().Be(12_000m);
    }
}
