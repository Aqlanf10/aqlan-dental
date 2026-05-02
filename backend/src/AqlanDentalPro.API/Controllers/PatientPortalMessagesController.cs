using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            .Include(c => c.Patient)
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

        // Batch unread counts
        var convIds = conversations.Select(c => c.Id).ToList();
        var unreadCounts = await db.Messages
            .Where(m => convIds.Contains(m.ConversationId) && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConvId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConvId, x => x.Count);

        var result = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId.Value);
            var patientName = conv.Patient != null ? $"{conv.Patient.FirstName} {conv.Patient.LastName}".Trim() : null;
            var dto = new ConversationListDto
            {
                Id = conv.Id,
                Title = conv.ConversationType == "StaffToPatient" && patientName != null
                    ? $"محادثة مع المركز — {patientName}"
                    : (otherParticipant?.User?.Doctor?.Name ?? otherParticipant?.User?.Username ?? conv.Title),
                IsGroup = conv.IsGroup,
                ConversationType = conv.ConversationType,
                PatientId = conv.PatientId,
                PatientName = patientName,
                LastMessageAt = conv.LastMessageAt,
                LastMessagePreview = conv.LastMessagePreview,
                UnreadCount = unreadCounts.GetValueOrDefault(conv.Id),
                OtherParticipant = otherParticipant != null ? MapParticipant(otherParticipant) : null,
                Participants = conv.Participants.Select(MapParticipant).ToList()
            };
            result.Add(dto);
        }

        return Ok(new { Data = result, TotalCount = total, Page = page, PageSize = pageSize });
    }

    /// <summary>المريض يبدأ محادثة مع المركز — ينشئ أو يجلب محادثة StaffToPatient خاصة به</summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDetailDto>> StartConversation([FromBody] StartConversationRequest? request = null)
    {
        var userId = LinkedUserId;
        if (userId == null) return BadRequest(new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        var patientId = PatientId;

        // Find or create a StaffToPatient conversation for this patient
        var existing = await db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.PatientId == patientId && c.ConversationType == "StaffToPatient");

        if (existing != null)
        {
            // Already exists — return it
            return await GetConversation(existing.Id);
        }

        // Create new conversation
        var patient = await db.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == patientId);
        var patientName = patient != null ? $"{patient.FirstName} {patient.LastName}".Trim() : "مريض";
        var patientNumber = patient?.PatientNumber ?? "";

        var conv = new Conversation
        {
            Title = $"المريض: {patientName}",
            IsGroup = true,
            ConversationType = "StaffToPatient",
            PatientId = patientId,
        };

        await db.Conversations.AddAsync(conv);

        // Add patient as participant
        await db.ConversationParticipants.AddAsync(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId = userId.Value,
            IsAdmin = false
        });

        // Find staff to add (admin or the patient's primary doctor)
        var staffUser = await db.Users
            .Include(u => u.Doctor)
            .FirstOrDefaultAsync(u => u.Role == UserRole.Admin && u.IsActive);
        
        if (staffUser == null)
        {
            // Try primary doctor
            if (patient?.PrimaryDoctorId != null)
            {
                staffUser = await db.Users
                    .Include(u => u.Doctor)
                    .FirstOrDefaultAsync(u => u.Doctor != null && u.Doctor.Id == patient.PrimaryDoctorId && u.IsActive);
            }
        }

        if (staffUser != null)
        {
            await db.ConversationParticipants.AddAsync(new ConversationParticipant
            {
                ConversationId = conv.Id,
                UserId = staffUser.Id,
                IsAdmin = true
            });
        }

        // Add system message
        var initialContent = request?.InitialMessage;
        if (!string.IsNullOrWhiteSpace(initialContent))
        {
            await db.Messages.AddAsync(new Message
            {
                ConversationId = conv.Id,
                SenderId = userId.Value,
                Content = initialContent,
                IsSystemMessage = false
            });
            conv.LastMessageAt = DateTime.UtcNow;
            conv.LastMessagePreview = initialContent.Length > 200 ? initialContent[..200] + "..." : initialContent;
        }
        else
        {
            await db.Messages.AddAsync(new Message
            {
                ConversationId = conv.Id,
                SenderId = userId.Value,
                Content = $"بدأ المريض {patientName} محادثة مع المركز",
                IsSystemMessage = true
            });
            conv.LastMessageAt = DateTime.UtcNow;
            conv.LastMessagePreview = $"محادثة جديدة: {patientName}";
        }

        await db.SaveChangesAsync();

        // Notify staff participants
        if (staffUser != null)
        {
            await notifications.NotifyAsync(staffUser.Id, "message", "رسالة جديدة من مريض",
                $"المريض {patientName} بدأ محادثة", "Conversation", conv.Id);
        }

        return await GetConversation(conv.Id);
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var userId = LinkedUserId;
        if (userId == null) return NotFound(new { message = "حساب البوابة غير مرتبط بحساب مراسلة" });

        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (!isParticipant) return NotFound(new { message = "المحادثة غير موجودة" });

        var conv = await db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return NotFound();

        var messages = await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads).Include(m => m.ReplyTo)
            .OrderByDescending(m => m.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .OrderBy(m => m.CreatedAt).ToListAsync();

        // Mark as read
        var unreadMessages = await db.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            db.MessageReads.Add(new MessageRead { MessageId = msg.Id, UserId = userId.Value, ReadAt = DateTime.UtcNow });
        }

        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (participant != null) participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var patientName = conv.Patient != null ? $"{conv.Patient.FirstName} {conv.Patient.LastName}".Trim() : null;
        var patientPhone = conv.Patient?.Phone;
        var patientNumber = conv.Patient?.PatientNumber;

        return Ok(new ConversationDetailDto
        {
            Id = conv.Id,
            Title = conv.ConversationType == "StaffToPatient" && patientName != null
                ? $"محادثة مع المركز — {patientName}"
                : conv.Title,
            IsGroup = conv.IsGroup,
            ConversationType = conv.ConversationType,
            PatientId = conv.PatientId,
            PatientName = patientName,
            PatientNumber = patientNumber,
            PatientPhone = patientPhone,
            Participants = conv.Participants.Select(MapParticipant).ToList(),
            Messages = messages.Select(MapMessage).ToList(),
            CreatedAt = conv.CreatedAt
        });
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid conversationId, [FromBody] SendMessageRequest request)
    {
        var userId = LinkedUserId;
        if (userId == null) return Forbid("حساب البوابة غير مرتبط بحساب مراسلة");

        var isParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (!isParticipant) return Forbid("لست مشاركاً في هذه المحادثة");

        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { message = "محتوى الرسالة لا يمكن أن يكون فارغاً" });

        var msg = new Message
        {
            ConversationId = conversationId, SenderId = userId.Value,
            Content = content, AttachmentUrl = request.AttachmentUrl,
            AttachmentName = request.AttachmentName, AttachmentType = request.AttachmentType,
            ReplyToId = request.ReplyToId,
        };

        db.Messages.Add(msg);

        var conv = await db.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.LastMessageAt = DateTime.UtcNow;
            conv.LastMessagePreview = content.Length > 200 ? content[..200] + "..." : content;
        }

        await db.SaveChangesAsync();

        var loaded = await db.Messages.Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads).Include(m => m.ReplyTo)
            .FirstAsync(m => m.Id == msg.Id);

        var senderName = loaded.Sender?.Doctor?.Name ?? loaded.Sender?.Username ?? "مريض";
        var otherParticipants = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId.Value)
            .Select(cp => cp.UserId).ToListAsync();

        foreach (var pid in otherParticipants)
        {
            await notifications.NotifyAsync(pid, "message", "رسالة جديدة", $"رسالة جديدة من {senderName}", "Conversation", conversationId);
        }

        return Ok(MapMessage(loaded));
    }

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var userId = LinkedUserId;
        if (userId == null) return NoContent();

        var unread = await db.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
            .ToListAsync();

        foreach (var msg in unread)
            db.MessageReads.Add(new MessageRead { MessageId = msg.Id, UserId = userId.Value, ReadAt = DateTime.UtcNow });

        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId.Value);
        if (participant != null) participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var userId = LinkedUserId;
        if (userId == null) return Ok(new UnreadCountDto { TotalUnread = 0, UnreadConversations = 0 });

        var myConvIds = await db.ConversationParticipants
            .Where(cp => cp.UserId == userId.Value).Select(cp => cp.ConversationId).ToListAsync();

        var totalUnread = 0; var unreadConvs = 0;
        foreach (var convId in myConvIds)
        {
            var count = await db.Messages
                .Where(m => m.ConversationId == convId && m.SenderId != userId.Value && !m.Reads.Any(r => r.UserId == userId.Value))
                .CountAsync();
            totalUnread += count;
            if (count > 0) unreadConvs++;
        }

        return Ok(new UnreadCountDto { TotalUnread = totalUnread, UnreadConversations = unreadConvs });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static ConversationParticipantDto MapParticipant(ConversationParticipant cp) => new()
    {
        UserId = cp.UserId, Username = cp.User?.Username ?? "",
        DisplayName = cp.User?.Role == UserRole.Patient
            ? (cp.User.Username != "" ? $"مريض ({cp.User.Username})" : "مريض")
            : (cp.User?.Doctor?.Name ?? cp.User?.Username),
        Role = cp.User?.Role.ToString(), AvatarInitials = cp.User?.Doctor?.AvatarInitials,
        Color = cp.User?.Doctor?.Color, IsAdmin = cp.IsAdmin
    };

    private MessageDto MapMessage(Message m) => new()
    {
        Id = m.Id, ConversationId = m.ConversationId, SenderId = m.SenderId,
        SenderName = m.Sender?.Role == UserRole.Patient
            ? "مريض"
            : (m.Sender?.Doctor?.Name ?? m.Sender?.Username ?? "غير معروف"),
        SenderInitials = m.Sender?.Doctor?.AvatarInitials, SenderColor = m.Sender?.Doctor?.Color,
        Content = m.Content, AttachmentUrl = m.AttachmentUrl,
        AttachmentName = m.AttachmentName, AttachmentType = m.AttachmentType,
        ReplyToId = m.ReplyToId,
        ReplyToContent = m.ReplyTo?.Content?.Length > 100 ? m.ReplyTo.Content[..100] + "..." : m.ReplyTo?.Content,
        ReplyToSenderName = m.ReplyTo?.Sender?.Doctor?.Name ?? m.ReplyTo?.Sender?.Username,
        IsSystemMessage = m.IsSystemMessage,
        IsReadByMe = m.Reads.Any(r => r.UserId == LinkedUserId),
        ReadCount = m.Reads.Count, CreatedAt = m.CreatedAt
    };
}

public class StartConversationRequest
{
    public string? InitialMessage { get; set; }
}
