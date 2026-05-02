using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Messaging endpoints for patient portal (uses Patient JWT with patientId claim)
/// </summary>
[ApiController]
[Route("api/portal/messages")]
[Authorize(Policy = "PatientAccess")]
public class PatientPortalMessagesController(AppDbContext db, INotificationService notifications) : ControllerBase
{
    private Guid PatientId => Guid.Parse(User.FindFirst("patientId")!.Value);
    private Guid? LinkedUserId => Guid.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : null;

    // ─── Get my conversations ──────────────────────────────────────────────
    [HttpGet("conversations")]
    public async Task<ActionResult<object>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var userId = LinkedUserId;
        if (userId == null)
            return Ok(new { Data = Array.Empty<object>(), TotalCount = 0, Page = 1, PageSize = pageSize, TotalPages = 0, HasNextPage = false, HasPreviousPage = false });

        var query = db.ConversationParticipants
            .Where(cp => cp.UserId == userId.Value)
            .Select(cp => cp.Conversation)
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Title.Contains(search) ||
                c.Participants.Any(p => p.User.Doctor != null && p.User.Doctor.Name.Contains(search)));
        }

        var total = await query.CountAsync();
        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId.Value);
            var dto = new ConversationListDto
            {
                Id = conv.Id,
                Title = conv.IsGroup ? conv.Title : (otherParticipant?.User?.Doctor?.Name ?? otherParticipant?.User?.Username ?? conv.Title),
                IsGroup = conv.IsGroup,
                LastMessageAt = conv.LastMessageAt,
                LastMessagePreview = conv.LastMessagePreview,
                OtherParticipant = otherParticipant != null ? new ConversationParticipantDto
                {
                    UserId = otherParticipant.UserId,
                    Username = otherParticipant.User?.Username ?? "",
                    DisplayName = otherParticipant.User?.Doctor?.Name ?? otherParticipant.User?.Username,
                    Role = otherParticipant.User?.Role.ToString(),
                    AvatarInitials = otherParticipant.User?.Doctor?.AvatarInitials,
                    Color = otherParticipant.User?.Doctor?.Color,
                    IsAdmin = otherParticipant.IsAdmin
                } : null,
                Participants = conv.Participants.Select(p => new ConversationParticipantDto
                {
                    UserId = p.UserId,
                    Username = p.User?.Username ?? "",
                    DisplayName = p.User?.Doctor?.Name ?? p.User?.Username,
                    Role = p.User?.Role.ToString(),
                    AvatarInitials = p.User?.Doctor?.AvatarInitials,
                    Color = p.User?.Doctor?.Color,
                    IsAdmin = p.IsAdmin
                }).ToList()
            };

            // Get unread count
            dto.UnreadCount = await db.Messages
                .Where(m => m.ConversationId == conv.Id && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
                .CountAsync();

            result.Add(dto);
        }

        return Ok(new { Data = result, TotalCount = total, Page = page, PageSize = pageSize });
    }

    // ─── Get conversation detail with messages ──────────────────────────────
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = LinkedUserId;
        if (userId == null) return NotFound(new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        // Verify participation
        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (!isParticipant) return NotFound(new { message = "المحادثة غير موجودة" });

        var conv = await db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.Doctor)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conv == null) return NotFound();

        var messages = await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
                .ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Mark as read
        var unreadMessages = await db.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            db.MessageReads.Add(new Domain.Entities.MessageRead
            {
                MessageId = msg.Id,
                UserId = userId.Value,
                ReadAt = DateTime.UtcNow
            });
        }

        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (participant != null)
            participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ConversationDetailDto
        {
            Id = conv.Id,
            Title = conv.Title,
            IsGroup = conv.IsGroup,
            Participants = conv.Participants.Select(p => new ConversationParticipantDto
            {
                UserId = p.UserId,
                Username = p.User?.Username ?? "",
                DisplayName = p.User?.Doctor?.Name ?? p.User?.Username,
                Role = p.User?.Role.ToString(),
                AvatarInitials = p.User?.Doctor?.AvatarInitials,
                Color = p.User?.Doctor?.Color,
                IsAdmin = p.IsAdmin
            }).ToList(),
            Messages = messages.Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.Doctor?.Name ?? m.Sender?.Username ?? "غير معروف",
                SenderInitials = m.Sender?.Doctor?.AvatarInitials,
                SenderColor = m.Sender?.Doctor?.Color,
                Content = m.Content,
                AttachmentUrl = m.AttachmentUrl,
                AttachmentName = m.AttachmentName,
                AttachmentType = m.AttachmentType,
                ReplyToId = m.ReplyToId,
                ReplyToContent = m.ReplyTo?.Content?.Length > 100 ? m.ReplyTo.Content[..100] + "..." : m.ReplyTo?.Content,
                ReplyToSenderName = m.ReplyTo?.Sender?.Doctor?.Name ?? m.ReplyTo?.Sender?.Username,
                IsSystemMessage = m.IsSystemMessage,
                IsReadByMe = m.Reads.Any(r => r.UserId == userId.Value),
                ReadCount = m.Reads.Count,
                CreatedAt = m.CreatedAt
            }).ToList(),
            CreatedAt = conv.CreatedAt
        });
    }

    // ─── Send message ──────────────────────────────────────────────────────
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid conversationId, [FromBody] SendMessageRequest request)
    {
        var userId = LinkedUserId;
        if (userId == null) return Forbid("حساب البوابة غير مرتبط بحساب مراسلة");

        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (!isParticipant) return Forbid("لست مشاركاً في هذه المحادثة");

        var msg = new Domain.Entities.Message
        {
            ConversationId = conversationId,
            SenderId = userId.Value,
            Content = request.Content,
            AttachmentUrl = request.AttachmentUrl,
            AttachmentName = request.AttachmentName,
            AttachmentType = request.AttachmentType,
            ReplyToId = request.ReplyToId,
        };

        db.Messages.Add(msg);

        var conv = await db.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.LastMessageAt = DateTime.UtcNow;
            conv.LastMessagePreview = request.Content.Length > 200 ? request.Content[..200] + "..." : request.Content;
        }

        await db.SaveChangesAsync();

        // Reload with navigation
        var loaded = await db.Messages
            .Include(m => m.Sender)
                .ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo)
            .FirstAsync(m => m.Id == msg.Id);

        // Notify other participants
        var otherParticipants = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId.Value)
            .Select(cp => cp.UserId)
            .ToListAsync();

        var senderName = loaded.Sender?.Doctor?.Name ?? loaded.Sender?.Username ?? "مريض";
        foreach (var pid in otherParticipants)
        {
            await notifications.NotifyAsync(pid, "message", "رسالة جديدة", $"رسالة جديدة من {senderName}", "Conversation", conversationId);
        }

        return Ok(new MessageDto
        {
            Id = loaded.Id,
            ConversationId = loaded.ConversationId,
            SenderId = loaded.SenderId,
            SenderName = loaded.Sender?.Doctor?.Name ?? loaded.Sender?.Username ?? "غير معروف",
            SenderInitials = loaded.Sender?.Doctor?.AvatarInitials,
            SenderColor = loaded.Sender?.Doctor?.Color,
            Content = loaded.Content,
            AttachmentUrl = loaded.AttachmentUrl,
            AttachmentName = loaded.AttachmentName,
            AttachmentType = loaded.AttachmentType,
            ReplyToId = loaded.ReplyToId,
            ReplyToContent = loaded.ReplyTo?.Content?.Length > 100 ? loaded.ReplyTo.Content[..100] + "..." : loaded.ReplyTo?.Content,
            ReplyToSenderName = loaded.ReplyTo?.Sender?.Doctor?.Name ?? loaded.ReplyTo?.Sender?.Username,
            IsSystemMessage = loaded.IsSystemMessage,
            IsReadByMe = true,
            ReadCount = loaded.Reads.Count,
            CreatedAt = loaded.CreatedAt
        });
    }

    // ─── Mark conversation as read ──────────────────────────────────────────
    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var userId = LinkedUserId;
        if (userId == null) return NoContent();

        var unreadMessages = await db.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            db.MessageReads.Add(new Domain.Entities.MessageRead
            {
                MessageId = msg.Id,
                UserId = userId.Value,
                ReadAt = DateTime.UtcNow
            });
        }

        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (participant != null)
            participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── Get unread count ──────────────────────────────────────────────────
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var userId = LinkedUserId;
        if (userId == null) return Ok(new UnreadCountDto { TotalUnread = 0, UnreadConversations = 0 });

        var myConversationIds = await db.ConversationParticipants
            .Where(cp => cp.UserId == userId.Value)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        var totalUnread = 0;
        var unreadConvs = 0;

        foreach (var convId in myConversationIds)
        {
            var count = await db.Messages
                .Where(m => m.ConversationId == convId && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
                .CountAsync();
            totalUnread += count;
            if (count > 0) unreadConvs++;
        }

        return Ok(new UnreadCountDto { TotalUnread = totalUnread, UnreadConversations = unreadConvs });
    }
}
