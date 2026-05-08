using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController(MessagingService messagingService) : ControllerBase
{
    /// <summary>جلب محادثاتي</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<object>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var result = await messagingService.GetMyConversationsAsync(page, pageSize, search);
        return Ok(new { result.Data, result.TotalCount, result.Page, result.PageSize, result.TotalPages, result.HasNextPage, result.HasPreviousPage });
    }

    /// <summary>جلب تفاصيل محادثة مع الرسائل</summary>
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await messagingService.GetConversationAsync(conversationId, page, pageSize);
        if (result == null) return NotFound(new { message = "المحادثة غير موجودة أو ليس لديك صلاحية الوصول" });
        return Ok(result);
    }

    /// <summary>إنشاء محادثة جديدة</summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDetailDto>> CreateConversation([FromBody] CreateConversationRequest request)
    {
        var result = await messagingService.CreateConversationAsync(request);
        return CreatedAtAction(nameof(GetConversation), new { conversationId = result.Id }, result);
    }

    /// <summary>إرسال رسالة في محادثة</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid conversationId, [FromBody] SendMessageRequest request)
    {
        try
        {
            var result = await messagingService.SendMessageAsync(conversationId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    /// <summary>تحديد الرسائل كمقروءة</summary>
    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        await messagingService.MarkAsReadAsync(conversationId);
        return NoContent();
    }

    /// <summary>عدد الرسائل غير المقروءة</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var result = await messagingService.GetUnreadCountAsync();
        return Ok(result);
    }

    /// <summary>مغادرة محادثة</summary>
    [HttpPost("conversations/{conversationId:guid}/leave")]
    public async Task<IActionResult> LeaveConversation(Guid conversationId)
    {
        await messagingService.LeaveConversationAsync(conversationId);
        return NoContent();
    }

    /// <summary>إنشاء/جلب محادثة داخلية حول مريض (StaffToPatient)</summary>
    [HttpPost("conversations/patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreatePatientConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetOrCreatePatientConversationAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>حذف رسالة (المرسل فقط)</summary>
    [HttpDelete("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid conversationId, Guid messageId)
    {
        var result = await messagingService.DeleteMessageAsync(conversationId, messageId);
        if (!result) return Forbid();
        return NoContent();
    }

    /// <summary>جلب رسائل جديدة منذ آخر رسالة (للـ polling)</summary>
    [HttpGet("conversations/{conversationId:guid}/poll")]
    public async Task<IActionResult> PollMessages(Guid conversationId, [FromQuery] string? since = null)
    {
        var result = await messagingService.GetConversationAsync(conversationId, 1, 50);
        if (result == null) return NotFound(new { message = "المحادثة غير موجودة" });

        if (since != null && DateTime.TryParse(since, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sinceDate))
        {
            result.Messages = result.Messages
                .Where(m => m.CreatedAt > sinceDate)
                .ToList();
        }

        return Ok(result);
    }
}
