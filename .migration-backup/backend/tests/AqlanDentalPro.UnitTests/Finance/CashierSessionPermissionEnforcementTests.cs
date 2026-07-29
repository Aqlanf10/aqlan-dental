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
/// FIN-PERM: verifies CashierSessionsController enforces the granular
/// finance.cashier_session permission on top of the FinanceAccess policy. Admin
/// bypasses; a non-admin role is allowed only for the exact actions granted in
/// the database (open/close = create, view = view, reconcile = approve).
/// </summary>
public class CashierSessionPermissionEnforcementTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService User(UserRole role, Guid? branchId)
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
            Resource = "finance.cashier_session",
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
            CanApprove = approve,
        });
        db.SaveChanges();
    }

    private static CashierSessionsController Build(AppDbContext db, ICurrentUserService user)
    {
        var audit = new Mock<IAuditService>().Object;
        var treasury = new TreasuryResolutionService(db, NullLogger<TreasuryResolutionService>.Instance);
        return new CashierSessionsController(db, user, audit, treasury, NullLogger<CashierSessionsController>.Instance);
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    [Fact]
    public async Task Reception_WithoutCashierSessionPermission_CannotViewActiveSession()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid(); // no RolePermissions seeded
        var controller = Build(db, User(UserRole.Reception, branchId));

        var result = await controller.GetActiveSession();

        StatusOf(result).Should().Be(403, "Reception without finance.cashier_session.view is denied");
    }

    [Fact]
    public async Task Reception_WithCreateButNoApprove_CannotReconcile()
    {
        await using var db = CreateDb();
        Grant(db, "Reception", view: true, create: true); // approve NOT granted
        var controller = Build(db, User(UserRole.Reception, Guid.NewGuid()));

        var result = await controller.ReconcileSession(Guid.NewGuid(), notes: null);

        StatusOf(result).Should().Be(403, "reconciliation requires finance.cashier_session.approve");
    }

    [Fact]
    public async Task Admin_BypassesGuard_EvenWithNoRolePermissionRows()
    {
        await using var db = CreateDb(); // no RolePermissions seeded
        var controller = Build(db, User(UserRole.Admin, Guid.NewGuid()));

        var result = await controller.GetActiveSession();

        StatusOf(result).Should().NotBe(403, "Admin always bypasses PermissionGuard");
    }
}
