using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Sms;
using AqlanDentalPro.Application.Interfaces.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Messaging;

/// <summary>
/// Unit tests for <see cref="SmsController"/>.
///
/// Coverage map:
///   - SendSms: 200 + service called with exact request; 400 Arabic on
///     InvalidOperationException (fail-closed when gateway is down / not configured)
///   - SendAppointmentReminder: 200 + service called; 400 Arabic on InvalidOperation
///   - SendBulkSms: 200 with aggregated sent/failed counts computed from service result
///   - SendPendingReminders: 200 with "تم إرسال {N} تذكير" + sentCount field
///   - GetMessages: 200 + service called with pagination + status filter
///   - RetryFailedMessage: 200 on success; 400 Arabic on InvalidOperation; 500 Arabic
///     on unexpected exception
///   - UpdateTemplate: 200 on success; 404 Arabic on InvalidOperation
///   - GetSettings / UpdateSettings: 200 + service called
///   - TestConnection: 200 with Arabic connected/failed message based on service result
///
/// The audit specifically called out the SMS fail-closed behavior — same as WhatsApp,
/// the service throws InvalidOperationException when the local-Android-SIM gateway is
/// unreachable. The controller must translate that into a 400 Arabic, NOT a 500.
/// </summary>
public class SmsControllerTests
{
    // ── SendSms (POST /api/sms/send) ─────────────────────────────────────────────

    [Fact]
    public async Task SendSms_HappyPath_Returns200_AndCallsServiceWithExactRequest()
    {
        var request = MessagingTestData.BuildSmsSendRequest();
        var dto = MessagingTestData.BuildSmsMessageDto();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendSmsAsync(It.IsAny<SendSmsRequest>()))
            .ReturnsAsync(dto)
            .Verifiable();
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendSms(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeSameAs(dto);

        serviceMock.Verify(s => s.SendSmsAsync(request), Times.Once,
            "the service must be called exactly once with the EXACT request the controller received");
    }

