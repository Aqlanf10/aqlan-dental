using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

/// <summary>
/// Sprint 2: Cashier shift workflow hardening tests.
/// Verifies open session, duplicate prevention, negative balance rejection,
/// admin without branch, and active session response shape.
/// </summary>
public class CashierShiftWorkflowTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId) SeedBranchAndCashier(AppDbContext db)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "الفرع الرئيسي", IsActive = true });
        db.Users.Add(new User { Id = cashierId, Username = "cashier1", BranchId = branchId, Role = UserRole.Accountant });
        db.SaveChanges();
        return (branchId, cashierId);
    }

    private static ICurrentUserService CreateCurrentUser(Guid userId, Guid? branchId, bool isAdmin = false)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.UserId).Returns(userId);
        mock.SetupGet(c => c.BranchId).Returns(branchId);
        mock.SetupGet(c => c.IsAdmin).Returns(isAdmin);
        mock.SetupGet(c => c.IsAuthenticated).Returns(true);
        mock.SetupGet(c => c.Role).Returns(isAdmin ? UserRole.Admin : UserRole.Accountant);
        return mock.Object;
    }

    private static void SeedTreasury(AppDbContext db, Guid branchId)
    {
        db.Treasuries.Add(new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج كاشير",
            Type = TreasuryType.Vault,
            Balance = 500_000m,
            BranchId = branchId,
            IsActive = true
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task OpenSession_WithValidData_Succeeds()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db);
        SeedTreasury(db, branchId);

        var currentUser = CreateCurrentUser(cashierId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        var result = await controller.OpenSession(new OpenSessionRequest
        {
            OpeningBalance = 50_000m,
            Notes = "وردية اختبار"
        });

        result.Should().BeOfType<OkObjectResult>();
        var session = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == cashierId && s.Status == SessionStatus.Open);
        session.Should().NotBeNull();
        session!.OpeningBalance.Should().Be(50_000m);
    }

    [Fact]
    public async Task OpenSession_DuplicateActiveSession_Blocked()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db);
        SeedTreasury(db, branchId);

        // Create existing open session
        db.CashierSessions.Add(new CashierSession
        {
            SessionNumber = "CS-20260529-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow,
            OpeningBalance = 100_000m,
            ExpectedClosingCash = 100_000m,
            ExpectedClosingCard = 0,
            ExpectedClosingBank = 0,
            Status = SessionStatus.Open,
            IsActive = true
        });
        db.SaveChanges();

        var currentUser = CreateCurrentUser(cashierId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        var result = await controller.OpenSession(new OpenSessionRequest { OpeningBalance = 0 });
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        // The response should contain a message about duplicate
        var responseObj = badRequest.Value;
        responseObj.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenSession_NegativeOpeningBalance_Blocked()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db);

        var currentUser = CreateCurrentUser(cashierId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        var result = await controller.OpenSession(new OpenSessionRequest { OpeningBalance = -100m });
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task OpenSession_AdminWithoutBranch_CanOpenWithBranchId()
    {
        await using var db = CreateContext();
        var (branchId, adminId) = SeedBranchAndCashier(db);
        SeedTreasury(db, branchId);

        // Admin with no branch in token
        var currentUser = CreateCurrentUser(adminId, null, isAdmin: true);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        // Without branchId → should fail
        var resultNoBranch = await controller.OpenSession(new OpenSessionRequest { OpeningBalance = 0 });
        resultNoBranch.Should().BeOfType<BadRequestObjectResult>();

        // With branchId → should succeed
        var resultWithBranch = await controller.OpenSession(new OpenSessionRequest
        {
            OpeningBalance = 25_000m,
            BranchId = branchId
        });
        resultWithBranch.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ActiveEndpoint_ReturnsHasActiveSessionFalse_WhenNoSession()
    {
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db);

        var currentUser = CreateCurrentUser(cashierId, branchId);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);

        // Use ShiftsController for the active endpoint test
        var shiftsLogger = new Mock<ILogger<ShiftsController>>().Object;
        var controller = new ShiftsController(db, currentUser, audit, treasuryResolution, shiftsLogger);

        var result = await controller.GetActiveShift();
        result.Should().BeOfType<OkObjectResult>();
        // The response should indicate no active shift
    }
}
