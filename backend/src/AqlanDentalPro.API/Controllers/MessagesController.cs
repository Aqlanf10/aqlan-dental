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
public class MessagesController(
    IMessagingService messagingService,
    AppDbContext db,
    ICurrentUserService currentUser,
    IPatientAccessService patientAccess,
    ILogger<MessagesController> logger) : ControllerBase
{
    /// <summary>
    /// MOBILE-03: fail closed before any patient-linked messaging operation.
    /// MessagingService intentionally uses IgnoreQueryFilters in several places so it can
    /// reactivate old participants. That must never become a branch/doctor IDOR surface.
    /// Return 404 for inaccessible patients so callers cannot enumerate patients in other branches.
    /// </summary>
    private async Task<ActionResult?> DenyIfPatientInaccessible(Guid patientId)
    {
        var patient = await db.Patients
            .IgnoreQueryFilters()
            .Where(p => p.Id == patientId && p.IsActive)
            .Select(p => new { p.Id, p.BranchId })
            .FirstOrDefaultAsync();

        if (patient is null)
            return NotFound(new { message = "المريض غير موجود" });

        if (!currentUser.IsAdmin)
        {
            if (!currentUser.BranchId.HasValue
                || currentUser.BranchId.Value == Guid.Empty
                || !patient.BranchId.HasValue
                || patient.BranchId.Value != currentUser.BranchId.Value)
            {
                logger.LogWarning(
                    "Messaging patient branch access denied: user {UserId} attempted patient {PatientId}",
                    currentUser.UserId,
                    patientId);
                return NotFound(new { message = "المريض غير موجود" });
            }
        }

        if (patientAccess.IsDoctor && !await patientAccess.CanAccessPatientAsync(patientId))
        {
            logger.LogWarning(
                "Messaging patient doctor access denied: user {UserId} attempted patient {PatientId}",
                currentUser.UserId,
                patientId);
            return NotFound(new { message = "المريض غير موجود" });
        }

        return null;
    }

    /// <summary>
    /// StaffToPatient is an internal staff conversation about a patient, not a portal conversation.
    /// Older MessagingService behavior added the patient's linked messaging User as a participant,
    /// which could make that user receive generic "new message" notifications even though the portal
    /// correctly refuses to expose StaffToPatient conversations. Deactivate that participant before
    /// loading or sending internal messages. PatientFacing conversations are intentionally untouched.
    /// </summary>
    private async Task EnsureInternalConversationIsStaffOnlyAsync(Guid conversationId)
    {
        var conversation = await db.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.Id == conversationId
                     && c.IsActive
                     && c.ConversationType == "StaffToPatient"
                     && c.PatientId.HasValue)
            .Select(c => new { PatientId = c.PatientId!.Value })
            .FirstOrDefaultAsync();

        if (conversation is null)
            return;

        var linkedPatientUserId = await db.PatientAccounts
            .IgnoreQueryFilters()
            .Where(a => a.PatientId == conversation.PatientId
                     && a.IsActive
                     && a.LinkedUserId.HasValue)
            .Select(a => a.LinkedUserId)
            .FirstOrDefaultAsync();

        if (!linkedPatientUserId.HasValue)
            return;

        var patientParticipant = await db.ConversationParticipants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId
                                    && cp.UserId == linkedPatientUserId.Value);

        if (patientParticipant is null || !patientParticipant.IsActive)
            return;

        patientParticipant.IsActive = false;
        patientParticipant.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Removed patient-linked participant {PatientUserId} from internal conversation {ConversationId}",
            linkedPatientUserId.Value,
            conversationId);
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
        [FromQuery] string? type = null,
        [FromQuery] bool? hasUnread = null)
    {
        var result = await messagingService.GetMyConversationsAsync(page, pageSize, search, type, hasUnread);
        return Ok(new { result.Data, result.TotalCount, result.Page, result.PageSize, result.TotalPages, result.HasNextPage, result.HasPreviousPage });
    }

    /// <summary>جلب تفاصيل محادثة مع الرسائل</summary>
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            await EnsureInternalConversationIsStaffOnlyAsync(conversationId);
            var result = await messagingService.GetConversationAsync(conversationId, page, pageSize);
            if (result == null) return NotFound(new { message = "المحادثة غير موجودة أو ليس لديك صلاحية الوصول" });
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load conversation {ConversationId}", conversationId);
            return StatusCode(500, new { message = "حدث خطأ أثناء تحميل بيانات المحادثة" });
        }
    }

    /// <summary>إنشاء محادثة جديدة</summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDetailDto>> CreateConversation([FromBody] CreateConversationRequest request)
    {
        if (request.PatientId.HasValue)
        {
            var denied = await DenyIfPatientInaccessible(request.PatientId.Value);
            if (denied != null) return denied;
        }

        try
        {
            var result = await messagingService.CreateConversationAsync(request);
            if (string.Equals(result.ConversationType, "StaffToPatient", StringComparison.OrdinalIgnoreCase))
                await EnsureInternalConversationIsStaffOnlyAsync(result.Id);
            return CreatedAtAction(nameof(GetConversation), new { conversationId = result.Id }, result);
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية إنشاء هذه المحادثة" });
        }
    }

    /// <summary>إرسال رسالة في محادثة</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid conversationId, [FromBody] SendMessageRequest request)
    {
        try
        {
            // Privacy boundary: scrub legacy patient participants before the service enumerates
            // recipients for notifications/push on an internal StaffToPatient conversation.
            await EnsureInternalConversationIsStaffOnlyAsync(conversationId);
            var result = await messagingService.SendMessageAsync(conversationId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized message send to conversation {ConversationId}", conversationId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية الإرسال في هذه المحادثة" });
        }
        catch (ArgumentException)
        {
            logger.LogWarning("Invalid argument sending message to conversation {ConversationId}", conversationId);
            return BadRequest(new { message = "بيانات الرسالة غير صالحة" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error sending message to conversation {ConversationId}", conversationId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ غير متوقع أثناء إرسال الرسالة" });
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
        var denied = await DenyIfPatientInaccessible(patientId);
        if (denied != null) return denied;

        try
        {
            var result = await messagingService.GetOrCreatePatientFacingConversationAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("Patient not found for patient-facing conversation, patient {PatientId}", patientId);
            return NotFound(new { message = "المريض غير موجود" });
        }
    }

    /// <summary>
    /// إنشاء/جلب محادثة داخلية حول مريض (StaffToPatient) — يراها الطاقم فقط
    /// يُستخدم من تبويب الرسائل الداخلية في ملف المريض
    /// </summary>
    [HttpPost("internal-patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreateInternalPatientConversation(Guid patientId)
    {
        var denied = await DenyIfPatientInaccessible(patientId);
        if (denied != null) return denied;

        try
        {
            var result = await messagingService.GetOrCreatePatientConversationAsync(patientId);
            await EnsureInternalConversationIsStaffOnlyAsync(result.Id);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("Patient not found for internal patient conversation, patient {PatientId}", patientId);
            return NotFound(new { message = "المريض غير موجود" });
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
        var denied = await DenyIfPatientInaccessible(patientId);
        if (denied != null) return denied;

        try
        {
            var internalConversationId = await db.Conversations
                .Where(c => c.PatientId == patientId && c.ConversationType == "StaffToPatient")
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync();
            if (internalConversationId.HasValue)
                await EnsureInternalConversationIsStaffOnlyAsync(internalConversationId.Value);

            var result = await messagingService.GetPatientConversationAsync(patientId);
            if (result == null)
                return NotFound(new { message = "لا توجد محادثة داخلية مرتبطة بهذا المريض" });
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            logger.LogWarning("Patient not found for internal conversation lookup, patient {PatientId}", patientId);
            return NotFound(new { message = "المريض غير موجود" });
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
            await EnsureInternalConversationIsStaffOnlyAsync(conversationId);
            var messages = await messagingService.PollNewMessagesAsync(conversationId, sinceDate);
            return Ok(new { messages });
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(new { message = "المحادثة غير موجودة" });
        }
    }
}
