using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController(MessagingService messagingService, AppDbContext db) : ControllerBase
{
    /// <summary>تطبيق الـ migrations يدوياً (Admin فقط)</summary>
    [HttpPost("ensure-schema")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> EnsureSchema()
    {
        try
        {
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();

            if (pendingMigrations.Any())
            {
                await db.Database.MigrateAsync();
                return Ok(new
                {
                    message = "تم تطبيق الـ migrations بنجاح",
                    applied = pendingMigrations.ToList(),
                    previouslyApplied = appliedMigrations.ToList()
                });
            }

            return Ok(new
            {
                message = "قاعدة البيانات محدثة - لا توجد migrations معلقة",
                appliedMigrations = appliedMigrations.ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "فشل تطبيق الـ migrations",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    /// <summary>فحص حالة جداول المراسلة (Admin فقط)</summary>
    [HttpGet("schema-status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> SchemaStatus()
    {
        try
        {
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
            var canConnect = await db.Database.CanConnectAsync();

            return Ok(new
            {
                canConnect,
                pendingMigrations = pendingMigrations.ToList(),
                appliedMigrations = appliedMigrations.ToList(),
                conversationsExists = db.Conversations.Any(),
                messageReadsExists = db.MessageReads.Any()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }
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

    /// <summary>إنشاء أو جلب محادثة مع مريض بواسطة معرف المريض</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreatePatientConversation(Guid patientId)
    {
        var result = await messagingService.GetOrCreatePatientConversationAsync(patientId);
        if (result == null) return NotFound(new { message = "المريض ليس لديه حساب في البوابة" });
        return Ok(result);
    }
}
