using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Common;
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
/// FIN-PERMS-B · B1: verifies OperationalExpensesController enforces the granular
/// finance.expenses permission on top of the ReportsAccess policy (Admin + Accountant;
/// Reception already excluded). Admin always bypasses. Accountant is seeded
/// view/create/edit/approve but NOT delete — so the accountant can register and
/// approve expenses but cannot delete them (admin-only). Voucher PDF = view.
/// </summary>
public class OperationalExpensesPermissionEnforcementTests
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

    private static OperationalExpensesController Build(AppDbContext db, ICurrentUserService user)
    {
        var audit = new Mock<IAuditService>();
        var journalEntryService = new JournalEntryService(db, NullLogger<JournalEntryService>.Instance);
        var treasuryResolution = new TreasuryResolutionService(db, NullLogger<TreasuryResolutionService>.Instance);
        return new OperationalExpensesController(
            db, user, audit.Object, journalEntryService, treasuryResolution,
            new FinanceSettingsReader(db));
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    private static string? MessageOf(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task Accountant_NotSeededDelete_CannotDeleteExpense()
    {
        // Accountant is seeded (view, create, edit, approve) but NOT delete — the seeded
        // finance.expenses row has CanDelete=false for Accountant. Deleting posted
        // ledger history is admin-only.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true); // delete = false
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Delete(Guid.NewGuid());

        StatusOf(result).Should().Be(403, "Accountant is not granted finance.expenses.delete");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }

    [Fact]
    public async Task Accountant_WithApproveGrant_CanApproveExpense_GatePasses()
    {
        // Accountant is seeded approve=true on finance.expenses. The permission gate
        // should pass; the action then proceeds to look up the expense in the empty
        // InMemory DB → returns 404 NotFound. The assertion is that the gate did NOT
        // short-circuit to 403. (The [Authorize(Policy="AdminAccess")] attribute is a
        // controller-level coarse gate not enforced in unit tests; the granular check is
        // the meaningful unit-level gate and is the one under test.)
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true);
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Approve(Guid.NewGuid(), new ApproveExpenseRequest { Notes = "ok" });

        StatusOf(result).Should().NotBe(403, "Accountant has finance.expenses.approve — gate must pass");
    }

    [Fact]
    public async Task Accountant_WithViewGrant_CanListExpenses()
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
    public async Task Accountant_WithCreateGrant_CanCreateExpense_GatePasses()
    {
        // Accountant is seeded create=true on finance.expenses. The permission gate
        // passes; the action proceeds into validation logic which rejects with a 400
        // (empty title). The assertion is the gate did NOT short-circuit to 403.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true);
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Create(new CreateExpenseRequest
        {
            Title = "", // invalid — but the gate must let it through to validation
            Category = "Rent",
            Amount = 100m,
            PaymentMethod = "cash",
        });

        StatusOf(result).Should().NotBe(403, "Accountant has finance.expenses.create — gate must pass");
    }

    [Fact]
    public async Task Accountant_NotGrantedDelete_OnVoucherPdf_StillPassesGate()
    {
        // Voucher PDF maps to `view`, NOT `delete`. Accountant has view=true, so the
        // voucher PDF gate passes. The action then looks up the expense in the empty
        // InMemory DB → 404. Asserts the gate did NOT short-circuit to 403.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true, approve: true);
        var pdf = new Mock<IPdfService>();
        var audit = new Mock<IAuditService>();
        var journalEntryService = new JournalEntryService(db, NullLogger<JournalEntryService>.Instance);
        var treasuryResolution = new TreasuryResolutionService(db, NullLogger<TreasuryResolutionService>.Instance);
        var controller = new OperationalExpensesController(
            db, User(UserRole.Accountant, branchId: Guid.NewGuid()), audit.Object,
            journalEntryService, treasuryResolution, new FinanceSettingsReader(db));

        var result = await controller.DownloadDisbursementVoucher(Guid.NewGuid(), pdf.Object);

        StatusOf(result).Should().NotBe(403, "Accountant has finance.expenses.view — voucher PDF gate must pass");
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

        var pendingResult = await controller.GetPending();
        pendingResult.Should().BeOfType<OkObjectResult>("Admin always bypasses PermissionGuard on GetPending");
    }

    [Fact]
    public async Task Accountant_WithoutApproveGrant_CannotApproveExpense()
    {
        // If the owner revokes finance.expenses.approve from Accountant via Settings,
        // the accountant must be blocked from approving expenses.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true); // approve = false (owner-revoked)
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Approve(Guid.NewGuid(), new ApproveExpenseRequest { Notes = "ok" });

        StatusOf(result).Should().Be(403, "Accountant with finance.expenses.approve revoked cannot approve");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }
}
