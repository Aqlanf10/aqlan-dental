using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// CORE-FIN-LAB: <c>InvoiceLineItem.LabOrderId</c> was never assigned anywhere in the
/// repository. It is the ONLY side of the lab-order ↔ invoice-line relationship that EF
/// configures and the only side <c>CommissionService</c> reads, so a permanently empty
/// column meant the doctor's commission was always computed after deducting
/// <c>ClinicService.DefaultLabCost</c> — an estimate — instead of what the lab actually
/// charges.
///
/// <para>
/// These tests pin the linking half of the fix and, just as importantly, its refusal to
/// guess. The owner's decision is "link automatically ONLY when unambiguous": attaching a
/// lab cost to the wrong invoice line moves one patient's lab bill onto another service
/// and silently corrupts a DIFFERENT doctor's commission, which is the exact damage this
/// work exists to prevent. An unlinked order merely keeps today's behaviour; a wrongly
/// linked one corrupts money.
/// </para>
///
/// <para>
/// Every "nothing was linked" test carries a CONTROL order in the same database and
/// through the same controller. Without it the assertion would also hold with the feature
/// switched off entirely, and would prove nothing.
/// </para>
/// </summary>
public class LabOrderInvoiceLinkTests : IDisposable
{
    private readonly AppDbContext _db;

    public LabOrderInvoiceLinkTests() => _db = LabOrdersTestData.CreateDb();

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Seeding ───────────────────────────────────────────────────────────────

    private Patient AddPatient(string firstName = "سالم")
    {
        var patient = LabOrdersTestData.BuildPatient(firstName);
        // Create resolves the order's branch from the patient first — without it the
        // action short-circuits on "لا يمكن إنشاء طلب معمل لمريض بلا فرع محدد".
        patient.BranchId = Guid.NewGuid();
        _db.Patients.Add(patient);
        return patient;
    }

    private Visit AddVisit(Guid patientId)
    {
        var visit = new Visit
        {
            PatientId = patientId,
            // Fixed date — never DateTime.Today (clinic time is UTC+3).
            VisitDate = new DateOnly(2026, 6, 15),
        };
        _db.Visits.Add(visit);
        return visit;
    }

    /// <param name="defaultLabCost">
    /// The resolver's only (weak) hint that a service normally involves lab work.
    /// </param>
    private ClinicService AddService(string arabicName, decimal defaultLabCost)
    {
        var service = new ClinicService
        {
            ArabicName = arabicName,
            EnglishName = arabicName,
            Code = $"SVC-{Guid.NewGuid():N}"[..10],
            DefaultPrice = 50_000m,
            DefaultLabCost = defaultLabCost,
        };
        _db.ClinicServices.Add(service);
        return service;
    }

