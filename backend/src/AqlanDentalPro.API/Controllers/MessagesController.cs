using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize(Policy = "StaffOnly")]
public class MessagesController(IMessagingService messagingService, AppDbContext db, ILogger<MessagesController> logger) : ControllerBase
{
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
            // H-SEC FIX: Never expose raw exception messages to client
            logger.LogError(ex, "Schema status check failed");
            return StatusCode(500, new { error = "حدث خطأ أثناء فحص حالة قاعدة البيانات" });
        }
    }
    /// <summary>جلب محادثاتي</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<object>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null)
    {
        var result = await messagingService.GetMyConversationsAsync(page, pageSize, search, type);
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
            logger.LogWarning(ex, "Unauthorized message send to conversation {ConversationId}", conversationId);
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid argument sending message to conversation {ConversationId}", conversationId);
            return BadRequest(new { message = ex.Message });
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

    /// <summary>
    /// إنشاء/جلب محادثة PatientFacing مع مريض — مرئية للمريض في بوابته
    /// يُستخدم من زر "راسل المريض" وصفحة الرسائل
    /// </summary>
    [HttpPost("conversations/patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreatePatientFacingConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetOrCreatePatientFacingConversationAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Patient not found for patient-facing conversation, patient {PatientId}", patientId);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// إنشاء/جلب محادثة داخلية حول مريض (StaffToPatient) — يراها الطاقم فقط
    /// يُستخدم من تبويب الرسائل الداخلية في ملف المريض
    /// </summary>
    [HttpPost("internal-patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreateInternalPatientConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetOrCreatePatientConversationAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Patient not found for internal patient conversation, patient {PatientId}", patientId);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>تعديل رسالة (المرسل فقط، خلال 15 دقيقة)</summary>
    [HttpPut("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<ActionResult<MessageDto>> EditMessage(
        Guid conversationId, Guid messageId, [FromBody] EditMessageRequest request)
    {
        var (success, error, message) = await messagingService.EditMessageAsync(conversationId, messageId, request);
        if (!success)
        {
            if (message is null && error is null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية الوصول." });
            return BadRequest(new { message = error });
        }
        return Ok(message);
    }

    /// <summary>حذف رسالة (المرسل فقط)</summary>
    [HttpDelete("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid conversationId, Guid messageId)
    {
        var result = await messagingService.DeleteMessageAsync(conversationId, messageId);
        if (!result) return StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية الوصول." });
        return NoContent();
    }

    /// <summary>إحصائيات المراسلة</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<MessagingStatsDto>> GetStats()
    {
        var result = await messagingService.GetStatsAsync();
        return Ok(result);
    }

    /// <summary>جلب محادثة مريض الداخلية الموجودة (GET) — لا تنشئ واحدة جديدة</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetInternalPatientConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetPatientConversationAsync(patientId);
            if (result == null)
                return NotFound(new { message = "لا توجد محادثة داخلية مرتبطة بهذا المريض" });
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            logger.LogWarning(ex, "Patient not found for internal conversation lookup, patient {PatientId}", patientId);
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>جلب رسائل جديدة منذ آخر رسالة (للـ polling) — تصفية بالسيرفر</summary>
    [HttpGet("conversations/{conversationId:guid}/poll")]
    public async Task<IActionResult> PollMessages(Guid conversationId, [FromQuery] string? since = null)
    {
        if (since == null || !DateTime.TryParse(since, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sinceDate))
            return BadRequest(new { message = "يجب تحديد معامل 'since' بصيغة تاريخ صالحة" });

        try
        {
            var messages = await messagingService.PollNewMessagesAsync(conversationId, sinceDate);
            return Ok(new { messages });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(new { message = "المحادثة غير موجودة" });
        }
    }
}
