using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

public class CashierSessionActiveEndpointTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (Guid branchId, Guid cashierId) SeedBranchAndCashier(AppDbContext db, UserRole role)
    {
        var branchId = Guid.NewGuid();
        var cashierId = Guid.NewGuid();

        db.Branches.Add(new Branch { Id = branchId, Name = "فرع الاختبار" });
        db.Users.Add(new User { Id = cashierId, Username = "cashier_test", BranchId = branchId, Role = role });
        db.SaveChanges();

        return (branchId, cashierId);
    }

    private static ICurrentUserService CreateCurrentUser(Guid userId, Guid branchId, UserRole role)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.UserId).Returns(userId);
        mock.SetupGet(c => c.BranchId).Returns(branchId);
        mock.SetupGet(c => c.IsAdmin).Returns(role == UserRole.Admin);
        mock.SetupGet(c => c.IsAuthenticated).Returns(true);
        mock.SetupGet(c => c.Role).Returns(role);
        return mock.Object;
    }

    private static Treasury SeedVaultTreasury(AppDbContext db, Guid branchId)
    {
        var vault = new Treasury
        {
            Id = Guid.NewGuid(),
            Name = "درج الكاشير الرئيسي",
            Type = TreasuryType.Vault,
            Balance = 100_000m,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(vault);
        db.SaveChanges();
        return vault;
    }

    [Fact]
    public async Task GetActiveSession_NoOpenSession_ReturnsHasActiveSessionFalse()
    {
        // Arrange
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db, UserRole.Reception);
        var currentUser = CreateCurrentUser(cashierId, branchId, UserRole.Reception);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        // Act
        var result = await controller.GetActiveSession();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();
        
        // Use reflection to inspect dynamic object returned in Ok
        var valueType = okResult.Value.GetType();
        var hasActiveSessionProp = valueType.GetProperty("hasActiveSession");
        hasActiveSessionProp.Should().NotBeNull();
        
        var hasActiveSession = (bool)hasActiveSessionProp.GetValue(okResult.Value);
        hasActiveSession.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveSession_WithOpenSession_ReturnsSessionDataAndCalculatedValues()
    {
        // Arrange
        await using var db = CreateContext();
        var (branchId, cashierId) = SeedBranchAndCashier(db, UserRole.Reception);
        var vault = SeedVaultTreasury(db, branchId);
        var currentUser = CreateCurrentUser(cashierId, branchId, UserRole.Reception);
        var audit = new Mock<IAuditService>().Object;
        var treasuryResolution = new TreasuryResolutionService(db, new Mock<ILogger<TreasuryResolutionService>>().Object);
        var logger = new Mock<ILogger<CashierSessionsController>>().Object;

        var session = new CashierSession
        {
            Id = Guid.NewGuid(),
            SessionNumber = "CS-20260601-01",
            CashierId = cashierId,
            BranchId = branchId,
            OpeningTime = DateTime.UtcNow.AddHours(-2),
            OpeningBalance = 25_000m,
            ExpectedClosingCash = 25_000m,
            Status = SessionStatus.Open,
            TreasuryId = vault.Id,
            IsActive = true
        };
        db.CashierSessions.Add(session);
        db.SaveChanges();

        var controller = new CashierSessionsController(db, currentUser, audit, treasuryResolution, logger);

        // Act
        var result = await controller.GetActiveSession();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().NotBeNull();

        var valueType = okResult.Value.GetType();
        
        var hasActiveSessionProp = valueType.GetProperty("hasActiveSession");
        hasActiveSessionProp.Should().NotBeNull();
        ((bool)hasActiveSessionProp.GetValue(okResult.Value)).Should().BeTrue();

        var sessionNumberProp = valueType.GetProperty("SessionNumber");
        sessionNumberProp.Should().NotBeNull();
        ((string)sessionNumberProp.GetValue(okResult.Value)).Should().Be("CS-20260601-01");

        var cashierIdProp = valueType.GetProperty("CashierId");
        cashierIdProp.Should().NotBeNull();
        ((Guid)cashierIdProp.GetValue(okResult.Value)).Should().Be(cashierId);

        var openingBalanceProp = valueType.GetProperty("OpeningBalance");
        openingBalanceProp.Should().NotBeNull();
        ((decimal)openingBalanceProp.GetValue(okResult.Value)).Should().Be(25_000m);
    }

    [Fact]
    public void CashierSessionsController_AuthorizationPolicy_IsFinanceAccess()
    {
        // Verify class-level policy on CashierSessionsController is "FinanceAccess"
        // This policy allows Admin, Accountant, and Reception roles.
        var classAuthorizeAttr = typeof(CashierSessionsController).GetCustomAttribute<AuthorizeAttribute>();
        classAuthorizeAttr.Should().NotBeNull("CashierSessionsController must specify an AuthorizeAttribute at class level");
        classAuthorizeAttr!.Policy.Should().Be("FinanceAccess", 
            "Class-level authorization policy must be FinanceAccess to support Reception access safely.");

        // Verify action-levelHttpGet route is "active"
        var activeMethod = typeof(CashierSessionsController).GetMethod("GetActiveSession");
        activeMethod.Should().NotBeNull("GetActiveSession action must exist on the controller");

        var getAttr = activeMethod.GetCustomAttribute<HttpGetAttribute>();
        getAttr.Should().NotBeNull();
        getAttr!.Template.Should().Be("active", "Route template must be exactly 'active'");

        var obsoleteAttr = activeMethod.GetCustomAttribute<ObsoleteAttribute>();
        obsoleteAttr.Should().NotBeNull("Restored legacy endpoint must be marked Obsolete");
    }

    [Fact]
    public void FinanceV3Controller_AuthorizationPolicy_IsReportsAccess()
    {
        // Verify class-level policy on FinanceV3Controller is "ReportsAccess"
        // This policy restricts access to Admin and Accountant roles only.
        var classAuthorizeAttr = typeof(FinanceV3Controller).GetCustomAttribute<AuthorizeAttribute>();
        classAuthorizeAttr.Should().NotBeNull("FinanceV3Controller must specify an AuthorizeAttribute at class level");
        classAuthorizeAttr!.Policy.Should().Be("ReportsAccess",
            "Class-level authorization policy on FinanceV3Controller must be ReportsAccess to restrict access from Reception.");
    }
}
