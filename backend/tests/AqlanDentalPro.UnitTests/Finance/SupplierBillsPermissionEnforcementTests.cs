using AqlanDentalPro.API.Controllers;
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
/// FIN-PERMS-B · B2: verifies SupplierBillsController enforces the granular
/// finance.expenses permission on top of the ReportsAccess policy (Admin +
/// Accountant; Reception already excluded). Admin always bypasses. Accountant is
/// seeded view/create/edit/approve but NOT delete — so the accountant can register
/// and pay bills but cannot cancel (soft-delete) them. Pay = create (creates a
/// SupplierBillPayment + CashFlowTransaction + JournalEntry outflow).
/// </summary>
public class SupplierBillsPermissionEnforcementTests
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

    /// <summary>
    /// Seeds finance.expenses RolePermission for the given role. Defaults match the
    /// production DbSeeder seed: Admin = all; Accountant = view/create/edit (NO delete,
    /// NO export), approve = true.
    /// </summary>
    private static void Grant(AppDbContext db, string role,
        bool view = false, bool create = false, bool edit = false,
        bool delete = false, bool export = false, bool approve = false)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role,
            Resource = "finance.expenses",
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
            CanDelete = delete,
            CanExport = export,
            CanApprove = approve,
        });
        db.SaveChanges();
    }

    private static SupplierBillsController Build(AppDbContext db, ICurrentUserService user)
    {
        var audit = new Mock<IAuditService>();
        var journalEntryService = new JournalEntryService(db, NullLogger<JournalEntryService>.Instance);
        var treasuryResolution = new TreasuryResolutionService(db, NullLogger<TreasuryResolutionService>.Instance);
        return new SupplierBillsController(
            db, user, audit.Object, journalEntryService, treasuryResolution);
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    private static string? MessageOf(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task Accountant_NotSeededDelete_CannotCancelBill()
    {
        // Accountant is seeded (view, create, edit, approve) but NOT delete on
        // finance.expenses. Cancelling a supplier bill (soft-delete) is admin-only.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true); // delete = false
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Cancel(Guid.NewGuid());

        StatusOf(result).Should().Be(403, "Accountant is not granted finance.expenses.delete");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }

    [Fact]
    public async Task Accountant_WithViewGrant_CanListBills()
    {
        // Accountant is seeded view=true on finance.expenses.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true);
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        await db.SaveChangesAsync();
        var controller = Build(db, User(UserRole.Accountant, branchId));

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>("Accountant has finance.expenses.view");
    }

    [Fact]
    public async Task Accountant_WithCreateGrant_CanRegisterBill_GatePasses()
    {
        // Accountant is seeded create=true on finance.expenses. The permission gate
        // passes; the action proceeds into validation logic which rejects with a 400
        // (empty description). The assertion is the gate did NOT short-circuit to 403.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true);
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Create(new CreateSupplierBillRequest
        {
            SupplierId = Guid.NewGuid(),
            Description = "", // invalid — but the gate must let it through to validation
            TotalAmount = 100m,
        });

        StatusOf(result).Should().NotBe(403, "Accountant has finance.expenses.create — gate must pass");
    }

    [Fact]
    public async Task Accountant_WithCreateGrant_CanPayBill_GatePasses()
    {
        // Paying a supplier bill maps to finance.expenses.create (it creates a
        // SupplierBillPayment + CashFlowTransaction + JournalEntry outflow — a financial
        // write that posts to the GL). Accountant is seeded create=true so the gate
        // passes; the action then looks up the bill in the empty InMemory DB → 404.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true);
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Pay(Guid.NewGuid(), new PayBillInstallmentRequest
        {
            Amount = 100m,
            PaymentMethod = "cash",
        });

        StatusOf(result).Should().NotBe(403, "Accountant has finance.expenses.create (pay) — gate must pass");
    }

    [Fact]
    public async Task Accountant_WithoutCreateGrant_CannotPayBill()
    {
        // If the owner revokes finance.expenses.create from Accountant via Settings,
        // the accountant must be blocked from paying supplier bills (paying creates a
        // posted payment record — a financial write).
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true); // create = false (owner-revoked)
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Pay(Guid.NewGuid(), new PayBillInstallmentRequest
        {
            Amount = 100m,
            PaymentMethod = "cash",
        });

        StatusOf(result).Should().Be(403, "Accountant with finance.expenses.create revoked cannot pay bills");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }

    [Fact]
    public async Task Admin_BypassesGuard_EvenWithNoRolePermissionRows()
    {
        // No RolePermissions seeded at all — Admin must still pass every gate.
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        await db.SaveChangesAsync();
        var controller = Build(db, User(UserRole.Admin, branchId));

        var listResult = await controller.GetAll();
        listResult.Should().BeOfType<OkObjectResult>("Admin always bypasses PermissionGuard");

        var statementResult = await controller.GetSupplierStatement(Guid.NewGuid());
        // Statement returns 404 (supplier not found) — but NOT 403 (admin bypass).
        StatusOf(statementResult).Should().NotBe(403, "Admin always bypasses the gate");
    }
}
