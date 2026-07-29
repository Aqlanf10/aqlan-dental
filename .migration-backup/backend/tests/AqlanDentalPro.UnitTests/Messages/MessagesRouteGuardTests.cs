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
/// Route guard and integration tests for MessagesController.
/// Verifies correct attribute routing, authorization policy, and basic endpoint behavior.
/// </summary>
public class MessagesRouteGuardTests
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Section A: Reflection-only route/attribute checks
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MessagesController_HasRouteAttribute_WithApiMessages()
    {
        var attr = typeof(MessagesController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("MessagesController must have [Route] attribute");
        attr!.Template.Should().Be("api/messages",
            "Route template must be 'api/messages' for consistent routing");
    }

    [Fact]
    public void MessagesController_HasAuthorizePolicy_StaffOnly()
    {
        var attr = typeof(MessagesController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        attr.Should().NotBeNull("MessagesController must have [Authorize] attribute");
        attr!.Policy.Should().Be("StaffOnly",
            "Authorization policy must be 'StaffOnly'");
    }

    [Fact]
    public void MessagesController_HasApiControllerAttribute()
    {
        var attr = typeof(MessagesController)
            .GetCustomAttributes(typeof(ApiControllerAttribute), false)
            .FirstOrDefault();

        attr.Should().NotBeNull("MessagesController must have [ApiController] attribute");
    }

    [Fact]
    public void MessagesController_KeySubRoutes_Exist()
    {
        var type = typeof(MessagesController);

        // GET conversations
        var getConversations = type.GetMethod("GetConversations");
        getConversations.Should().NotBeNull("GET conversations endpoint must exist");
        getConversations!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("conversations");

        // POST conversations
        var createConversation = type.GetMethod("CreateConversation");
        createConversation.Should().NotBeNull("POST conversations endpoint must exist");

        // GET unread-count
        var getUnreadCount = type.GetMethod("GetUnreadCount");
        getUnreadCount.Should().NotBeNull("GET unread-count endpoint must exist");
        getUnreadCount!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("unread-count");

        // GET stats
        var getStats = type.GetMethod("GetStats");
        getStats.Should().NotBeNull("GET stats endpoint must exist");
        getStats!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("stats");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Section B: Integration-style tests (InMemory DB + mocked IMessagingService)
    // ═══════════════════════════════════════════════════════════════════════════

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (MessagesController controller, Mock<IMessagingService> messagingMock) BuildController(AppDbContext db)
    {
        var messagingMock = new Mock<IMessagingService>();
        var logger = new Mock<ILogger<MessagesController>>().Object;
        var controller = new MessagesController(messagingMock.Object, db, logger);
        return (controller, messagingMock);
    }

    [Fact]
    public async Task GetConversations_ReturnsOk_NotNotFound()
    {
        await using var db = CreateDb();
        var (controller, messagingMock) = BuildController(db);

        messagingMock
            .Setup(m => m.GetMyConversationsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>()))
            .ReturnsAsync(new PaginatedResponse<ConversationListDto>
            {
                Data = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        var result = await controller.GetConversations();

        result.Result.Should().BeOfType<OkObjectResult>(
            "GetConversations should return 200 OK with data, NOT 404 NotFound");
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk_NotNotFound()
    {
        await using var db = CreateDb();
        var (controller, messagingMock) = BuildController(db);

        messagingMock
            .Setup(m => m.GetUnreadCountAsync())
            .ReturnsAsync(new UnreadCountDto { TotalUnread = 5, UnreadConversations = 2 });

        var result = await controller.GetUnreadCount();

        result.Result.Should().BeOfType<OkObjectResult>(
            "GetUnreadCount (sidebar badge) should return 200 OK, NOT 404 NotFound");
    }

    [Fact]
    public async Task GetStats_ReturnsOk_NotNotFound()
    {
        await using var db = CreateDb();
        var (controller, messagingMock) = BuildController(db);

        messagingMock
            .Setup(m => m.GetStatsAsync())
            .ReturnsAsync(new MessagingStatsDto
            {
                TotalConversations = 10,
                ActiveConversations = 3,
                MessagesToday = 5,
                MessagesThisWeek = 25,
                StaffToStaffConversations = 8,
                StaffToPatientConversations = 2,
                PatientFacingConversations = 0
            });

        var result = await controller.GetStats();

        result.Result.Should().BeOfType<OkObjectResult>(
            "GetStats (dashboard stats) should return 200 OK, NOT 404 NotFound");
    }
}
