using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Messaging;

/// <summary>
/// Unit tests for <see cref="WhatsAppController"/>.
///
/// Coverage map:
///   - SendMessage: 200 + service called with exact request on happy path
///   - SendMessage: 400 Arabic when service throws InvalidOperationException (the
///     fail-closed contract — e.g. WhatsApp not configured, patient has no phone)
///   - SendAppointmentReminder: 404 Arabic when service returns null (no appointment
///     or no phone); 200 when service returns a DTO
///   - SendBulk: 200 + service called; admin-only (auth enforced via attribute)
///   - SendPendingReminders: 200 with "تم إرسال {N} تذكير" format
///   - GetMessageHistory / GetDashboard / GetTemplates: 200 + service called with
///     pagination params
///   - RetryFailedMessage: 200 Arabic on success, 400 Arabic on failure
///   - UpdateTemplate: 200 on success, 404 Arabic when service returns null
///   - GetMessageStatus: 200 on success, 404 (no body) when service returns null
///
/// The audit specifically called out the WhatsApp fail-closed behavior — the service
/// throws InvalidOperationException when not configured (SEC-07 / WhatsApp config
/// missing). The controller must translate that into a 400 with the Arabic exception
/// message, NOT a 500 or a silent no-op. Tests pin this contract.
/// </summary>
public class WhatsAppControllerTests
{
    // ── SendMessage (POST /api/whatsapp/send) ────────────────────────────────────

