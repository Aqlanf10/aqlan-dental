using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Messages;

public class PatientConversationAccessRevocationTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static (
        MessagesController Controller,
        Mock<IMessagingService> Messaging,
        Mock<ICurrentUserService> CurrentUser,
        Mock<IPatientAccessService> PatientAccess)
        BuildController(
            AppDbContext db,
            Guid userId,
            bool isAdmin,
            Guid? branchId,
            bool isDoctor = false,
            bool canAccessPatient = true)
    {
        var messaging = new Mock<IMessagingService>();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);
        currentUser.SetupGet(u => u.UserId).Returns(userId);
        currentUser.SetupGet(u => u.IsAdmin).Returns(isAdmin);
        currentUser.SetupGet(u => u.BranchId).Returns(branchId);

        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(p => p.IsDoctor).Returns(isDoctor);
        patientAccess
            .Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>()))
            .ReturnsAsync(canAccessPatient);

        var controller = new MessagesController(
            messaging.Object,
            db,
            currentUser.Object,
            patientAccess.Object,
            Mock.Of<ILogger<MessagesController>>());

        return (controller, messaging, currentUser, patientAccess);
    }

    [Fact]
    public async Task GetConversation_CrossBranchStaleParticipant_Returns404_AndDeactivatesMembership()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var patientBranch = Guid.NewGuid();
        var userBranch = Guid.NewGuid();
        var patient = new Patient
        {
            PatientNumber = "MSG-REV-001",
            FirstName = "أحمد",
            LastName = "محمد",
            BranchId = patientBranch
        };
        var conversation = new Conversation
        {
            Title = "المريض: أحمد محمد",
            ConversationType = "StaffToPatient",
            IsGroup = true,
            PatientId = patient.Id,
            BranchId = patientBranch
        };
        var participant = new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = userId,
            IsAdmin = false
        };

        db.Patients.Add(patient);
        db.Conversations.Add(conversation);
        db.ConversationParticipants.Add(participant);
        await db.SaveChangesAsync();

        var (controller, messaging, _, _) = BuildController(
            db,
            userId,
            isAdmin: false,
            branchId: userBranch);

        var result = await controller.GetConversation(conversation.Id);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        messaging.Verify(
            m => m.GetConversationAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);

        var stored = await db.ConversationParticipants
            .IgnoreQueryFilters()
            .SingleAsync(cp => cp.Id == participant.Id);
        stored.IsActive.Should().BeFalse();
        stored.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMessage_UnlinkedDoctorStaleParticipant_Returns404_AndDoesNotSend()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var patient = new Patient
        {
            PatientNumber = "MSG-REV-002",
            FirstName = "سارة",
            LastName = "علي",
            BranchId = branchId
        };
        var conversation = new Conversation
        {
            Title = "حول المريض",
            ConversationType = "StaffToPatient",
            IsGroup = true,
            PatientId = patient.Id,
            BranchId = branchId
        };
        var participant = new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = userId,
            IsAdmin = false
        };

        db.Patients.Add(patient);
        db.Conversations.Add(conversation);
        db.ConversationParticipants.Add(participant);
        await db.SaveChangesAsync();

        var (controller, messaging, _, patientAccess) = BuildController(
            db,
            userId,
            isAdmin: false,
            branchId: branchId,
            isDoctor: true,
            canAccessPatient: false);

        var result = await controller.SendMessage(
            conversation.Id,
            new SendMessageRequest { Content = "لا يجب إرسالها" });

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        patientAccess.Verify(p => p.CanAccessPatientAsync(patient.Id), Times.Once);
        messaging.Verify(
            m => m.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<SendMessageRequest>()),
            Times.Never);

        var stored = await db.ConversationParticipants
            .IgnoreQueryFilters()
            .SingleAsync(cp => cp.Id == participant.Id);
        stored.IsActive.Should().BeFalse();
        stored.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetConversations_PrunesInaccessiblePatientMembership_BeforeListing()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        var patientBranch = Guid.NewGuid();
        var userBranch = Guid.NewGuid();
        var patient = new Patient
        {
            PatientNumber = "MSG-REV-003",
            FirstName = "خالد",
            LastName = "ناصر",
            BranchId = patientBranch
        };
        var conversation = new Conversation
        {
            Title = "المريض: خالد ناصر",
            ConversationType = "StaffToPatient",
            IsGroup = true,
            PatientId = patient.Id,
            BranchId = patientBranch
        };
        var participant = new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = userId,
            IsAdmin = false
        };

        db.Patients.Add(patient);
        db.Conversations.Add(conversation);
        db.ConversationParticipants.Add(participant);
        await db.SaveChangesAsync();

        var (controller, messaging, _, _) = BuildController(
            db,
            userId,
            isAdmin: false,
            branchId: userBranch);

        messaging
            .Setup(m => m.GetMyConversationsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>()))
            .ReturnsAsync(new PaginatedResponse<ConversationListDto>
            {
                Data = [],
                TotalCount = 0,
                Page = 1,
                PageSize = 20
            });

        var result = await controller.GetConversations();

        result.Result.Should().BeOfType<OkObjectResult>();
        messaging.Verify(
            m => m.GetMyConversationsAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool?>()),
            Times.Once);

        var stored = await db.ConversationParticipants
            .IgnoreQueryFilters()
            .SingleAsync(cp => cp.Id == participant.Id);
        stored.IsActive.Should().BeFalse();
        stored.DeletedAt.Should().NotBeNull();
    }
}
