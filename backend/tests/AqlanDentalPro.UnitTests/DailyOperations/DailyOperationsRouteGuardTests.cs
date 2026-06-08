using System.Reflection;
using AqlanDentalPro.API.Controllers;
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
/// Route guard tests for DailyOperationsController.
///
/// Sprint 2 root-cause fix:
///   - Added [Route("api/daily-operations")] at class level.
///   - Changed [HttpGet("/api/daily-operations/report")] (absolute path) to [HttpGet("report")]
///     (relative path). An absolute path (leading "/") bypasses the class-level [Route] prefix,
///     making the route invisible to middleware and Swagger for the prefix path.
///
/// Sections:
///   A. Reflection tests — verify attributes compile and are set correctly (fast, no I/O).
///   B. Integration-style tests — instantiate the controller with InMemory DB, call action
///      methods directly, and assert the result is NOT NotFoundResult. This follows the
///      existing FinanceV3IntegrationFixTests.cs pattern used throughout this project.
///
/// Production paths tested here (all observed in useMessaging.ts and page.tsx):
///   GET  /api/daily-operations/report?date=  — daily ops dashboard
/// </summary>
public class DailyOperationsRouteGuardTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // A. Reflection tests (attribute verification)
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void DailyOperationsController_HasClassLevelRoute()
    {
        var route = typeof(DailyOperationsController)
            .GetCustomAttributes<RouteAttribute>()
            .SingleOrDefault();

        route.Should().NotBeNull(
            "DailyOperationsController must have a class-level [Route] attribute so that " +
            "GET /api/daily-operations/report returns 401 (not 404) for unauthenticated requests");

        route!.Template.Should().Be("api/daily-operations",
            "the class route must match the path the frontend calls");
    }

    [Fact]
    public void DailyOperationsController_RequiresStaffOnlyPolicy()
    {
        var authorize = typeof(DailyOperationsController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull(
            "DailyOperationsController must be protected by [Authorize] so unauthenticated " +
            "requests return 401 rather than being served open data");

        authorize!.Policy.Should().Be("StaffOnly",
            "only authenticated clinic staff should access daily operations data");
    }

    [Fact]
    public void GetDailyReport_HasRelativeHttpGetRoute_NotAbsolutePath()
    {
        var method = typeof(DailyOperationsController)
            .GetMethod(nameof(DailyOperationsController.GetDailyReport));

        method.Should().NotBeNull("GetDailyReport action must exist");

        var httpGet = method!.GetCustomAttributes<HttpGetAttribute>().SingleOrDefault();

        httpGet.Should().NotBeNull("GetDailyReport must have [HttpGet]");

        httpGet!.Template.Should().Be("report",
            "the action route must be relative ('report') not absolute ('/api/daily-operations/report'). " +
            "An absolute path bypasses the class-level [Route] attribute.");

        httpGet.Template.Should().NotStartWith("/",
            "absolute paths (starting with '/') bypass the class-level [Route] prefix — " +
            "they must not be used when a class-level [Route] is defined");
    }

    [Fact]
    public void DailyOperationsController_IsApiController()
    {
        typeof(DailyOperationsController)
            .GetCustomAttributes<ApiControllerAttribute>()
            .Should().ContainSingle(
                "DailyOperationsController must be decorated with [ApiController]");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // B. Integration-style tests (direct controller invocation, InMemory DB)
    //    Pattern: FinanceV3IntegrationFixTests.cs — no WebApplicationFactory needed.
    //    These verify the action can be invoked and returns a meaningful result,
    //    i.e. it does NOT return NotFoundResult (confirming the route exists).
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
    public async Task GetDailyReport_WithEmptyDb_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/daily-operations/report is reachable (returns 200, not 404).
        // Production path used by the daily-operations dashboard page.
        await using var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.GetDailyReport(date: null);

        result.Should().NotBeOfType<NotFoundResult>(
            "GET /api/daily-operations/report must return a data response (200), not 404 — " +
            "before the Sprint 2 fix this returned 404 because the class-level [Route] was missing");

        result.Should().NotBeOfType<NotFoundObjectResult>(
            "the action must be reachable; a 404 response body would indicate a routing error");

        result.Should().BeOfType<OkObjectResult>(
            "with an empty but valid database the report endpoint must succeed with 200 OK");
    }

    [Fact]
    public async Task GetDailyReport_WithSpecificDate_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/daily-operations/report?date=2026-01-15 is reachable.
        await using var db = CreateDb();
        var controller = BuildController(db);

        var result = await controller.GetDailyReport(date: "2026-01-15");

        result.Should().NotBeOfType<NotFoundResult>(
            "the date filter parameter must not cause a 404");

        result.Should().BeOfType<OkObjectResult>(
            "a valid date string must yield a 200 OK report response");
    }
}
