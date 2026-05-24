using AqlanDentalPro.Application.DTOs.Sms;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// SMS Gateway Management — Local Android SIM SMS Gateway for clinic reminders.
/// Sends SMS through an Android phone running an SMS Gateway API app.
/// </summary>
[ApiController]
[Route("api/sms")]
[Authorize(Policy = "StaffOnly")]
public class SmsController : ControllerBase
{
    private readonly ISmsService _smsService;
    private readonly ILogger<SmsController> _logger;

    public SmsController(ISmsService smsService, ILogger<SmsController> logger)
    {
        _smsService = smsService;
        _logger = logger;
    }

    // ─── 1. GET /api/sms/dashboard ────────────────────────────────────────
    /// <summary>SMS dashboard with stats and recent messages.</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await _smsService.GetDashboardAsync();
        return Ok(dashboard);
    }

    // ─── 2. POST /api/sms/send ────────────────────────────────────────────
    /// <summary>Send a single SMS message to a patient.</summary>
    [HttpPost("send")]
    [Authorize(Policy = "AppointmentAccess")]
    public async Task<IActionResult> SendSms([FromBody] SendSmsRequest request)
    {
        try
        {
            var result = await _smsService.SendSmsAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to patient {PatientId}", request.PatientId);
            return StatusCode(500, new { message = "فشل إرسال الرسالة" });
        }
    }

    // ─── 3. POST /api/sms/send-bulk ───────────────────────────────────────
    /// <summary>Send SMS to multiple patients at once.</summary>
    [HttpPost("send-bulk")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SendBulkSms([FromBody] BulkSendSmsRequest request)
    {
        try
        {
            var results = await _smsService.SendBulkAsync(request);
            return Ok(new
            {
                TotalRequested = request.PatientIds.Count,
                SentSuccessfully = results.Count(r => r.Status == "sent"),
                Failed = results.Count(r => r.Status == "failed"),
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send bulk SMS");
            return StatusCode(500, new { message = "فشل إرسال الرسائل الجماعية" });
        }
    }

    // ─── 4. POST /api/sms/appointment-reminder ────────────────────────────
    /// <summary>Send appointment reminder SMS to a specific patient.</summary>
    [HttpPost("appointment-reminder")]
    [Authorize(Policy = "AppointmentAccess")]
    public async Task<IActionResult> SendAppointmentReminder([FromBody] SendSmsAppointmentReminderRequest request)
    {
        try
        {
            var result = await _smsService.SendAppointmentReminderAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send appointment reminder SMS for appointment {AppointmentId}", request.AppointmentId);
            return StatusCode(500, new { message = "فشل إرسال تذكير الموعد" });
        }
    }

    // ─── 5. POST /api/sms/send-pending-reminders ──────────────────────────
    /// <summary>Trigger sending of all pending SMS reminders (cron job fallback).</summary>
    [HttpPost("send-pending-reminders")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SendPendingReminders()
    {
        try
        {
            var count = await _smsService.SendPendingRemindersAsync();
            return Ok(new { sentCount = count, message = $"تم إرسال {count} تذكير" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send pending SMS reminders");
            return StatusCode(500, new { message = "فشل إرسال التذكيرات المعلقة" });
        }
    }

    // ─── 6. GET /api/sms/messages ──────────────────────────────────────────
    /// <summary>Get paginated list of SMS messages with optional status filter.</summary>
    [HttpGet("messages")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetMessages([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null)
    {
        var result = await _smsService.GetMessagesAsync(page, pageSize, status);
        return Ok(result);
    }

    // ─── 7. POST /api/sms/messages/{id}/retry ─────────────────────────────
    /// <summary>Retry sending a failed SMS message.</summary>
    [HttpPost("messages/{id:guid}/retry")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RetryFailedMessage(Guid id)
    {
        try
        {
            var result = await _smsService.RetryFailedMessageAsync(id);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry SMS message {MessageId}", id);
            return StatusCode(500, new { message = "فشل إعادة إرسال الرسالة" });
        }
    }

    // ─── 8. GET /api/sms/templates ─────────────────────────────────────────
    /// <summary>Get all SMS templates.</summary>
    [HttpGet("templates")]
    [Authorize(Policy = "StaffOnly")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await _smsService.GetTemplatesAsync();
        return Ok(templates);
    }

    // ─── 9. PUT /api/sms/templates/{id} ────────────────────────────────────
    /// <summary>Update an SMS template's content and/or name.</summary>
    [HttpPut("templates/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateSmsTemplateRequest request)
    {
        try
        {
            var result = await _smsService.UpdateTemplateAsync(id, request.ContentTemplate, request.NameAr);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ─── 10. GET /api/sms/settings ─────────────────────────────────────────
    /// <summary>Get SMS gateway settings (API key masked).</summary>
    [HttpGet("settings")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetSettings()
    {
        var settings = await _smsService.GetGatewaySettingsAsync();
        return Ok(settings);
    }

    // ─── 11. PUT /api/sms/settings ─────────────────────────────────────────
    /// <summary>Update SMS gateway settings.</summary>
    [HttpPut("settings")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSmsGatewaySettingsRequest request)
    {
        var settings = await _smsService.UpdateGatewaySettingsAsync(request);
        return Ok(settings);
    }

    // ─── 12. POST /api/sms/test-connection ─────────────────────────────────
    /// <summary>Test connection to the SMS gateway.</summary>
    [HttpPost("test-connection")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> TestConnection()
    {
        var isConnected = await _smsService.TestGatewayConnectionAsync();
        return Ok(new { connected = isConnected, message = isConnected ? "تم الاتصال بنجاح بالبوابة" : "فشل الاتصال بالبوابة" });
    }
}

// ─── Additional Request DTOs ──────────────────────────────────────────────────

public class UpdateSmsTemplateRequest
{
    public string ContentTemplate { get; set; } = string.Empty;
    public string? NameAr { get; set; }
}