    private Invoice AddInvoice(Guid patientId)
    {
        var invoice = new Invoice
        {
            PatientId = patientId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12],
            Subtotal = 50_000m,
            TotalAmount = 50_000m,
        };
        _db.Invoices.Add(invoice);
        return invoice;
    }

    private InvoiceLineItem AddLine(
        Guid invoiceId,
        Guid? relatedVisitId,
        ClinicService service,
        Guid? labOrderId = null)
    {
        var line = new InvoiceLineItem
        {
            InvoiceId = invoiceId,
            ServiceId = service.Id,
            ServiceNameSnapshot = service.ArabicName,
            Description = service.ArabicName,
            Quantity = 1,
            UnitPrice = 50_000m,
            TotalPrice = 50_000m,
            RelatedVisitId = relatedVisitId,
            LabOrderId = labOrderId,
        };
        _db.InvoiceLineItems.Add(line);
        return line;
    }

    private LabOrdersController BuildAdminController(Mock<IPatientAccessService>? access = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.BranchId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);
        // Admin bypasses PermissionGuard — the patient-access mock is the gate under test.
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);

        if (access is null)
        {
            access = new Mock<IPatientAccessService>();
            LabOrdersTestData.SetupNonDoctor(access);
        }

        return LabOrdersTestData.BuildController(_db, access, currentUser);
    }

    private static CreateLabOrderRequest CreateRequest(Guid patientId, Guid? visitId) => new()
    {
        PatientId = patientId,
        ApplianceType = "تاج خزفي",
        Priority = "normal",
        Currency = "YER",
        Cost = 12_000m,
        VisitId = visitId,
    };

    private static Guid CreatedId(IActionResult result)
    {
        var created = result.Should().BeOfType<CreatedAtActionResult>(
            "creating a lab order must succeed — the invoice link is an optimisation of the "
            + "commission figure, never a precondition of the order").Subject;
        created.RouteValues.Should().ContainKey("id");
        return (Guid)created.RouteValues!["id"]!;
    }

    private async Task<Guid> CreateOrderAsync(LabOrdersController controller, Guid patientId, Guid? visitId)
        => CreatedId(await controller.Create(CreateRequest(patientId, visitId)));

    private async Task<Guid?> LinkOfAsync(Guid lineItemId)
        => (await _db.InvoiceLineItems.AsNoTracking().SingleAsync(l => l.Id == lineItemId)).LabOrderId;

    // ── 1. The unambiguous case links ────────────────────────────────────────

    [Fact]
    public async Task Create_WithExactlyOneCandidateLineOnTheVisit_LinksItAutomatically()
    {
        var patient = AddPatient();
        var visit = AddVisit(patient.Id);
        var crown = AddService("تاج خزفي", defaultLabCost: 3_000m);
        var invoice = AddInvoice(patient.Id);
        var line = AddLine(invoice.Id, visit.Id, crown);
        await _db.SaveChangesAsync();

        var controller = BuildAdminController();
        var orderId = await CreateOrderAsync(controller, patient.Id, visit.Id);

        _db.ChangeTracker.Clear();

        (await LinkOfAsync(line.Id)).Should().Be(orderId,
            "InvoiceLineItem.LabOrderId is the source of truth and was never assigned by any "
            + "code path before CORE-FIN-LAB — the commission read a column that was always null");

        // LabOrder.InvoiceLineItemId is an abandoned bare column: no EF configuration, no
        // index, no navigation, zero readers. It must stay untouched.
        (await _db.LabOrders.AsNoTracking().SingleAsync(o => o.Id == orderId))
            .InvoiceLineItemId.Should().BeNull(
                "the abandoned reverse column must not be written — a second 'source of truth' "
                + "is how this relationship became inconsistent in the first place");
    }

    // ── 2. THE anti-guessing guarantee ───────────────────────────────────────

    [Fact]
    public async Task Create_WithTwoCandidateLinesOnTheVisit_LinksNothing()
    {
        var patient = AddPatient();
        var invoice = AddInvoice(patient.Id);

        // The ambiguous visit: two billed services that both plausibly involve lab work.
        // Nothing in the data says which one the appliance was made for.
        var ambiguousVisit = AddVisit(patient.Id);
        var crownLine = AddLine(invoice.Id, ambiguousVisit.Id, AddService("تاج خزفي", 3_000m));
        var bridgeLine = AddLine(invoice.Id, ambiguousVisit.Id, AddService("جسر ثابت", 4_000m));

        // CONTROL — a visit with a single line, same database, same controller, same call.
        // It proves automatic linking is live in this fixture, so "nothing was linked" on
        // the ambiguous visit is a decision and not the feature being absent.
        var clearVisit = AddVisit(patient.Id);
        var clearLine = AddLine(invoice.Id, clearVisit.Id, AddService("طقم متحرك", 5_000m));

        await _db.SaveChangesAsync();

        var controller = BuildAdminController();
        var ambiguousOrderId = await CreateOrderAsync(controller, patient.Id, ambiguousVisit.Id);
        var clearOrderId = await CreateOrderAsync(controller, patient.Id, clearVisit.Id);

        _db.ChangeTracker.Clear();

        (await LinkOfAsync(clearLine.Id)).Should().Be(clearOrderId,
            "control: the unambiguous visit must still link, otherwise the assertions below "
            + "would pass simply because nothing links at all");

        (await LinkOfAsync(crownLine.Id)).Should().BeNull(
            "picking either line is a coin flip: the wrong one puts this lab cost against "
            + "another service and silently corrupts a DIFFERENT doctor's commission");
        (await LinkOfAsync(bridgeLine.Id)).Should().BeNull(
            "neither 'first' nor 'nearest' is a rule — ambiguity must stay unlinked");

        (await _db.InvoiceLineItems.CountAsync(l => l.LabOrderId == ambiguousOrderId))
            .Should().Be(0, "the ambiguous order must own no invoice line at all");
    }

    [Fact]
    public async Task Create_WhenOnlyOneLineOnTheVisitInvolvesLabWork_LinksThatLine()
    {
        var patient = AddPatient();
        var visit = AddVisit(patient.Id);
        var invoice = AddInvoice(patient.Id);

        // A consultation never goes to a lab (DefaultLabCost = 0); the crown does. That is
        // the only signal available today, and it is what narrows two lines down to one.
        var consultationLine = AddLine(invoice.Id, visit.Id, AddService("كشف", 0m));
        var crownLine = AddLine(invoice.Id, visit.Id, AddService("تاج خزفي", 3_000m));
        await _db.SaveChangesAsync();

        var controller = BuildAdminController();
        var orderId = await CreateOrderAsync(controller, patient.Id, visit.Id);

        _db.ChangeTracker.Clear();

        (await LinkOfAsync(crownLine.Id)).Should().Be(orderId,
            "the only line whose service normally involves lab work is not ambiguous");
        (await LinkOfAsync(consultationLine.Id)).Should().BeNull(
            "a consultation must never carry a lab cost");
    }

    // ── 3. No visit → no signal, but the order is still created ──────────────

    [Fact]
    public async Task Create_WithoutAVisit_LinksNothing_AndStillSucceeds()
    {
        var patient = AddPatient();
        var visit = AddVisit(patient.Id);
        var invoice = AddInvoice(patient.Id);
        var line = AddLine(invoice.Id, visit.Id, AddService("تاج خزفي", 3_000m));
        await _db.SaveChangesAsync();

        var controller = BuildAdminController();

        // Walk-in remakes and appliances ordered ahead carry no visit. That is normal, not
        // an error, and it must not make the order unsavable.
        var noVisitOrderId = await CreateOrderAsync(controller, patient.Id, visitId: null);

        (await _db.LabOrders.AsNoTracking().CountAsync(o => o.Id == noVisitOrderId))
            .Should().Be(1, "an unresolved link must never block creating the order");
        (await _db.InvoiceLineItems.CountAsync(l => l.LabOrderId != null))
            .Should().Be(0, "with no visit there is no join to resolve on — guessing from the "
            + "patient alone would attach the cost to an unrelated service");

        // CONTROL — the same patient, the same line, but the order names the visit.
        var linkedOrderId = await CreateOrderAsync(controller, patient.Id, visit.Id);

        _db.ChangeTracker.Clear();
        (await LinkOfAsync(line.Id)).Should().Be(linkedOrderId,
            "control: linking is live, so the no-visit order was skipped by rule, not by absence");
    }

    // ── 4. Never steal a line that belongs to another lab order ──────────────

    [Fact]
    public async Task Create_NeverStealsALineAlreadyLinkedToAnotherLabOrder()
    {
        var patient = AddPatient();
        var visit = AddVisit(patient.Id);
        var invoice = AddInvoice(patient.Id);

        var earlierOrder = LabOrdersTestData.BuildLabOrder(patient.Id, status: "sent");
        earlierOrder.VisitId = visit.Id;
        earlierOrder.Cost = 9_000m;
        earlierOrder.TotalCost = 9_000m;
        _db.LabOrders.Add(earlierOrder);

        // Two lines on one visit — but one already carries the earlier order's cost.
        var claimedLine = AddLine(invoice.Id, visit.Id, AddService("تاج خزفي", 3_000m), labOrderId: earlierOrder.Id);
        var freeLine = AddLine(invoice.Id, visit.Id, AddService("جسر ثابت", 4_000m));
        await _db.SaveChangesAsync();

        var controller = BuildAdminController();
        var newOrderId = await CreateOrderAsync(controller, patient.Id, visit.Id);

        _db.ChangeTracker.Clear();

        (await LinkOfAsync(claimedLine.Id)).Should().Be(earlierOrder.Id,
            "stealing the line would both misprice this order and strip the earlier one — two "
            + "wrong commissions from one write");
        (await LinkOfAsync(freeLine.Id)).Should().Be(newOrderId,
            "excluding the already-claimed line leaves exactly one candidate, which is not a guess");
        (await _db.InvoiceLineItems.CountAsync(l => l.LabOrderId == newOrderId))
            .Should().Be(1, "one lab order must not end up owning two invoice lines");
    }

    // ── 9. The admin fix-up list ─────────────────────────────────────────────

    private static IReadOnlyList<LabOrdersController.UnlinkedLabOrderDto> UnlinkedRows(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var data = ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value);
        return ((IEnumerable<LabOrdersController.UnlinkedLabOrderDto>)data!).ToList();
    }

    private static int UnlinkedTotal(IActionResult result)
    {
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        return (int)ok.Value!.GetType().GetProperty("total")!.GetValue(ok.Value)!;
    }

    [Fact]
    public async Task Unlinked_ListsOnlyUnlinkedOrders_WithATruthfulArabicReason()
    {
        var patient = AddPatient();
        var invoice = AddInvoice(patient.Id);

        // (a) resolvable but unlinked: seeded directly, i.e. created before automatic
        //     linking existed. It is a real repair item and the easiest one.
        var legacyVisit = AddVisit(patient.Id);
        AddLine(invoice.Id, legacyVisit.Id, AddService("طقم متحرك", 5_000m));
        var legacyOrder = LabOrdersTestData.BuildLabOrder(patient.Id, status: "sent");
        legacyOrder.VisitId = legacyVisit.Id;
        _db.LabOrders.Add(legacyOrder);

        // (b) cancelled and unlinked: owes the lab nothing and deducts nothing, so linking
        //     it would fix nothing — not a fix-up item.
        var cancelledOrder = LabOrdersTestData.BuildLabOrder(patient.Id, status: "cancelled");
        _db.LabOrders.Add(cancelledOrder);

        // (c) a visit that will link cleanly, and (d) one that is ambiguous.
        var clearVisit = AddVisit(patient.Id);
        AddLine(invoice.Id, clearVisit.Id, AddService("تاج خزفي", 3_000m));
        var ambiguousVisit = AddVisit(patient.Id);
        AddLine(invoice.Id, ambiguousVisit.Id, AddService("جسر ثابت", 4_000m));
        AddLine(invoice.Id, ambiguousVisit.Id, AddService("وجه خزفي", 6_000m));

        await _db.SaveChangesAsync();

        var controller = BuildAdminController();
        var linkedOrderId = await CreateOrderAsync(controller, patient.Id, clearVisit.Id);
        var ambiguousOrderId = await CreateOrderAsync(controller, patient.Id, ambiguousVisit.Id);
        var noVisitOrderId = await CreateOrderAsync(controller, patient.Id, visitId: null);

        _db.ChangeTracker.Clear();
        var rows = UnlinkedRows(await controller.GetUnlinked());

        rows.Select(r => r.Id).Should().NotContain(linkedOrderId,
            "an order that already owns an invoice line is not a fix-up item");
        rows.Select(r => r.Id).Should().NotContain(cancelledOrder.Id,
            "a cancelled order deducts nothing from any commission — listing it would bury the real gaps");

        var ambiguousRow = rows.Should().ContainSingle(r => r.Id == ambiguousOrderId).Which;
        ambiguousRow.ReasonCode.Should().Be("ambiguous");
        ambiguousRow.CandidateCount.Should().Be(2, "the count is what makes an ambiguous row actionable");
        ambiguousRow.Reason.Should().Contain("بنود فاتورة محتملة",
            "every user-facing message in this project is Arabic");
        ambiguousRow.PatientName.Should().Contain(patient.FirstName);

        var noVisitRow = rows.Should().ContainSingle(r => r.Id == noVisitOrderId).Which;
        noVisitRow.ReasonCode.Should().Be("no_visit");
        noVisitRow.Reason.Should().Contain("زيارة");

        var legacyRow = rows.Should().ContainSingle(r => r.Id == legacyOrder.Id).Which;
        legacyRow.ReasonCode.Should().Be("resolvable",
            "calling a linkable historical order 'no candidate' would be false, and it is the "
            + "cheapest repair on the list");
        legacyRow.Reason.Should().Contain("الربط التلقائي");
    }

    [Fact]
    public async Task Unlinked_ByRestrictedDoctor_HidesOtherPatientsRows()
    {
        var mine = AddPatient("سالم");
        var other = AddPatient("ناصر");

        var mineOrder = LabOrdersTestData.BuildLabOrder(mine.Id, status: "sent");
        var otherOrder = LabOrdersTestData.BuildLabOrder(other.Id, status: "sent");
        _db.LabOrders.AddRange(mineOrder, otherOrder);
        await _db.SaveChangesAsync();

        // These rows carry patient names and file numbers, and the class-level
        // PatientAccessFilter never fires here (no "patientId" route/query value), so the
        // doctor filter has to be explicit inside the action.
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(p => p.IsDoctor).Returns(true);
        access.SetupGet(p => p.HasFullAccess).Returns(false);
        access.Setup(p => p.GetAccessiblePatientIdsAsync())
            .ReturnsAsync(new HashSet<Guid> { mine.Id });

        var controller = BuildAdminController(access);
        var result = await controller.GetUnlinked();
        var rows = UnlinkedRows(result);

        rows.Select(r => r.Id).Should().Contain(mineOrder.Id);
        rows.Select(r => r.PatientId).Should().NotContain(other.Id,
            "a restricted doctor must never read another patient's name or file number");
        rows.Should().HaveCount(1);
        UnlinkedTotal(result).Should().Be(1,
            "the total is what the UI paginates on — an unfiltered count would leak how many "
            + "other patients' orders exist");
    }

    [Fact]
    public void Unlinked_IsAdminOnly()
    {
        var method = typeof(LabOrdersController).GetMethod(nameof(LabOrdersController.GetUnlinked));
        method.Should().NotBeNull("GET /api/lab-orders/unlinked is the admin fix-up list");

        var authorize = method!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull(
            "the class-level StaffOnly policy is not enough for an administrative view over "
            + "commission-affecting financial data");
        authorize!.Policy.Should().Be("AdminOnly");
    }
}
