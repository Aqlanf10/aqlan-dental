using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Commission;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.Interfaces.Services;
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

namespace AqlanDentalPro.UnitTests.Commissions;

/// <summary>
/// Route guard and integration tests for CommissionsController.
/// Verifies correct attribute routing, authorization policy, and basic endpoint behavior.
/// </summary>
public class CommissionsRouteGuardTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Section A: Reflection-only route/attribute checks
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CommissionsController_HasRouteAttribute_WithApiCommissions()
    {
        var attr = typeof(CommissionsController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("CommissionsController must have [Route] attribute");
        attr!.Template.Should().Be("api/commissions",
            "Route template must be 'api/commissions' for consistent routing");
    }

    [Fact]
    public void CommissionsController_HasAuthorizePolicy_CommissionView()
    {
        var attr = typeof(CommissionsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("CommissionsController must have [Authorize] attribute");
        attr!.Policy.Should().Be("CommissionView",
            "Authorization policy must be 'CommissionView'");
    }

    [Fact]
    public void CommissionsController_HasApiControllerAttribute()
    {
        var attr = typeof(CommissionsController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), false)
            .FirstOrDefault();

        attr.Should().NotBeNull("CommissionsController must have [ApiController] attribute");
    }

    [Fact]
    public void CommissionsController_KeySubRoutes_Exist()
    {
        var type = typeof(CommissionsController);

        // GET report
        var getReport = type.GetMethod("GetReport");
        getReport.Should().NotBeNull("GET report endpoint must exist");
        getReport!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("report");

        // GET payments
        var getPayments = type.GetMethod("GetPayments");
        getPayments.Should().NotBeNull("GET payments endpoint must exist");

        // POST payments (RecordPayment)
        var recordPayment = type.GetMethod("RecordPayment");
        recordPayment.Should().NotBeNull("POST payments endpoint must exist");

        // GET services/{id}/defaults (service defaults)
        var getServiceDefaults = type.GetMethod("GetServiceDefaults");
        getServiceDefaults.Should().NotBeNull("GET services/{id}/defaults endpoint must exist");

        // GET backfill-preview
        var getBackfillPreview = type.GetMethod("GetBackfillPreview");
        getBackfillPreview.Should().NotBeNull("GET backfill-preview endpoint must exist");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Section B: Integration-style tests (mocked ICommissionService)
    // ═══════════════════════════════════════════════════════════════════════════

    private static (CommissionsController controller, Mock<ICommissionService> commissionMock, Mock<ICurrentUserService> currentUserMock) BuildController()
    {
        var commissionMock = new Mock<ICommissionService>();
        var currentUserMock = new Mock<ICurrentUserService>();
        currentUserMock.Setup(u => u.UserId).Returns(Guid.NewGuid());
        currentUserMock.Setup(u => u.IsAdmin).Returns(true);
        currentUserMock.Setup(u => u.Role).Returns(UserRole.Admin);
        currentUserMock.Setup(u => u.IsAuthenticated).Returns(true);

        // FIN-PERM: CommissionsController now takes AppDbContext first (for PermissionGuard).
        // This test uses an Admin user (above), so the granular guard always bypasses;
        // an empty in-memory DB is sufficient — no RolePermission seeding required.
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        var controller = new CommissionsController(db, commissionMock.Object, currentUserMock.Object);
        return (controller, commissionMock, currentUserMock);
    }

    [Fact]
    public async Task GetReport_ValidDates_ReturnsOk_NotNotFound()
    {
        var (controller, commissionMock, _) = BuildController();

        commissionMock
            .Setup(c => c.GetReportAsync(
                It.IsAny<DateOnly>(), It.IsAny<DateOnly>(),
                It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CommissionReportResponse(
                new CommissionReportSummary(0, 0, 0, 0, 0, 0, 0, 0, 0),
                []));

        var result = await controller.GetReport("2026-01-01", "2026-01-31", null, null, null, null, null);

        result.Should().BeOfType<OkObjectResult>(
            "GetReport with valid dates should return 200 OK, NOT 404 NotFound");
    }

    [Fact]
    public async Task GetReport_InvalidDate_ReturnsBadRequest_NotNotFound()
    {
        var (controller, _, _) = BuildController();

        var result = await controller.GetReport("not-a-date", "2026-01-31", null, null, null, null, null);

        result.Should().BeOfType<BadRequestObjectResult>(
            "GetReport with invalid date should return 400 BadRequest, NOT 404 NotFound");
    }

    [Fact]
    public async Task GetPayments_NullDoctorId_ReturnsOk_NotNotFound()
    {
        var (controller, commissionMock, _) = BuildController();

        commissionMock
            .Setup(c => c.GetPaymentsAsync(It.IsAny<Guid?>()))
            .ReturnsAsync([]);

        var result = await controller.GetPayments(null);

        result.Should().BeOfType<OkObjectResult>(
            "GetPayments should return 200 OK with data, NOT 404 NotFound");
    }
}
