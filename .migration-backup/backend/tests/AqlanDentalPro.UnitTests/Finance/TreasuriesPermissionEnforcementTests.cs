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
/// FIN-PERMS-A · A3: verifies TreasuriesController enforces the granular
/// finance.treasuries permission on top of the FinanceAccess policy. Admin always
/// bypasses; Reception is NOT seeded for finance.treasuries → auto-denied on every
/// action. Accountant has view/create/edit (no delete/export/approve).
/// </summary>
public class TreasuriesPermissionEnforcementTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
        bool view = false, bool create = false, bool edit = false)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Role = role,
            Resource = "finance.treasuries",
            CanView = view,
            CanCreate = create,
            CanEdit = edit,
        });
        db.SaveChanges();
    }

    private static TreasuriesController Build(AppDbContext db, ICurrentUserService user)
    {
        var audit = new Mock<IAuditService>();
        return new TreasuriesController(db, user, audit.Object, NullLogger<TreasuriesController>.Instance);
    }

    private static int? StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? (result as StatusCodeResult)?.StatusCode;

    private static string? MessageOf(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    [Fact]
    public async Task Reception_AutoDenied_CannotListTreasuries()
    {
        // finance.treasuries is NOT seeded for Reception → HasAsync returns false.
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.GetAll();

        StatusOf(result).Should().Be(403, "Reception is not seeded for finance.treasuries → auto-denied");
        MessageOf(result).Should().Be("غير مصرح لك بهذا الإجراء المالي");
    }

    [Fact]
    public async Task Reception_AutoDenied_CannotViewTreasuryTransactions()
    {
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.GetTreasuryTransactions(Guid.NewGuid());

        StatusOf(result).Should().Be(403, "Reception is not seeded for finance.treasuries → auto-denied");
    }

    [Fact]
    public async Task Reception_AutoDenied_CannotCreateTreasury()
    {
        await using var db = CreateDb();
        var controller = Build(db, User(UserRole.Reception, branchId: Guid.NewGuid()));

        var result = await controller.Create(new CreateTreasuryRequest
        {
            Name = "خزنة اختبار",
            Type = "Vault",
            OpeningBalance = 0m,
        });

        StatusOf(result).Should().Be(403, "Reception is not seeded for finance.treasuries → auto-denied");
    }

    [Fact]
    public async Task Accountant_WithViewGrant_CanListTreasuries()
    {
        await using var db = CreateDb();
        Grant(db, "Accountant", view: true, create: true, edit: true);
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(), Name = "الخزنة الرئيسية", Type = TreasuryType.Vault,
            Balance = 50_000m, BranchId = branchId, IsActive = true,
        });
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
