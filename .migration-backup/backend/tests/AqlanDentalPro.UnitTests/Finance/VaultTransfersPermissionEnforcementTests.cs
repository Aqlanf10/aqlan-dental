using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// FIN-PERMS-A · A4: verifies VaultTransfersController enforces the granular
/// finance.treasuries permission on top of the FinanceAccess policy. Vault transfers
/// move money between treasuries, so they share the treasury permission key. Admin
/// always bypasses; Reception is NOT seeded → auto-denied. Accountant has
/// view+create+edit (no approve — so cannot approve/reject transfers unless the
/// owner explicitly grants approve on finance.treasuries).
/// </summary>
public class VaultTransfersPermissionEnforcementTests
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
        bool view = false, bool create = false, bool edit = false, bool approve = false)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role,
            Resource = "finance.treasuries",
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
            CanApprove = approve,
        });
        db.SaveChanges();
    }

    private static VaultTransfersController Build(AppDbContext db, ICurrentUserService user)
    {
        var audit = new Mock<IAuditService>();
        var je = new Mock<IJournalEntryService>();
        return new VaultTransfersController(db, user, audit.Object, je.Object);
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    private static string? MessageOf(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task Reception_AutoDenied_CannotListTransfers()
    {
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.GetAll();

        StatusOf(result).Should().Be(403, "Reception is not seeded for finance.treasuries → auto-denied");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }

    [Fact]
    public async Task Reception_AutoDenied_CannotCreateTransfer()
    {
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.Create(new CreateTransferRequest
        {
            DestinationTreasuryId = Guid.NewGuid(),
            Amount = 1_000m,
        });

        StatusOf(result).Should().Be(403, "Reception is not seeded for finance.treasuries → auto-denied");
    }

    [Fact]
    public async Task Reception_AutoDenied_CannotApproveTransfer()
    {
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.Approve(Guid.NewGuid(), new ApproveTransferRequest());

        StatusOf(result).Should().Be(403, "Reception is not seeded for finance.treasuries → auto-denied");
    }

    [Fact]
    public async Task Accountant_WithoutApproveGrant_CannotApproveTransfer()
    {
        // Accountant is seeded (view, create, edit) — approve NOT granted by default
        // on finance.treasuries. So Accountant cannot approve/reject vault transfers
        // unless the owner explicitly grants approve. Approve/Reject endpoints are
        // also guarded by [Authorize(Roles="Admin,Accountant")] but the granular
        // permission gate adds the owner-configurable layer.
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true); // approve = false
        var controller = Build(db, User(UserRole.Accountant, branchId: Guid.NewGuid()));

        var result = await controller.Approve(Guid.NewGuid(), new ApproveTransferRequest());

        StatusOf(result).Should().Be(403,
            "Accountant has no finance.treasuries.approve by default — owner can grant it from Settings");
    }

    [Fact]
    public async Task Accountant_WithViewGrant_CanListTransfers()
    {
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true);
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        await db.SaveChangesAsync();
        var controller = Build(db, User(UserRole.Accountant, branchId));

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>("Accountant has finance.treasuries.view");
    }

    [Fact]
    public async Task Admin_BypassesGuard_EvenWithNoRolePermissionRows()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        await db.SaveChangesAsync();
        var controller = Build(db, User(UserRole.Admin, branchId));

        var result = await controller.GetAll();

        result.Should().BeOfType<OkObjectResult>("Admin always bypasses PermissionGuard");
    }
}
