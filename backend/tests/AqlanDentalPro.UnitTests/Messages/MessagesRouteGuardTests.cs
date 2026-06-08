using System.Reflection;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Messages;

/// <summary>
/// Route guard tests for MessagesController.
///
/// Sprint 1.5 smoke audit finding (P1): GET /api/messages returned 404.
/// Sprint 2 analysis conclusion: FALSE POSITIVE.
///
/// Evidence:
///   - MessagesController already had [Route("api/messages")] before Sprint 2.
///   - The root GET /api/messages was tested with curl but does NOT exist by design —
///     there is no root GET action on this controller (all actions use sub-paths).
///   - A curl GET to a controller root with no root action correctly returns 404; this
///     is normal ASP.NET Core behaviour, not a bug. No fix required.
///
/// Real production paths called by the frontend (grep: frontend/src/hooks/useMessaging.ts):
///   GET  /api/messages/conversations        ← GetConversations (paginated list)
///   GET  /api/messages/conversations/{id}   ← GetConversation (detail + messages)
///   POST /api/messages/conversations        ← CreateConversation
///   POST /api/messages/conversations/{id}/messages  ← SendMessage
///   GET  /api/messages/unread-count         ← GetUnreadCount (sidebar badge)
///   GET  /api/messages/stats                ← GetStats (dashboard panel)
///   DELETE /api/messages/conversations/{id}/messages/{msgId} ← DeleteMessage
///   POST /api/messages/conversations/{id}/read  ← MarkAsRead
///   GET  /api/messages/conversations/patient/{patientId} ← GetOrCreatePatientFacingConversation
///   GET  /api/messages/internal-patient/{patientId}     ← GetOrCreateInternalPatientConversation
///   Root GET /api/messages — NOT called by frontend → audit finding was a false positive.
///
/// Sections:
///   A. Reflection tests — verify class route, auth policy, and sub-route attributes.
///   B. Integration-style tests — instantiate controller with mocked IMessagingService,
///      call the three highest-traffic frontend actions, assert result is NOT NotFoundResult.
/// </summary>
public class MessagesRouteGuardTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // A. Reflection tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MessagesController_HasClassLevelRoute()
    {
        var route = typeof(MessagesController)
            .GetCustomAttributes<RouteAttribute>()
            .SingleOrDefault();

        route.Should().NotBeNull(
            "MessagesController must have a class-level [Route] attribute so all sub-paths " +
            "under /api/messages return 401 (not 404) for unauthenticated requests");

        route!.Template.Should().Be("api/messages",
            "the class route must match the path prefix the frontend uses for all messaging calls");
    }

    [Fact]
    public void MessagesController_RequiresStaffOnlyPolicy()
    {
        var authorize = typeof(MessagesController)
            .GetCustomAttributes<AuthorizeAttribute>()
            .SingleOrDefault();

        authorize.Should().NotBeNull(
            "MessagesController must be protected by [Authorize] so unauthenticated " +
            "requests return 401 rather than reaching the messaging service");

        authorize!.Policy.Should().Be("StaffOnly",
            "only authenticated clinic staff should send or read internal messages");
    }

    [Fact]
    public void MessagesController_IsApiController()
    {
        typeof(MessagesController)
            .GetCustomAttributes<ApiControllerAttribute>()
            .Should().ContainSingle(
                "MessagesController must be decorated with [ApiController]");
    }

    [Theory]
    [InlineData("GetConversations", typeof(HttpGetAttribute), "conversations")]
    [InlineData("GetUnreadCount", typeof(HttpGetAttribute), "unread-count")]
    [InlineData("GetStats", typeof(HttpGetAttribute), "stats")]
    [InlineData("CreateConversation", typeof(HttpPostAttribute), "conversations")]
    public void MessagesController_KeyActions_HaveExpectedHttpAttributes(
        string methodName, Type httpAttributeType, string expectedTemplate)
    {
        var method = typeof(MessagesController).GetMethod(methodName);

        method.Should().NotBeNull(
            $"MessagesController must contain action '{methodName}' " +
            $"— it is called by the frontend at /api/messages/{expectedTemplate}");

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
            $"{methodName} must be reachable at /api/messages/{expectedTemplate}");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // B. Integration-style tests (direct controller invocation, mocked service)
    //    These verify the highest-traffic frontend actions can be invoked and do
    //    NOT return NotFoundResult. Pattern: FinanceV3IntegrationFixTests.cs.
    // ═══════════════════════════════════════════════════════════════════════════

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MessagesController BuildController(IMessagingService messagingService, AppDbContext db)
    {
        var logger = new Mock<ILogger<MessagesController>>().Object;
        return new MessagesController(messagingService, db, logger);
    }

    private static IMessagingService BuildMessagingServiceMock()
    {
        var mock = new Mock<IMessagingService>();

        mock.Setup(s => s.GetMyConversationsAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PaginatedResponse<ConversationListDto>
            {
                Data = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        mock.Setup(s => s.GetUnreadCountAsync())
            .ReturnsAsync(new UnreadCountDto { TotalUnread = 0 });

        mock.Setup(s => s.GetStatsAsync())
            .ReturnsAsync(new MessagingStatsDto
            {
                TotalConversations = 0,
                ActiveConversations = 0,
                MessagesToday = 0,
                MessagesThisWeek = 0
            });

        return mock.Object;
    }

    [Fact]
    public async Task GetConversations_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/messages/conversations is reachable (200, not 404).
        // This is the first call made when the messages page loads.
        await using var db = CreateDb();
        var controller = BuildController(BuildMessagingServiceMock(), db);

        var result = await controller.GetConversations();

        result.Should().NotBeOfType<NotFoundResult>(
            "GET /api/messages/conversations must return data (200), not 404");
        result.Result.Should().BeOfType<OkObjectResult>(
            "an empty conversation list is still a valid 200 OK response");
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/messages/unread-count is reachable (200, not 404).
        // This endpoint drives the unread-count badge in the staff sidebar;
        // it is polled on every page load. A 404 here would silently break the badge.
        await using var db = CreateDb();
        var controller = BuildController(BuildMessagingServiceMock(), db);

        var result = await controller.GetUnreadCount();

        result.Should().NotBeOfType<NotFoundResult>(
            "GET /api/messages/unread-count must return 200 (not 404) — " +
            "this drives the sidebar unread badge and is polled on every staff page load");
        result.Result.Should().BeOfType<OkObjectResult>(
            "unread count of 0 is still a valid response");
    }

    [Fact]
    public async Task GetStats_ReturnsOk_NotNotFound()
    {
        // Verifies: GET /api/messages/stats is reachable (200, not 404).
        // Called by the messages dashboard stats panel.
        await using var db = CreateDb();
        var controller = BuildController(BuildMessagingServiceMock(), db);

        var result = await controller.GetStats();

        result.Should().NotBeOfType<NotFoundResult>(
            "GET /api/messages/stats must return 200 (not 404)");
        result.Result.Should().BeOfType<OkObjectResult>(
            "an all-zero stats object is still a valid response");
    }
}
