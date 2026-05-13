using AqlanDentalPro.Application.DTOs.WhatsApp;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Authorize(Policy = "StaffOnly")]
public class WhatsAppController(IWhatsAppService whatsappService) : ControllerBase
{
    [HttpGet("dashboard")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetDashboard()
    {
        var dashboard = await whatsappService.GetDashboardAsync();
        return Ok(dashboard);
    }

    [HttpPost("send")]
    [Authorize(Policy = "AppointmentAccess")]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            var result = await whatsappService.SendMessageAsync(request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("send-bulk")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SendBulk([FromBody] BulkSendMessageRequest request)
    {
        var results = await whatsappService.SendBulkAsync(request);
        return Ok(results);
    }

    [HttpPost("appointment-reminder")]
    [Authorize(Policy = "AppointmentAccess")]
    public async Task<IActionResult> SendAppointmentReminder([FromBody] SendAppointmentReminderRequest request)
    {
        var result = await whatsappService.SendAppointmentReminderAsync(request);
        return result == null ? NotFound(new { message = "الموعد غير موجود أو لا يوجد رقم هاتف" }) : Ok(result);
    }

    [HttpPost("send-pending-reminders")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SendPendingReminders()
    {
        var count = await whatsappService.SendPendingRemindersAsync();
        return Ok(new { message = $"تم إرسال {count} تذكير", count });
    }

    [HttpGet("messages")]
    [Authorize(Policy = "FinanceAccess")]
    public async Task<IActionResult> GetMessageHistory(
        [FromQuery] Guid? patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var messages = await whatsappService.GetMessageHistoryAsync(patientId, page, pageSize);
        return Ok(messages);
    }

    [HttpPost("messages/{id:guid}/retry")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RetryFailedMessage(Guid id)
    {
        var success = await whatsappService.RetryFailedMessageAsync(id);
        return success ? Ok(new { message = "تم إعادة الإرسال" }) : BadRequest(new { message = "لا يمكن إعادة الإرسال" });
    }

    [HttpGet("templates")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetTemplates()
    {
        var templates = await whatsappService.GetTemplatesAsync();
        return Ok(templates);
    }

    [HttpPut("templates/{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateWhatsAppTemplateRequest request)
    {
        var result = await whatsappService.UpdateTemplateAsync(id, request);
        return result == null ? NotFound(new { message = "القالب غير موجود" }) : Ok(result);
    }

    [HttpGet("messages/{id:guid}/status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetMessageStatus(Guid id)
    {
        var status = await whatsappService.GetMessageStatusAsync(id);
        return status == null ? NotFound() : Ok(status);
    }
}