    [Fact]
    public async Task SendMessage_HappyPath_Returns200_AndCallsServiceWithExactRequest()
    {
        var request = MessagingTestData.BuildWhatsAppSendRequest();
        var dto = MessagingTestData.BuildWhatsAppMessageDto();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>()))
            .ReturnsAsync(dto)
            .Verifiable();
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.SendMessage(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeSameAs(dto,
            "the controller must echo back the DTO returned by the service — no re-wrapping");

        serviceMock.Verify(s => s.SendMessageAsync(request), Times.Once,
            "the service must be called exactly once with the EXACT request the controller received");
    }

    [Fact]
    public async Task SendMessage_ServiceThrowsInvalidOperation_Returns400_ArabicMessage_FailClosed()
    {
        // FAIL-CLOSED CONTRACT: when WhatsApp is not configured (SEC-07), the service
        // throws InvalidOperationException with an Arabic message. The controller must
        // translate that into a 400 — NOT a 500, NOT a silent 200 with no message sent.
        // This is the audit's WhatsApp fail-closed behavior pin.
        var request = MessagingTestData.BuildWhatsAppSendRequest();
        var arabicError = "خدمة واتساب غير مُهيأة — يرجى مراجعة الإعدادات";
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>()))
            .ThrowsAsync(new InvalidOperationException(arabicError));
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.SendMessage(request);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be(arabicError,
            "the controller must surface the service's Arabic InvalidOperationException message verbatim");
    }

    // ── SendAppointmentReminder (POST /api/whatsapp/appointment-reminder) ────────

    [Fact]
    public async Task SendAppointmentReminder_ServiceReturnsNull_Returns404_ArabicMessage()
    {
        // Null from the service means: appointment not found, OR patient has no phone.
        // The controller must return 404 (not 200 with null body) with an Arabic message.
        var request = MessagingTestData.BuildWhatsAppReminderRequest();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.SendAppointmentReminderAsync(It.IsAny<SendAppointmentReminderRequest>()))
            .ReturnsAsync((WhatsAppMessageDto?)null);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.SendAppointmentReminder(request);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value).Should().Be("الموعد غير موجود أو لا يوجد رقم هاتف");
    }

    [Fact]
    public async Task SendAppointmentReminder_HappyPath_Returns200_AndEchoesDto()
    {
        var request = MessagingTestData.BuildWhatsAppReminderRequest();
        var dto = MessagingTestData.BuildWhatsAppMessageDto();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.SendAppointmentReminderAsync(It.IsAny<SendAppointmentReminderRequest>()))
            .ReturnsAsync(dto);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.SendAppointmentReminder(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
        serviceMock.Verify(s => s.SendAppointmentReminderAsync(request), Times.Once);
    }

    // ── SendBulk (POST /api/whatsapp/send-bulk) — admin-only ─────────────────────

    [Fact]
    public async Task SendBulk_HappyPath_Returns200_AndCallsServiceWithExactRequest()
    {
        var patientA = Guid.NewGuid();
        var patientB = Guid.NewGuid();
        var request = MessagingTestData.BuildWhatsAppBulkRequest(patientA, patientB);
        var results = new List<WhatsAppMessageDto>
        {
            MessagingTestData.BuildWhatsAppMessageDto(),
            MessagingTestData.BuildWhatsAppMessageDto()
        };
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.SendBulkAsync(It.IsAny<BulkSendMessageRequest>()))
            .ReturnsAsync(results);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.SendBulk(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(results);
        serviceMock.Verify(s => s.SendBulkAsync(request), Times.Once);
    }

    // ── SendPendingReminders (POST /api/whatsapp/send-pending-reminders) ─────────

    [Fact]
    public async Task SendPendingReminders_Returns200_WithArabicCountMessage()
    {
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.SendPendingRemindersAsync()).ReturnsAsync(7);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.SendPendingReminders();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((string)payload.message).Should().Be("تم إرسال 7 تذكير",
            "the success message must include the count and be in Arabic");
        ((int)payload.count).Should().Be(7);
    }

    // ── GetDashboard (GET /api/whatsapp/dashboard) — admin-only ──────────────────

    [Fact]
    public async Task GetDashboard_HappyPath_Returns200_AndEchoesDashboard()
    {
        var dashboard = MessagingTestData.BuildWhatsAppDashboard();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.GetDashboardAsync()).ReturnsAsync(dashboard);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.GetDashboard();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dashboard);
    }

    [Fact]
    public async Task GetDashboard_ServiceThrows_Returns500_ArabicMessage()
    {
        // The dashboard endpoint wraps the service call in a try/catch — any exception
        // (DB unreachable, Redis down, etc.) must surface as 500 Arabic, NOT a raw stack.
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.GetDashboardAsync()).ThrowsAsync(new InvalidOperationException("boom"));
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.GetDashboard();

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        ExtractMessage(statusResult.Value).Should().Be("حدث خطأ أثناء تحميل البيانات");
    }

    // ── GetMessageHistory (GET /api/whatsapp/messages) ───────────────────────────

    [Fact]
    public async Task GetMessageHistory_PassesPatientIdAndPaginationToService()
    {
        var patientId = Guid.NewGuid();
        var page = 2;
        var pageSize = 25;
        var history = new List<WhatsAppMessageDto> { MessagingTestData.BuildWhatsAppMessageDto() };
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.GetMessageHistoryAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(history)
            .Verifiable();
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.GetMessageHistory(patientId, page, pageSize);

        result.Should().BeOfType<OkObjectResult>();
        serviceMock.Verify(s => s.GetMessageHistoryAsync(patientId, page, pageSize), Times.Once,
            "patientId + pagination params must be forwarded verbatim to the service");
    }

    // ── RetryFailedMessage (POST /api/whatsapp/messages/{id}/retry) ──────────────

    [Fact]
    public async Task RetryFailedMessage_ServiceReturnsTrue_Returns200_ArabicMessage()
    {
        var messageId = Guid.NewGuid();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.RetryFailedMessageAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.RetryFailedMessage(messageId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Be("تم إعادة الإرسال");
        serviceMock.Verify(s => s.RetryFailedMessageAsync(messageId), Times.Once);
    }

    [Fact]
    public async Task RetryFailedMessage_ServiceReturnsFalse_Returns400_ArabicMessage()
    {
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.RetryFailedMessageAsync(It.IsAny<Guid>())).ReturnsAsync(false);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.RetryFailedMessage(Guid.NewGuid());

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("لا يمكن إعادة الإرسال");
    }

    // ── Templates (GET / PUT) ────────────────────────────────────────────────────

    [Fact]
    public async Task GetTemplates_HappyPath_Returns200_AndEchoesList()
    {
        var templates = new List<WhatsAppTemplateDto> { MessagingTestData.BuildWhatsAppTemplate() };
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.GetTemplatesAsync()).ReturnsAsync(templates);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.GetTemplates();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(templates);
    }

    [Fact]
    public async Task UpdateTemplate_ServiceReturnsNull_Returns404_ArabicMessage()
    {
        var id = Guid.NewGuid();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.UpdateTemplateAsync(It.IsAny<Guid>(), It.IsAny<UpdateWhatsAppTemplateRequest>()))
            .ReturnsAsync((WhatsAppTemplateDto?)null);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.UpdateTemplate(id, new UpdateWhatsAppTemplateRequest());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("القالب غير موجود");
    }

    [Fact]
    public async Task UpdateTemplate_HappyPath_Returns200_AndCallsServiceWithExactArgs()
    {
        var id = Guid.NewGuid();
        var req = new UpdateWhatsAppTemplateRequest { ContentTemplate = "new template", IsActive = true };
        var template = MessagingTestData.BuildWhatsAppTemplate(id);
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.UpdateTemplateAsync(It.IsAny<Guid>(), It.IsAny<UpdateWhatsAppTemplateRequest>()))
            .ReturnsAsync(template);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.UpdateTemplate(id, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(template);
        serviceMock.Verify(s => s.UpdateTemplateAsync(id, req), Times.Once);
    }

    // ── GetMessageStatus (GET /api/whatsapp/messages/{id}/status) ────────────────

    [Fact]
    public async Task GetMessageStatus_ServiceReturnsNull_Returns404_NoBody()
    {
        // Unlike other endpoints, GetMessageStatus returns a bare NotFound() (no Arabic
        // body). This is a minor inconsistency — the audit's "every 4xx must have an
        // Arabic message" rule is violated here. Test documents the actual behavior.
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.GetMessageStatusAsync(It.IsAny<Guid>())).ReturnsAsync((WhatsAppMessageDto?)null);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.GetMessageStatus(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMessageStatus_HappyPath_Returns200_AndEchoesDto()
    {
        var dto = MessagingTestData.BuildWhatsAppMessageDto();
        var serviceMock = new Mock<IWhatsAppService>();
        serviceMock.Setup(s => s.GetMessageStatusAsync(It.IsAny<Guid>())).ReturnsAsync(dto);
        var controller = MessagingTestData.BuildWhatsAppController(serviceMock);

        var result = await controller.GetMessageStatus(dto.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx/5xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }
}
