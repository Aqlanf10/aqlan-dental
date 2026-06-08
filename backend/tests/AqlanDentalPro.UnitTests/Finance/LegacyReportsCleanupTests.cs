using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Sprint 4: Tests for legacy reports cleanup — Profit-Loss and Daily Cash Summary
/// now read from JournalEntry/JournalLine instead of CashFlowTransaction.
/// </summary>
public class LegacyReportsCleanupTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ICurrentUserService CreateAdminUser(Guid? branchId = null)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.Role).Returns(UserRole.Admin);
        mock.Setup(u => u.IsAdmin).Returns(true);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static ICurrentUserService CreateBranchUser(Guid userId, Guid branchId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.UserId).Returns(userId);
        mock.Setup(u => u.Role).Returns(UserRole.Accountant);
        mock.Setup(u => u.IsAdmin).Returns(false);
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        mock.Setup(u => u.BranchId).Returns(branchId);
        mock.Setup(u => u.IsImpersonating).Returns(false);
        mock.Setup(u => u.OriginalUserId).Returns((Guid?)null);
        return mock.Object;
    }

    private static (Guid branchId, Guid userId) SeedBranchAndUser(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = userId, Username = "admin1", BranchId = branchId });
        db.SaveChanges();
        return (branchId, userId);
    }

    private static ReportsController BuildReportsController(AppDbContext db, ICurrentUserService? currentUser = null)
    {
        currentUser ??= CreateAdminUser();
        var pdfService = new Mock<IPdfService>().Object;
        var logger = new Mock<ILogger<ReportsController>>().Object;
        return new ReportsController(db, pdfService, logger, currentUser);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 1. Profit-Loss Report returns 200 for valid dates
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProfitLoss_ValidDates_ReturnsOk()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var controller = BuildReportsController(db, CreateAdminUser(branchId));
        var result = await controller.GetProfitLossReport("2026-01-01", "2026-01-31");

        result.Should().BeOfType<OkObjectResult>(
            "Profit-Loss report with valid dates should return 200 OK, not 500");
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        // Verify legacy response shape is preserved
        responseType.GetProperty("grossRevenue").Should().NotBeNull("grossRevenue must exist in response");
        responseType.GetProperty("totalRefunds").Should().NotBeNull("totalRefunds must exist in response");
        responseType.GetProperty("netRevenue").Should().NotBeNull("netRevenue must exist in response");
        responseType.GetProperty("totalDirectCosts").Should().NotBeNull("totalDirectCosts must exist in response");
        responseType.GetProperty("grossProfit").Should().NotBeNull("grossProfit must exist in response");
        responseType.GetProperty("totalOpex").Should().NotBeNull("totalOpex must exist in response");
        responseType.GetProperty("salaryAdvances").Should().NotBeNull("salaryAdvances must exist in response (C4 fix)");
        responseType.GetProperty("netProfit").Should().NotBeNull("netProfit must exist in response");
    }

    [Fact]
    public async Task ProfitLoss_NoData_ReturnsOkWithZeros()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var controller = BuildReportsController(db, CreateAdminUser(branchId));
        var result = await controller.GetProfitLossReport("2026-01-01", "2026-01-31");

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var grossRevenue = (decimal)response.GetType().GetProperty("grossRevenue")!.GetValue(response)!;
        grossRevenue.Should().Be(0m, "No journal entries means zero revenue");
    }

    [Fact]
    public async Task ProfitLoss_WithJournalEntries_ReturnsCorrectFigures()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var treasuryId = Guid.NewGuid();
        db.Treasuries.Add(new Treasury { Id = treasuryId, Name = "الخزنة", Type = TreasuryType.Vault, Balance = 5000m, BranchId = branchId, IsActive = true });

        var paymentId = Guid.NewGuid();
        var jeId = Guid.NewGuid();
        db.JournalEntries.Add(new JournalEntry
        {
            Id = jeId,
            EntryNumber = "JE-20260115-001",
            FinancialDocumentType = FinancialDocumentType.Payment,
            FinancialDocumentId = paymentId,
            EntryDate = new DateOnly(2026, 1, 15),
            Description = "Test payment",
            BranchId = branchId,
            PerformedBy = userId,
            IsPosted = true,
            IsReversal = false
        });

        db.JournalLines.Add(new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntryId = jeId,
            AccountType = JournalAccountType.Treasury, AccountId = treasuryId,
            Debit = 1000m, Credit = 0m, BranchId = branchId
        });

        db.JournalLines.Add(new JournalLine
        {
            Id = Guid.NewGuid(), JournalEntryId = jeId,
            AccountType = JournalAccountType.Revenue, AccountId = Guid.NewGuid(),
            Debit = 0m, Credit = 1000m, BranchId = branchId
        });

        await db.SaveChangesAsync();

        var controller = BuildReportsController(db, CreateAdminUser(branchId));
        var result = await controller.GetProfitLossReport("2026-01-01", "2026-01-31");

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var grossRevenue = (decimal)response.GetType().GetProperty("grossRevenue")!.GetValue(response)!;
        grossRevenue.Should().Be(1000m, "One payment of 1000 should appear as gross revenue");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 2. Daily Cash Summary returns 200 for valid date + admin branch handling
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DailyCashSummary_ValidDate_WithBranchId_ReturnsOk()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var controller = BuildReportsController(db, CreateAdminUser(branchId));
        var result = await controller.GetDailyCashSummary("2026-01-15", branchId);

        result.Should().BeOfType<OkObjectResult>(
            "Daily Cash Summary with valid date and branchId should return 200 OK, not 400");
        var ok = (OkObjectResult)result;
        var response = ok.Value!;
        var responseType = response.GetType();

        responseType.GetProperty("Date").Should().NotBeNull();
        responseType.GetProperty("BranchId").Should().NotBeNull();
        responseType.GetProperty("OpeningBalance").Should().NotBeNull();
        responseType.GetProperty("Inflows").Should().NotBeNull();
        responseType.GetProperty("Outflows").Should().NotBeNull();
        responseType.GetProperty("TotalInflows").Should().NotBeNull();
        responseType.GetProperty("TotalOutflows").Should().NotBeNull();
        responseType.GetProperty("NetCashFlow").Should().NotBeNull();
        responseType.GetProperty("ClosingBalance").Should().NotBeNull();
        responseType.GetProperty("Sessions").Should().NotBeNull();
    }

    [Fact]
    public async Task DailyCashSummary_AdminNoBranch_FallsBackToFirstBranch()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي" });
        db.Users.Add(new User { Id = Guid.NewGuid(), Username = "admin-no-branch", BranchId = null });
        await db.SaveChangesAsync();

        var controller = BuildReportsController(db, CreateAdminUser(null));
        var result = await controller.GetDailyCashSummary("2026-01-15", null);

        result.Should().BeOfType<OkObjectResult>(
            "Admin without branch should fallback to first active branch, not return 400");
    }

    [Fact]
    public async Task DailyCashSummary_NonAdminNoBranch_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Username = "branchless-user", BranchId = null });
        await db.SaveChangesAsync();

        var controller = BuildReportsController(db, CreateBranchUser(userId, Guid.Empty));
        var result = await controller.GetDailyCashSummary("2026-01-15", null);

        result.Should().BeOfType<BadRequestObjectResult>(
            "Non-admin without branch should return 400 Bad Request");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 3. Daily Cash Summary returns 400 for invalid date
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DailyCashSummary_InvalidDate_ReturnsBadRequest()
    {
        await using var db = CreateDb();
        var (branchId, userId) = SeedBranchAndUser(db);

        var controller = BuildReportsController(db, CreateAdminUser(branchId));
        var result = await controller.GetDailyCashSummary("not-a-date", branchId);

        result.Should().BeOfType<BadRequestObjectResult>(
            "Invalid date should still return 400 Bad Request");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 4. Finance V3 vault-transfers endpoint still exists
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FinanceV3_VaultTransfers_RouteExists()
    {
        var method = typeof(FinanceV3Controller).GetMethod("GetVaultTransfers");
        method.Should().NotBeNull("FinanceV3 vault-transfers GET endpoint must exist");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 5/6. CreatePayment and DeletePayment still work (smoke test)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PaymentsController_CreatePayment_Exists()
    {
        var method = typeof(PaymentsController).GetMethod("CreatePayment");
        method.Should().NotBeNull("PaymentsController.CreatePayment must exist");
    }

    [Fact]
    public void PaymentsController_DeletePayment_IsAdminOnly()
    {
        var method = typeof(PaymentsController).GetMethod("DeletePayment");
        method.Should().NotBeNull("PaymentsController.DeletePayment must exist");

        var adminOnlyAttr = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();
        adminOnlyAttr.Should().NotBeNull("DeletePayment must have [Authorize] attribute");
        adminOnlyAttr!.Policy.Should().Be("AdminOnly", "DeletePayment must be AdminOnly");
    }
}
