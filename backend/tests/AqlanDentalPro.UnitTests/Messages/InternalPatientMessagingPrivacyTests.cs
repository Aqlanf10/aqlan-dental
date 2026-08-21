using AqlanDentalPro.API.Controllers;
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

public class InternalPatientMessagingPrivacyTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SendMessage_InternalPatientConversation_DeactivatesPatientParticipantBeforeSend()
    {
        await using var db = CreateDb();

        var staffUserId = Guid.NewGuid();
        var linkedPatientUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = "MSG-PRIV-001",
            FirstName = "أحمد",
            LastName = "محمد"
        });

        db.PatientAccounts.Add(new PatientAccount
        {
            PatientId = patientId,
            PhoneNumber = "770000000",
            LinkedUserId = linkedPatientUserId,
            IsVerified = true,
            PortalAccountActive = true
        });

        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            Title = "المريض: أحمد محمد",
            IsGroup = true,
            ConversationType = "StaffToPatient",
            PatientId = patientId,
            CreatedBy = staffUserId
        });

        db.ConversationParticipants.AddRange(
            new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = staffUserId,
                IsAdmin = true
            },
            new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = linkedPatientUserId,
                IsAdmin = false
            });

        await db.SaveChangesAsync();

        var messaging = new Mock<IMessagingService>();
        messaging
            .Setup(m => m.SendMessageAsync(conversationId, It.IsAny<SendMessageRequest>()))
            .ReturnsAsync(new MessageDto
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = staffUserId,
                SenderName = "Staff",
                Content = "ملاحظة داخلية",
                CreatedAt = DateTime.UtcNow
            });

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(staffUserId);
        currentUser.SetupGet(u => u.IsAdmin).Returns(true);
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);

        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(p => p.IsDoctor).Returns(false);

        var controller = new MessagesController(
            messaging.Object,
            db,
            currentUser.Object,
            patientAccess.Object,
            Mock.Of<ILogger<MessagesController>>());

        var result = await controller.SendMessage(
            conversationId,
            new SendMessageRequest { Content = "ملاحظة داخلية" });

        result.Result.Should().BeOfType<OkObjectResult>();

        var patientParticipant = await db.ConversationParticipants
            .IgnoreQueryFilters()
            .SingleAsync(cp => cp.ConversationId == conversationId && cp.UserId == linkedPatientUserId);

        patientParticipant.IsActive.Should().BeFalse(
            "a patient-linked user must not remain a recipient of internal StaffToPatient notifications");
        patientParticipant.DeletedAt.Should().NotBeNull();

        messaging.Verify(
            m => m.SendMessageAsync(conversationId, It.IsAny<SendMessageRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessage_PatientFacingConversation_KeepsPatientParticipantActive()
    {
        await using var db = CreateDb();

        var staffUserId = Guid.NewGuid();
        var linkedPatientUserId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = "MSG-PRIV-002",
            FirstName = "سارة",
            LastName = "علي"
        });

        db.PatientAccounts.Add(new PatientAccount
        {
            PatientId = patientId,
            PhoneNumber = "771000000",
            LinkedUserId = linkedPatientUserId,
            IsVerified = true,
            PortalAccountActive = true
        });

        db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            Title = "محادثة مع المريض: سارة علي",
            IsGroup = true,
            ConversationType = "PatientFacing",
            PatientId = patientId,
            CreatedBy = staffUserId
        });

        db.ConversationParticipants.AddRange(
            new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = staffUserId,
                IsAdmin = true
            },
            new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = linkedPatientUserId,
                IsAdmin = false
            });

        await db.SaveChangesAsync();

        var messaging = new Mock<IMessagingService>();
        messaging
            .Setup(m => m.SendMessageAsync(conversationId, It.IsAny<SendMessageRequest>()))
            .ReturnsAsync(new MessageDto
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderId = staffUserId,
                SenderName = "Staff",
                Content = "رسالة للمريض",
                CreatedAt = DateTime.UtcNow
            });

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(u => u.UserId).Returns(staffUserId);
        currentUser.SetupGet(u => u.IsAdmin).Returns(true);
        currentUser.SetupGet(u => u.IsAuthenticated).Returns(true);

        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(p => p.IsDoctor).Returns(false);

        var controller = new MessagesController(
            messaging.Object,
            db,
            currentUser.Object,
            patientAccess.Object,
            Mock.Of<ILogger<MessagesController>>());

        var result = await controller.SendMessage(
            conversationId,
            new SendMessageRequest { Content = "رسالة للمريض" });

        result.Result.Should().BeOfType<OkObjectResult>();

        var patientParticipant = await db.ConversationParticipants
            .IgnoreQueryFilters()
            .SingleAsync(cp => cp.ConversationId == conversationId && cp.UserId == linkedPatientUserId);

        patientParticipant.IsActive.Should().BeTrue(
            "PatientFacing conversations are intentionally visible to the patient");
    }
}