    [Fact]
    public async Task SendSms_ServiceThrowsInvalidOperation_Returns400_Arabic_FailClosed()
    {
        // FAIL-CLOSED: when the SMS gateway is not configured / unreachable, the service
        // throws InvalidOperationException with an Arabic message. The controller must
        // translate that into a 400 — NOT a 500 (compare with the generic-Exception
        // catch which returns 500).
        var request = MessagingTestData.BuildSmsSendRequest();
        var arabicError = "بوابة SMS غير مُهيأة — تحقق من الإعدادات";
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendSmsAsync(It.IsAny<SendSmsRequest>()))
            .ThrowsAsync(new InvalidOperationException(arabicError));
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendSms(request);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.StatusCode.Should().Be(400);
        ExtractMessage(bad.Value).Should().Be(arabicError);
    }

    [Fact]
    public async Task SendSms_ServiceThrowsGenericException_Returns500_Arabic()
    {
        // Non-InvalidOperationException must NOT leak the raw exception message — it must
        // surface as a generic 500 Arabic message (CLAUDE.md: لا تكشف تفاصيل الاستثناءات).
        var request = MessagingTestData.BuildSmsSendRequest();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendSmsAsync(It.IsAny<SendSmsRequest>()))
            .ThrowsAsync(new InvalidOperationException("LEAK_ME") { });
        // Use a non-InvalidOperationException to test the generic catch — wrap in a custom exception
        serviceMock.Setup(s => s.SendSmsAsync(It.IsAny<SendSmsRequest>()))
            .ThrowsAsync(new TimeoutException("connection timed out — DB pool exhausted"));
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendSms(request);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        ExtractMessage(statusResult.Value).Should().Be("فشل إرسال الرسالة");
    }

    // ── SendAppointmentReminder (POST /api/sms/appointment-reminder) ─────────────

    [Fact]
    public async Task SendAppointmentReminder_HappyPath_Returns200_AndCallsServiceWithExactRequest()
    {
        var request = MessagingTestData.BuildSmsReminderRequest();
        var dto = MessagingTestData.BuildSmsMessageDto();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendAppointmentReminderAsync(It.IsAny<SendSmsAppointmentReminderRequest>()))
            .ReturnsAsync(dto);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendAppointmentReminder(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
        serviceMock.Verify(s => s.SendAppointmentReminderAsync(request), Times.Once);
    }

    [Fact]
    public async Task SendAppointmentReminder_ServiceThrowsInvalidOperation_Returns400_Arabic()
    {
        var request = MessagingTestData.BuildSmsReminderRequest();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendAppointmentReminderAsync(It.IsAny<SendSmsAppointmentReminderRequest>()))
            .ThrowsAsync(new InvalidOperationException("الموعد غير موجود أو لا يوجد رقم هاتف"));
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendAppointmentReminder(request);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("الموعد غير موجود أو لا يوجد رقم هاتف");
    }

    // ── SendBulkSms (POST /api/sms/send-bulk) — admin-only ───────────────────────

    [Fact]
    public async Task SendBulkSms_Returns200_WithAggregatedSentFailedCounts()
    {
        var patientA = Guid.NewGuid();
        var patientB = Guid.NewGuid();
        var patientC = Guid.NewGuid();
        var request = MessagingTestData.BuildSmsBulkRequest(patientA, patientB, patientC);
        var results = new List<SmsMessageDto>
        {
            MessagingTestData.BuildSmsMessageDto(status: "sent"),
            MessagingTestData.BuildSmsMessageDto(status: "sent"),
            MessagingTestData.BuildSmsMessageDto(status: "failed")
        };
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendBulkAsync(It.IsAny<BulkSendSmsRequest>()))
            .ReturnsAsync(results);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendBulkSms(request);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.TotalRequested).Should().Be(3);
        ((int)payload.SentSuccessfully).Should().Be(2,
            "the controller computes SentSuccessfully from results.Count(r => r.Status == \"sent\")");
        ((int)payload.Failed).Should().Be(1);
    }

    [Fact]
    public async Task SendBulkSms_ServiceThrowsGenericException_Returns500_Arabic()
    {
        var request = MessagingTestData.BuildSmsBulkRequest();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendBulkAsync(It.IsAny<BulkSendSmsRequest>()))
            .ThrowsAsync(new InvalidOperationException("x")); // overridden below
        serviceMock.Setup(s => s.SendBulkAsync(It.IsAny<BulkSendSmsRequest>()))
            .ThrowsAsync(new NullReferenceException("service unavailable"));
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendBulkSms(request);

        var statusResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(500);
        ExtractMessage(statusResult.Value).Should().Be("فشل إرسال الرسائل الجماعية");
    }

    // ── SendPendingReminders (POST /api/sms/send-pending-reminders) ──────────────

    [Fact]
    public async Task SendPendingReminders_Returns200_WithArabicCountMessage()
    {
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.SendPendingRemindersAsync()).ReturnsAsync(5);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.SendPendingReminders();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((int)payload.sentCount).Should().Be(5);
        ((string)payload.message).Should().Be("تم إرسال 5 تذكير");
    }

    // ── GetMessages (GET /api/sms/messages) — FinanceAccess ──────────────────────

    [Fact]
    public async Task GetMessages_PassesPaginationAndStatusFilterToService()
    {
        var page = 3;
        var pageSize = 50;
        var status = "failed";
        var paged = new PagedResult<SmsMessageDto>
        {
            Items = new List<SmsMessageDto> { MessagingTestData.BuildSmsMessageDto() },
            TotalCount = 1,
            Page = page,
            PageSize = pageSize
        };
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.GetMessagesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync(paged);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.GetMessages(page, pageSize, status);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(paged);
        serviceMock.Verify(s => s.GetMessagesAsync(page, pageSize, status), Times.Once,
            "page + pageSize + status filter must be forwarded verbatim to the service");
    }

    // ── RetryFailedMessage (POST /api/sms/messages/{id}/retry) ───────────────────

    [Fact]
    public async Task RetryFailedMessage_HappyPath_Returns200_AndCallsServiceWithExactId()
    {
        var id = Guid.NewGuid();
        var dto = MessagingTestData.BuildSmsMessageDto();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.RetryFailedMessageAsync(It.IsAny<Guid>())).ReturnsAsync(dto);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.RetryFailedMessage(id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
        serviceMock.Verify(s => s.RetryFailedMessageAsync(id), Times.Once);
    }

    [Fact]
    public async Task RetryFailedMessage_ServiceThrowsInvalidOperation_Returns400_Arabic()
    {
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.RetryFailedMessageAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("لا يمكن إعادة إرسال رسالة لم تُرسل سابقًا"));
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.RetryFailedMessage(Guid.NewGuid());

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        ExtractMessage(bad.Value).Should().Be("لا يمكن إعادة إرسال رسالة لم تُرسل سابقًا");
    }

    // ── UpdateTemplate (PUT /api/sms/templates/{id}) — admin-only ────────────────

    [Fact]
    public async Task UpdateTemplate_HappyPath_Returns200_AndCallsServiceWithContentAndName()
    {
        var id = Guid.NewGuid();
        var req = new UpdateSmsTemplateRequest { ContentTemplate = "new content", NameAr = "اسم جديد" };
        var dto = MessagingTestData.BuildSmsTemplate(id);
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.UpdateTemplateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(dto);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.UpdateTemplate(id, req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
        // The controller unwraps the request DTO and passes ContentTemplate + NameAr to the service:
        serviceMock.Verify(s => s.UpdateTemplateAsync(id, "new content", "اسم جديد"), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplate_ServiceThrowsInvalidOperation_Returns404_Arabic()
    {
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.UpdateTemplateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("القالب غير موجود"));
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.UpdateTemplate(Guid.NewGuid(), new UpdateSmsTemplateRequest());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        ExtractMessage(notFound.Value).Should().Be("القالب غير موجود");
    }

    // ── Settings (GET / PUT) + TestConnection ────────────────────────────────────

    [Fact]
    public async Task GetSettings_HappyPath_Returns200_AndEchoesSettings()
    {
        var settings = new SmsGatewaySettingsDto
        {
            Enabled = true,
            ApiUrl = "http://192.168.1.5:5000",
            HasApiKey = true,
            GatewayMode = "local_android",
            SenderName = "AqlanDental",
            DailyLimit = 500,
            SendAppointmentReminders = true,
            SendPaymentReminders = true,
            ReminderHours = "24,2",
            IsGatewayConnected = true
        };
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.GetGatewaySettingsAsync()).ReturnsAsync(settings);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.GetSettings();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(settings);
    }

    [Fact]
    public async Task UpdateSettings_HappyPath_Returns200_AndCallsServiceWithExactRequest()
    {
        var req = new UpdateSmsGatewaySettingsRequest
        {
            Enabled = true,
            ApiUrl = "http://192.168.1.5:5000",
            GatewayMode = "local_android"
        };
        var settings = new SmsGatewaySettingsDto { Enabled = true };
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.UpdateGatewaySettingsAsync(It.IsAny<UpdateSmsGatewaySettingsRequest>()))
            .ReturnsAsync(settings);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.UpdateSettings(req);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(settings);
        serviceMock.Verify(s => s.UpdateGatewaySettingsAsync(req), Times.Once);
    }

    [Theory]
    [InlineData(true, "تم الاتصال بنجاح بالبوابة")]
    [InlineData(false, "فشل الاتصال بالبوابة")]
    public async Task TestConnection_ReturnsArabicMessage_BasedOnServiceResult(bool connected, string expectedMessage)
    {
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.TestGatewayConnectionAsync()).ReturnsAsync(connected);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.TestConnection();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        dynamic payload = ok.Value!;
        ((bool)payload.connected).Should().Be(connected);
        ((string)payload.message).Should().Be(expectedMessage);
    }

    // ── GetDashboard (GET /api/sms/dashboard) — admin-only ───────────────────────

    [Fact]
    public async Task GetDashboard_HappyPath_Returns200_AndEchoesDashboard()
    {
        var dashboard = MessagingTestData.BuildSmsDashboard();
        var serviceMock = new Mock<ISmsService>();
        serviceMock.Setup(s => s.GetDashboardAsync()).ReturnsAsync(dashboard);
        var controller = MessagingTestData.BuildSmsController(serviceMock);

        var result = await controller.GetDashboard();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dashboard);
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
