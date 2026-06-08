using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.DailyOperations;

/// <summary>
/// Route guard and integration tests for DailyOperationsController.
/// Verifies correct attribute routing, authorization policy, and basic endpoint behavior.
/// </summary>
public class DailyOperationsRouteGuardTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Section A: Reflection-only route/attribute checks
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DailyOperationsController_HasRouteAttribute_WithApiDailyOperations()
    {
        var attr = typeof(DailyOperationsController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("DailyOperationsController must have [Route] attribute");
        attr!.Template.Should().Be("api/daily-operations",
            "Route template must be 'api/daily-operations' for consistent routing");
    }

    [Fact]
    public void DailyOperationsController_HasAuthorizePolicy_StaffOnly()
    {
        var attr = typeof(DailyOperationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("DailyOperationsController must have [Authorize] attribute");
        attr!.Policy.Should().Be("StaffOnly",
            "Authorization policy must be 'StaffOnly'");
    }

    [Fact]
    public void GetDailyReport_HttpGetAttribute_IsRelativePath()
    {
        var method = typeof(DailyOperationsController).GetMethod("GetDailyReport");
        method.Should().NotBeNull("GetDailyReport method must exist");

        var httpGetAttr = method!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>()
            .FirstOrDefault();

        httpGetAttr.Should().NotBeNull("GetDailyReport must have [HttpGet] attribute");
        httpGetAttr!.Template.Should().Be("report",
            "HttpGet template must be relative 'report', not absolute '/api/daily-operations/report'");
    }

    [Fact]
    public void GetDailyReport_RequiresFinanceAccess_NotJustStaffOnly()
    {
        var method = typeof(DailyOperationsController).GetMethod("GetDailyReport");
        method.Should().NotBeNull("GetDailyReport method must exist");

        var authorize = method!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault(a => a.Policy == "FinanceAccess");

        authorize.Should().NotBeNull(
            "the daily report includes clinic financial totals and must not be visible to doctor-only StaffOnly users");
    }

    [Fact]
    public void DailyOperationsController_HasApiControllerAttribute()
    {
        var attr = typeof(DailyOperationsController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), false)
            .FirstOrDefault();

        attr.Should().NotBeNull("DailyOperationsController must have [ApiController] attribute for automatic model validation");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Section B: Integration-style tests (InMemory DB)
    // ═══════════════════════════════════════════════════════════════════════════

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DailyOperationsController BuildController(AppDbContext db)
    {
        var logger = new Mock<ILogger<DailyOperationsController>>().Object;
        return new DailyOperationsController(db, logger);
    }

    [Fact]
    public async Task GetDailyReport_NullDate_ReturnsOk_NotNotFound()
    {
        await using var db = CreateDb();

        // Seed minimal data so queries don't blow up
        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "Test Branch" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetDailyReport(null);

        result.Should().BeOfType<OkObjectResult>(
            "GetDailyReport with null date (defaults to today) should return 200 OK, NOT 404 NotFound");
    }

    [Fact]
    public async Task GetDailyReport_ValidDate_ReturnsOk_NotNotFound()
    {
        await using var db = CreateDb();

        var branchId = Guid.NewGuid();
        db.Branches.Add(new Branch { Id = branchId, Name = "Test Branch" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var result = await controller.GetDailyReport("2026-01-15");

        result.Should().BeOfType<OkObjectResult>(
            "GetDailyReport with a valid date should return 200 OK, NOT 404 NotFound");
    }
}
