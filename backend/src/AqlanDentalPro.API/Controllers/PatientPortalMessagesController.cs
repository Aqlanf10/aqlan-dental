using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Messaging endpoints for patient portal (uses Patient JWT with patientId claim).
/// SECURITY: All endpoints enforce ConversationType == "PatientFacing" and PatientId == this patient.
/// Internal StaffToPatient conversations are NEVER exposed here.
/// </summary>
[ApiController]
[Route("api/portal/messages")]
[Authorize(Policy = "PatientAccess")]
public class PatientPortalMessagesController(
    IPatientPortalMessagingService messagingService,
    ILogger<PatientPortalMessagesController> logger) : ControllerBase
{
    private Guid PatientId
    {
        get
        {
            var claim = User.FindFirst("patientId");
            if (claim == null || !Guid.TryParse(claim.Value, out var id))
                throw new UnauthorizedAccessException("Missing or invalid patientId claim");
            return id;
        }
    }

    private Guid? LinkedUserId => Guid.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : null;

    private async Task<Guid?> ResolveUserIdAsync()
    {
        var userId = LinkedUserId;
        if (userId != null) return userId;
        return await messagingService.EnsureLinkedUserAsync(PatientId);
    }

    // ─── GET /api/portal/messages/recipients ────────────────────────────────────
    [HttpGet("recipients")]
    public async Task<ActionResult<List<PortalRecipientDto>>> GetRecipients()
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null) return StatusCode(403, new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        var recipients = await messagingService.GetRecipientsAsync(PatientId);
        return Ok(recipients);
    }

    // ─── GET /api/portal/messages/conversations ───────────────────────────────
    [HttpGet("conversations")]
    public async Task<ActionResult<object>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null)
            return Ok(new { data = Array.Empty<object>(), totalCount = 0, page = 1, pageSize, totalPages = 0 });

        try
        {
            var (result, totalCount) = await messagingService.GetConversationsAsync(PatientId, userId.Value, page, pageSize, search);
            return Ok(result);
        }
        catch (ServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    // ─── POST /api/portal/messages/conversations ──────────────────────────────
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDetailDto>> StartConversation(
        [FromBody] StartConversationRequest? request)
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null) return StatusCode(403, new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        try
        {
            var result = await messagingService.StartConversationAsync(PatientId, userId.Value, request);
            return Ok(result);
        }
        catch (ServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    // ─── GET /api/portal/messages/conversations/{conversationId} ─────────────
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null) return StatusCode(403, new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        try
        {
            var result = await messagingService.GetConversationAsync(PatientId, userId.Value, conversationId, page, pageSize);
            if (result == null) return NotFound(new { message = "المحادثة غير موجودة أو ليس لديك صلاحية الوصول" });
            return Ok(result);
        }
        catch (ServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    // ─── POST /api/portal/messages/conversations/{conversationId}/messages ────
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request)
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null) return StatusCode(403, new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        try
        {
            var result = await messagingService.SendMessageAsync(PatientId, userId.Value, conversationId, request);
            return Ok(result);
        }
        catch (ServiceException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized portal message send to conversation {ConversationId}", conversationId);
            return StatusCode(403, new { message = "ليس لديك صلاحية الإرسال في هذه المحادثة" });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid argument in portal message send to conversation {ConversationId}", conversationId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error sending portal message to conversation {ConversationId}", conversationId);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إرسال الرسالة" });
        }
    }

    // ─── POST /api/portal/messages/conversations/{conversationId}/read ────────
    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null) return NoContent();

        await messagingService.MarkAsReadAsync(PatientId, userId.Value, conversationId);
        return NoContent();
    }

    // ─── GET /api/portal/messages/unread-count ────────────────────────────────
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var userId = await ResolveUserIdAsync();
        if (userId == null)
            return Ok(new UnreadCountDto { TotalUnread = 0, UnreadConversations = 0 });

        var result = await messagingService.GetUnreadCountAsync(PatientId, userId.Value);
        return Ok(result);
    }
}
