using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// Dynamic finance permissions: the granular finance.* keys are seeded so the
/// owner can grant/revoke finance capabilities from Settings (RolePermission).
/// These tests lock the intended access design against the real enforcement
/// primitive (PermissionGuard): Reception is cashier-safe ONLY (collect payments /
/// print receipts / cashier session) and must NOT reach reports/treasuries/
/// expenses/commissions; Accountant keeps finance access; Admin bypasses.
/// </summary>
public class FinancePermissionGuardTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static ICurrentUserService User(UserRole role)
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(u => u.Role).Returns(role);
        return m.Object;
    }

    /// <summary>Mirrors the granular finance.* seed (Reception cashier-safe only).</summary>
    private static void SeedFinancePermissions(AppDbContext db)
    {
        db.RolePermissions.AddRange(
            new RolePermission { Role = "Reception", Resource = "finance.payments", CanView = true, CanCreate = true },
            new RolePermission { Role = "Reception", Resource = "finance.receipts", CanView = true, CanCreate = true },
            new RolePermission { Role = "Reception", Resource = "finance.cashier_session", CanView = true, CanCreate = true },
            // Reception is intentionally ABSENT from finance.reports / finance.treasuries /
            // finance.expenses / finance.commissions / finance.patient_balance / etc.
            new RolePermission { Role = "Accountant", Resource = "finance.reports", CanView = true, CanExport = true },
            new RolePermission { Role = "Accountant", Resource = "finance.payments", CanView = true, CanCreate = true, CanEdit = true, CanExport = true });
        db.SaveChanges();
    }

    [Fact]
    public async Task Reception_CanCollectAndPrintAndOpenSession_ButNotReportsOrTreasuries()
    {
        await using var db = CreateDb();
        SeedFinancePermissions(db);
        var reception = User(UserRole.Reception);

        (await PermissionGuard.HasAsync(db, reception, "finance.payments", "create")).Should().BeTrue();
        (await PermissionGuard.HasAsync(db, reception, "finance.receipts", "view")).Should().BeTrue();
        (await PermissionGuard.HasAsync(db, reception, "finance.cashier_session", "create")).Should().BeTrue();

        (await PermissionGuard.HasAsync(db, reception, "finance.reports", "view")).Should().BeFalse("Reception must not see financial reports");
        (await PermissionGuard.HasAsync(db, reception, "finance.treasuries", "view")).Should().BeFalse();
        (await PermissionGuard.HasAsync(db, reception, "finance.commissions", "view")).Should().BeFalse();
        // Reception has no delete on payments even though it can create.
        (await PermissionGuard.HasAsync(db, reception, "finance.payments", "delete")).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_BypassesGranularFinancePermissions_EvenWithNoRows()
    {
        await using var db = CreateDb();
        var admin = User(UserRole.Admin);

        (await PermissionGuard.HasAsync(db, admin, "finance.reports", "view")).Should().BeTrue();
        (await PermissionGuard.HasAsync(db, admin, "finance.treasuries", "delete")).Should().BeTrue();
        (await PermissionGuard.HasAsync(db, admin, "finance.commissions", "approve")).Should().BeTrue();
    }

    [Fact]
    public async Task Accountant_KeepsFinanceAccess()
    {
        await using var db = CreateDb();
        SeedFinancePermissions(db);
        var accountant = User(UserRole.Accountant);

        (await PermissionGuard.HasAsync(db, accountant, "finance.reports", "view")).Should().BeTrue();
        (await PermissionGuard.HasAsync(db, accountant, "finance.payments", "create")).Should().BeTrue();
    }
}
