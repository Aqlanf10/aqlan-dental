using System.Reflection;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.Interfaces.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Commissions;

/// <summary>
/// Route guard tests for CommissionsController.
///
/// Sprint 1.5 smoke audit finding (P1): GET /api/commissions returned 404.
/// Sprint 2 analysis conclusion: FALSE POSITIVE.
///
/// Evidence:
///   - CommissionsController already had [Route("api/commissions")] before Sprint 2.
///   - The root GET /api/commissions was tested with curl but does NOT exist by design —
///     there is no root GET action on this controller (all actions use sub-paths).
///   - A curl GET to a controller root with no root action correctly returns 404; this
///     is normal ASP.NET Core behaviour, not a bug. No fix required.
///   - Note: the Finance V3 commissions tab uses /api/finance-v3/doctor-commissions
///     (a separate controller, confirmed by grep on CommissionsTab.tsx).
///
/// Real production paths called by the frontend (grep: frontend/src/hooks/useCommissions.ts):
///   GET  /api/commissions/report?from=&to=&...        ← GetReport (main report page)
///   GET  /api/commissions/payments?...                ← GetPayments (payment history)
///   POST /api/commissions/payments                    ← RecordPayment
///   GET  /api/commissions/line-items/{id}             ← GetLineItem
///   GET  /api/commissions/invoices/{id}               ← GetInvoiceCommissions
///   PATCH /api/commissions/line-items/{id}/costs      ← UpdateCosts
///   POST  /api/commissions/line-items/{id}/approve    ← Approve
///   POST  /api/commissions/line-items/{id}/unlock     ← Unlock
///   POST  /api/commissions/line-items/{id}/auto-fill  ← AutoFill
///   Root GET /api/commissions — NOT called by frontend → audit finding was a false positive.
///
/// Sections:
///   A. Reflection tests — verify class route, auth policy, and sub-route attributes.
///   B. Integration-style tests — instantiate controller with mocked ICommissionService,
///      call the two most critical frontend actions, assert result is NOT NotFoundResult.
/// </summary>
public class CommissionsRouteGuardTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // A. Reflection tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CommissionsController_HasClassLevelRoute()
    {
        var route = typeof(CommissionsController)
            .GetCustomAttributes<RouteAttribute>()
            .SingleOrDefault();

        route.Should().NotBeNull(
            "CommissionsController must have a class-level [Route] attribute so all sub-paths " +
            "under /api/commissions return 401 (not 404) for unauthenticated requests");

        route!.Template.Should().Be("api/commissions",
            "the class route must match the path prefix the frontend uses for all commission calls");
    }

    [Fact]
    public void CommissionsController_RequiresCommissionViewPolicy()
    {
        var authorize = typeof(CommissionsController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull(
            "CommissionsController must be protected by [Authorize] so unauthenticated " +
            "requests return 401 before reaching commission data");

        authorize!.Policy.Should().Be("CommissionView",
            "commission data is role-restricted; only users with CommissionView policy may access it");
    }

    [Fact]
    public void CommissionsController_IsApiController()
    {
        typeof(CommissionsController)
            .GetCustomAttributes<ApiControllerAttribute>()
            .Should().ContainSingle(
                "CommissionsController must be decorated with [ApiController]");
    }

    [Theory]
    [InlineData("GetReport", typeof(HttpGetAttribute), "report")]
    [InlineData("GetPayments", typeof(HttpGetAttribute), "payments")]
    [InlineData("RecordPayment", typeof(HttpPostAttribute), "payments")]
    [InlineData("GetServiceDefaults", typeof(HttpGetAttribute), "services/{serviceId:guid}/defaults")]
    [InlineData("GetBackfillPreview", typeof(HttpGetAttribute), "backfill-preview")]
    public void CommissionsController_KeyActions_HaveExpectedHttpAttributes(
        string methodName, Type httpAttributeType, string expectedTemplate)
    {
        var method = typeof(CommissionsController).GetMethod(methodName);

        method.Should().NotBeNull(
            $"CommissionsController must contain action '{methodName}' " +
            $"— it serves requests to /api/commissions/{expectedTemplate}");

        var attribute = method!.GetCustomAttributes()
            .SingleOrDefault(a => a.GetType() == httpAttributeType);

        attribute.Should().NotBeNull(
            $"{methodName} must have [{httpAttributeType.Name}]");

        var template = attribute switch
        {
            HttpGetAttribute g  => g.Template,
            HttpPostAttribute p => p.Template,
            _                   => null
        };

        template.Should().Be(expectedTemplate,
            $"{methodName} must be reachable at /api/commissions/{expectedTemplate}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // B. Integration-style tests (direct controller invocation, mocked services)
    //    These verify the most critical frontend-facing actions can be invoked
    //    and do NOT return NotFoundResult. Pattern: FinanceV3IntegrationFixTests.cs.
    // ═══════════════════════════════════════════════════════════════════════════

    private static (ICommissionService service, Mock<ICommissionService> mock) BuildCommissionServiceMock()
    {
        var mock = new Mock<ICommissionService>();

        var emptySummary = new CommissionReportSummary(
            TotalGross: 0, TotalDiscount: 0, TotalMaterialCost: 0,
            TotalLabCost: 0, TotalOtherCosts: 0, TotalNet: 0,
            TotalDoctorCommission: 0, TotalPaid: 0, TotalRemaining: 0);

        mock.Setup(s => s.GetReportAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new CommissionReportResponse(emptySummary, []));

        mock.Setup(s => s.GetPaymentsAsync(It.IsAny<Guid?>()))
            .ReturnsAsync([]);

        return (mock.Object, mock);
    }

    private static ICurrentUserService BuildAdminUserMock()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.IsAdmin).Returns(true);
        mock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        mock.Setup(u => u.IsAuthenticated).Returns(true);
        return mock.Object;
    }

    [Fact]
    public async Task GetReport_WithValidDateRange_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/commissions/report?from=2026-01-01&to=2026-01-31 is reachable.
        // This is the primary endpoint of the /commissions page.
        var (service, _) = BuildCommissionServiceMock();
        var controller = new CommissionsController(service, BuildAdminUserMock());

        var result = await controller.GetReport(
            from: "2026-01-01", to: "2026-01-31",
            doctorId: null, branchId: null,
            serviceCategory: null, commissionStatus: null, paymentStatus: null);

        result.Should().NotBeOfType<NotFoundResult>(
            "GET /api/commissions/report must return data (200), not 404 — " +
            "this is the main entry point for the commissions report page");

        result.Should().BeOfType<OkObjectResult>(
            "a valid date range with no data still returns 200 OK with an empty report");
    }

    [Fact]
    public async Task GetReport_WithInvalidFromDate_ReturnsBadRequest_NotNotFound()
    {
        // Verifies: bad input produces 400, not 404. Route is still reachable.
        var (service, _) = BuildCommissionServiceMock();
        var controller = new CommissionsController(service, BuildAdminUserMock());

        var result = await controller.GetReport(
            from: "not-a-date", to: "2026-01-31",
            doctorId: null, branchId: null,
            serviceCategory: null, commissionStatus: null, paymentStatus: null);

        result.Should().NotBeOfType<NotFoundResult>(
            "an invalid date must return 400 (bad request), never 404 (not found)");

        result.Should().BeOfType<BadRequestObjectResult>(
            "the controller must validate the date and return a 400 with an Arabic error message");
    }

    [Fact]
    public async Task GetPayments_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/commissions/payments is reachable (200, not 404).
        // Called by the commissions page to show the payment disbursement history.
        var (service, _) = BuildCommissionServiceMock();
        var controller = new CommissionsController(service, BuildAdminUserMock());

        var result = await controller.GetPayments(doctorId: null);

        result.Should().NotBeOfType<NotFoundResult>(
            "GET /api/commissions/payments must return 200 (not 404)");

        result.Should().BeOfType<OkObjectResult>(
            "an empty payments list is still a valid 200 OK response");
    }
}
