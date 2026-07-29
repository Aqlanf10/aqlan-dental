using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Sms;
using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace AqlanDentalPro.UnitTests.Messaging;

/// <summary>
/// Shared seed + builder helpers for the messaging-controller test suite
/// (<see cref="WhatsAppController"/>, <see cref="SmsController"/>,
/// <see cref="WhatsAppWebhookController"/>).
///
/// These controllers are thin: they validate input shape, delegate to a service
/// (<see cref="IWhatsAppService"/> / <see cref="ISmsService"/>), and translate
/// service-thrown <see cref="InvalidOperationException"/> into 400 Arabic responses.
/// The service implementations are out-of-scope (their own fail-closed behavior is
/// tested at the service layer); here we verify the controller contracts:
///   - Service is called with the EXACT request the controller received (no mutation).
///   - <see cref="InvalidOperationException"/> from a service is surfaced as 400 Arabic
///     (the fail-closed behavior — e.g. WhatsApp service throws when not configured).
///   - Admin-only endpoints (dashboard, send-bulk, retry, templates) call the service
///     but the [Authorize(Policy="AdminOnly")] attribute is the access-control gate.
///   - Success responses echo the Arabic message format the frontend expects.
/// </summary>
internal static class MessagingTestData
{
    // ── DTO factories ────────────────────────────────────────────────────────────

    public static SendMessageRequest BuildWhatsAppSendRequest(
        Guid? patientId = null,
        string templateType = "appointment_reminder",
        string? customMessage = "تذكير بموعد الغد الساعة 10 صباحًا")
        => new()
        {
            PatientId = patientId ?? Guid.NewGuid(),
            TemplateType = templateType,
            CustomMessage = customMessage
        };

    public static BulkSendMessageRequest BuildWhatsAppBulkRequest(params Guid[] patientIds)
        => new()
        {
            PatientIds = patientIds.ToList(),
            TemplateType = "appointment_reminder"
        };

    public static SendAppointmentReminderRequest BuildWhatsAppReminderRequest(Guid? appointmentId = null)
        => new() { AppointmentId = appointmentId ?? Guid.NewGuid(), HoursBefore = 24 };

    public static WhatsAppMessageDto BuildWhatsAppMessageDto(Guid? id = null, string status = "sent")
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            PatientName = "أحمد المريض",
            PhoneNumber = "+967777123456",
            TemplateType = "appointment_reminder",
            MessageContent = "تذكير بموعد",
            Status = status,
            RetryCount = 0,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

    public static WhatsAppDashboardDto BuildWhatsAppDashboard()
        => new()
        {
            SentToday = 12,
            DeliveredToday = 10,
            FailedToday = 2,
            PendingCount = 3
        };

    public static WhatsAppTemplateDto BuildWhatsAppTemplate(Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            TemplateKey = "appointment_reminder",
            NameAr = "تذكير الموعد",
            ContentTemplate = "مرحباً {name}، نذكرك بموعدك في {datetime}",
            IsActive = true,
            Category = "appointments"
        };

    public static SendSmsRequest BuildSmsSendRequest(Guid? patientId = null)
        => new()
        {
            PatientId = patientId ?? Guid.NewGuid(),
            TemplateType = "custom",
            CustomMessage = "تذكير بموعد"
        };

    public static BulkSendSmsRequest BuildSmsBulkRequest(params Guid[] patientIds)
        => new()
        {
            PatientIds = patientIds.ToList(),
            TemplateType = "custom"
        };

    public static SendSmsAppointmentReminderRequest BuildSmsReminderRequest(Guid? appointmentId = null)
        => new() { AppointmentId = appointmentId ?? Guid.NewGuid(), HoursBefore = 24 };

    public static SmsMessageDto BuildSmsMessageDto(Guid? id = null, string status = "sent")
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            PatientName = "أحمد المريض",
            PhoneNumber = "+967777123456",
            TemplateType = "custom",
            MessageContent = "تذكير",
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

    public static SmsDashboardDto BuildSmsDashboard()
        => new()
        {
            IsEnabled = true,
            IsGatewayConnected = true,
            SentToday = 5,
            FailedToday = 1,
            PendingCount = 0,
            DeliveredToday = 4,
            DailyLimit = 500,
            DeliveryRate = 0.8m
        };

    public static SmsTemplateDto BuildSmsTemplate(Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            TemplateKey = "appointment_reminder",
            NameAr = "تذكير الموعد",
            ContentTemplate = "تذكير بموعدك غدًا",
            IsTemplateActive = true,
            Category = "appointments",
            MaxLength = 160
        };

    // ── Controller builders ──────────────────────────────────────────────────────

    public static WhatsAppController BuildWhatsAppController(
        Mock<IWhatsAppService>? serviceMock = null,
        Mock<ILogger<WhatsAppController>>? loggerMock = null)
    {
        serviceMock ??= new Mock<IWhatsAppService>();
        loggerMock ??= new Mock<ILogger<WhatsAppController>>();
        return new WhatsAppController(serviceMock.Object, loggerMock.Object);
    }

    public static SmsController BuildSmsController(
        Mock<ISmsService>? serviceMock = null,
        Mock<ILogger<SmsController>>? loggerMock = null)
    {
        serviceMock ??= new Mock<ISmsService>();
        loggerMock ??= new Mock<ILogger<SmsController>>();
        return new SmsController(serviceMock.Object, loggerMock.Object);
    }

    public static WhatsAppWebhookController BuildWebhookController(
        AqlanDentalPro.Infrastructure.Data.AppDbContext db,
        Mock<IConfiguration>? configMock = null,
        Mock<ILogger<WhatsAppWebhookController>>? loggerMock = null)
    {
        configMock ??= new Mock<IConfiguration>();
        loggerMock ??= new Mock<ILogger<WhatsAppWebhookController>>();
        return new WhatsAppWebhookController(db, configMock.Object, loggerMock.Object);
    }
}
