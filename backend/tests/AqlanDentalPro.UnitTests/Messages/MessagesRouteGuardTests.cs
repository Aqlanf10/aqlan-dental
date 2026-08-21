using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
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
/// Verifies correct attribute routing, authorization policy, basic endpoint behavior,
/// and MOBILE-03 patient/branch ownership guards.
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

        var getConversations = type.GetMethod("GetConversations");
        getConversations.Should().NotBeNull("GET conversations endpoint must exist");
        getConversations!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("conversations");

        var createConversation = type.GetMethod("CreateConversation");
        createConversation.Should().NotBeNull("POST conversations endpoint must exist");

        var getUnreadCount = type.GetMethod("GetUnreadCount");
        getUnreadCount.Should().NotBeNull("GET unread-count endpoint must exist");
        getUnreadCount!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("unread-count");

        var getStats = type.GetMethod("GetStats");
        getStats.Should().NotBeNull("GET stats endpoint must exist");
        getStats!.GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>().First().Template.Should().Be("stats");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Section B: Integration-style tests (InMemory DB + mocked services)
    // ═══════════════════════════════════════════════════════════════════════════

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (
        MessagesController controller,
        Mock<IMessagingService> messagingMock,
        Mock<ICurrentUserService> currentUserMock,
        Mock<IPatientAccessService> patientAccessMock)
        BuildController(
            AppDbContext db,
            bool isAdmin = true,
            Guid? branchId = null,
            bool isDoctor = false,
            bool canAccessPatient = true)
    {
        var messagingMock = new Mock<IMessagingService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.IsAuthenticated).Returns(true);
        currentUser.Setup(u => u.IsAdmin).Returns(isAdmin);
        currentUser.Setup(u => u.BranchId).Returns(branchId);
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.Setup(p => p.IsDoctor).Returns(isDoctor);
        patientAccess.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(canAccessPatient);

        var logger = new Mock<ILogger<MessagesController>>().Object;
        var controller = new MessagesController(
            messagingMock.Object,
            db,
            currentUser.Object,
            patientAccess.Object,
            logger);

        return (controller, messagingMock, currentUser, patientAccess);
    }

    [Fact]
    public async Task GetConversations_ReturnsOk_NotNotFound()
    {
        await using var db = CreateDb();
        var (controller, messagingMock, _, _) = BuildController(db);

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
        var (controller, messagingMock, _, _) = BuildController(db);

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
        var (controller, messagingMock, _, _) = BuildController(db);

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

    // ═══════════════════════════════════════════════════════════════════════════
    // Section C: MOBILE-03 patient-linked messaging ownership guards
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PatientFacingConversation_CrossBranchStaff_Returns404_WithoutCallingService()
    {
        await using var db = CreateDb();
        var patientBranch = Guid.NewGuid();
        var userBranch = Guid.NewGuid();
        var patient = new Patient
        {
            PatientNumber = "GM-2026-MSG-01",
            FirstName = "أحمد",
            LastName = "محمد",
            BranchId = patientBranch
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var (controller, messagingMock, _, _) = BuildController(
            db,
            isAdmin: false,
            branchId: userBranch,
            isDoctor: false);

        var result = await controller.GetOrCreatePatientFacingConversation(patient.Id);

        result.Result.Should().BeOfType<NotFoundObjectResult>(
            "cross-branch patient IDs must not reveal or create conversations");
        messagingMock.Verify(
            m => m.GetOrCreatePatientFacingConversationAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task InternalPatientConversation_UnlinkedDoctor_Returns404_WithoutCallingService()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        var patient = new Patient
        {
            PatientNumber = "GM-2026-MSG-02",
            FirstName = "سارة",
            LastName = "علي",
            BranchId = branchId
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var (controller, messagingMock, _, patientAccess) = BuildController(
            db,
            isAdmin: false,
            branchId: branchId,
            isDoctor: true,
            canAccessPatient: false);

        var result = await controller.GetOrCreateInternalPatientConversation(patient.Id);

        result.Result.Should().BeOfType<NotFoundObjectResult>(
            "a doctor must not create a conversation for an unlinked patient");
        patientAccess.Verify(p => p.CanAccessPatientAsync(patient.Id), Times.Once);
        messagingMock.Verify(
            m => m.GetOrCreatePatientConversationAsync(It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task GenericPatientConversation_CrossBranchStaff_Returns404_WithoutCallingService()
    {
        await using var db = CreateDb();
        var patient = new Patient
        {
            PatientNumber = "GM-2026-MSG-03",
            FirstName = "خالد",
            LastName = "ناصر",
            BranchId = Guid.NewGuid()
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var (controller, messagingMock, _, _) = BuildController(
            db,
            isAdmin: false,
            branchId: Guid.NewGuid(),
            isDoctor: false);

        var request = new CreateConversationRequest
        {
            Title = "حول المريض",
            IsGroup = true,
            ConversationType = "StaffToPatient",
            PatientId = patient.Id
        };

        var result = await controller.CreateConversation(request);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        messagingMock.Verify(m => m.CreateConversationAsync(It.IsAny<CreateConversationRequest>()), Times.Never);
    }

    [Fact]
    public async Task PatientFacingConversation_SameBranchAuthorizedDoctor_CallsService()
    {
        await using var db = CreateDb();
        var branchId = Guid.NewGuid();
        var patient = new Patient
        {
            PatientNumber = "GM-2026-MSG-04",
            FirstName = "منى",
            LastName = "سالم",
            BranchId = branchId
        };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var (controller, messagingMock, _, patientAccess) = BuildController(
            db,
            isAdmin: false,
            branchId: branchId,
            isDoctor: true,
            canAccessPatient: true);

        messagingMock
            .Setup(m => m.GetOrCreatePatientFacingConversationAsync(patient.Id))
            .ReturnsAsync(new ConversationDetailDto { Id = Guid.NewGuid(), PatientId = patient.Id });

        var result = await controller.GetOrCreatePatientFacingConversation(patient.Id);

        result.Result.Should().BeOfType<OkObjectResult>();
        patientAccess.Verify(p => p.CanAccessPatientAsync(patient.Id), Times.Once);
        messagingMock.Verify(m => m.GetOrCreatePatientFacingConversationAsync(patient.Id), Times.Once);
    }
}
