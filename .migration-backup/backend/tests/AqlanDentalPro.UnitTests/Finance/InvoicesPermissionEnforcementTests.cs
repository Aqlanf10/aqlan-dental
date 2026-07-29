using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Finance;
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

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// FIN-PERMS-A · A1: verifies InvoicesController enforces the granular
/// finance.invoices permission on top of the FinanceAccess policy. Admin always
/// bypasses; Reception is seeded view-only (can read invoices and PDFs, but cannot
/// create/edit/issue/cancel — draft invoices for the journey are created via
/// CheckoutService, not this controller). Issue/Cancel = approve (Accountant/Admin).
/// </summary>
public class InvoicesPermissionEnforcementTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService User(UserRole role, Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        mock.SetupGet(u => u.Role).Returns(role);
        mock.SetupGet(u => u.IsAdmin).Returns(role == UserRole.Admin);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.BranchId).Returns(branchId);
        return mock.Object;
    }

    private static void Grant(AppDbContext db, string role,
        bool view = false, bool create = false, bool edit = false,
        bool delete = false, bool export = false, bool approve = false)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role,
            Resource = "finance.invoices",
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
            CanDelete = delete,
            CanExport = export,
            CanApprove = approve,
        });
        db.SaveChanges();
    }

    private static InvoicesController Build(AppDbContext db, ICurrentUserService user)
    {
        var pdf = new Mock<IPdfService>();
        var audit = new Mock<IAuditService>();
        var commission = new Mock<ICommissionService>();
        return new InvoicesController(
            db, pdf.Object, audit.Object,
            NullLogger<InvoicesController>.Instance,
            commission.Object, user, new FinanceSettingsReader(db));
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    private static string? MessageOf(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotCreateInvoice()
    {
        // Reception is seeded view-only on finance.invoices (Group A scope decision).
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // create = false
        var controller = Build(db, User(UserRole.Reception));

        var result = await controller.Create(new CreateInvoiceRequest
        {
            PatientId = Guid.NewGuid(),
        });

        StatusOf(result).Should().Be(403, "Reception is not granted finance.invoices.create");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotEditInvoice()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // edit = false
        var controller = Build(db, User(UserRole.Reception));

        var result = await controller.Update(Guid.NewGuid(), new UpdateInvoiceRequest { Notes = "x" });

        StatusOf(result).Should().Be(403, "Reception is not granted finance.invoices.edit");
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotIssueInvoice()
    {
        // Issue = approve (Accountant/Admin only — Reception view-only).
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // approve = false
        var controller = Build(db, User(UserRole.Reception));

        var result = await controller.Issue(Guid.NewGuid());

        StatusOf(result).Should().Be(403, "Reception is not granted finance.invoices.approve (issue)");
    }

    [Fact]
    public async Task Reception_ViewOnly_CannotCancelInvoice()
    {
        // Cancel = approve (Accountant/Admin only — Reception view-only).
        await using var db = CreateDb();
        Grant(db, "Reception", view: true); // approve = false
        var controller = Build(db, User(UserRole.Reception));

        var result = await controller.Cancel(Guid.NewGuid(), new AqlanDentalPro.API.Controllers.CancelInvoiceRequest { Notes = "x" });

        StatusOf(result).Should().Be(403, "Reception is not granted finance.invoices.approve (cancel)");
    }

    [Fact]
    public async Task Reception_WithViewGrant_CanListInvoices()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true);
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.GetAll();

        // Reception with view grant is allowed past the permission gate; the empty
        // InMemory DB returns an OkObjectResult with an empty invoices list.
        result.Should().BeOfType<OkObjectResult>("Reception is granted finance.invoices.view");
    }

    [Fact]
    public async Task Reception_WithViewGrant_CanGetInvoicePdf()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true);
        var controller = Build(db, User(UserRole.Reception));
        // The IPdfService mock is not configured to return bytes; what we assert here
        // is that the permission gate passes (no 403) — the call proceeds into the
        // try block where the mocked pdfService.GenerateInvoicePdfAsync returns null,
        // which surfaces as a 500 from the catch-all handler. The test asserts the
        // 403 permission gate did NOT short-circuit the request.
        var invoiceId = Guid.NewGuid();
        var result = await controller.GetInvoicePdf(invoiceId);
        StatusOf(result).Should().NotBe(403, "Reception with finance.invoices.view must pass the gate");
    }

    [Fact]
    public async Task Admin_BypassesGuard_EvenWithNoRolePermissionRows()
    {
        // No RolePermissions seeded at all — Admin must still pass.
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>("Admin always bypasses PermissionGuard");
    }

    [Fact]
    public async Task Accountant_WithApproveGrant_CanIssueInvoice()
    {
        // Accountant is seeded (view, create, edit, export, approve) — verify the
        // approve grant lets Issue proceed past the permission gate. The InMemory DB
        // has no invoice so the action returns 404 NotFound — the assertion is that
        // the permission gate did NOT short-circuit to 403.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, export: true, approve: true);
        var controller = Build(db, User(UserRole.Accountant));

        var result = await controller.Issue(Guid.NewGuid());

        StatusOf(result).Should().NotBe(403, "Accountant has finance.invoices.approve — gate must pass");
    }
}
