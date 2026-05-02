using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Messaging endpoints for patient portal (uses Patient JWT with patientId claim).
/// SECURITY: All endpoints enforce ConversationType == "PatientFacing" and PatientId == this patient.
/// Internal StaffToPatient conversations are NEVER exposed here.
/// </summary>
[ApiController]
[Route("api/portal/messages")]
[Authorize(Policy = "PatientAccess")]
public class PatientPortalMessagesController(AppDbContext db, INotificationService notifications) : ControllerBase
{
    private Guid PatientId => Guid.Parse(User.FindFirst("patientId")!.Value);
    private Guid? LinkedUserId => Guid.TryParse(User.FindFirst("userId")?.Value, out var id) ? id : null;

    /// <summary>
    /// Resolves the patient's messaging User ID. Returns null if not linked.
    /// Checks JWT claim first, then falls back to DB lookup/create.
    /// NOTE: The same linking logic exists in PatientPortalService.EnsureLinkedUserAsync().
    /// Both must be kept in sync. The controller version exists as a runtime fallback
    /// for cases where the JWT was issued before linking was completed.
    /// </summary>
    private async Task<Guid?> EnsureLinkedUserAsync()
    {
        var userId = LinkedUserId;
        if (userId != null) return userId;

        var account = await db.PatientAccounts
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.PatientId == PatientId);

        if (account == null) return null;
        if (account.LinkedUserId.HasValue)
        {
            // Already linked — just return the value (JWT will have it on next login)
            return account.LinkedUserId.Value;
        }

        // Not yet linked — create the link using the same logic as PatientPortalService
        var username = account.Username ?? account.Patient?.PatientNumber ?? $"patient-{PatientId}";
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (existingUser != null)
        {
            account.LinkedUserId = existingUser.Id;
        }
        else
        {
            var linkedUser = new User
            {
                Username = username,
                PasswordHash = account.PasswordHash ?? "",
                PasswordSalt = account.PasswordSalt ?? "",
                Role = UserRole.Patient,
                IsActive = true
            };
            db.Users.Add(linkedUser);
            await db.SaveChangesAsync();
            account.LinkedUserId = linkedUser.Id;
        }

        await db.SaveChangesAsync();
        return account.LinkedUserId.Value;
    }

    /// <summary>
    /// Verifies a conversation is PatientFacing AND belongs to this patient AND patient is a participant.
    /// Returns (userId, conversation) on success or an error ActionResult.
    /// </summary>
    private async Task<(Guid? userId, Conversation? conv, IActionResult? error)> VerifyPatientFacingAccessAsync(Guid conversationId)
    {
        var userId = await EnsureLinkedUserAsync();
        if (userId == null)
            return (null, null, Forbid("حساب البوابة غير مرتبط بحساب مراسلة"));

        var conv = await db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        // Conversation must exist
        if (conv == null)
            return (userId, null, NotFound(new { message = "المحادثة غير موجودة" }));

        // SECURITY: Must be PatientFacing — never expose internal staff conversations
        if (conv.ConversationType != ConversationType.PatientFacing.ToString())
            return (userId, null, NotFound(new { message = "المحادثة غير موجودة" }));

        // SECURITY: Must belong to this patient
        if (conv.PatientId != PatientId)
            return (userId, null, Forbid("ليس لديك صلاحية الوصول لهذه المحادثة"));

        // SECURITY: Patient must be a participant
        var isParticipant = conv.Participants.Any(p => p.UserId == userId.Value);
        if (!isParticipant)
            return (userId, null, Forbid("لست مشاركاً في هذه المحادثة"));

        return (userId, conv, null);
    }

    // ─── GET /api/portal/messages/conversations ───────────────────────────────
    [HttpGet("conversations")]
    public async Task<ActionResult<object>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var userId = await EnsureLinkedUserAsync();
        if (userId == null)
            return Ok(new { data = Array.Empty<object>(), totalCount = 0, page = 1, pageSize, totalPages = 0 });

        // Ensure patient is a participant in their PatientFacing conversations
        await EnsurePatientInPatientFacingConversationsAsync(userId.Value);

        // SECURITY: Only PatientFacing conversations for this patient
        var query = db.ConversationParticipants
            .Where(cp => cp.UserId == userId.Value)
            .Select(cp => cp.Conversation)
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.Doctor)
            .Where(c => c.ConversationType == ConversationType.PatientFacing.ToString() && c.PatientId == PatientId)
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
            // Show the staff participant's info as the "other" person
            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId.Value);
            var dto = new ConversationListDto
            {
                Id = conv.Id,
                Title = conv.IsGroup
                    ? conv.Title
                    : (otherParticipant?.User?.Doctor?.Name ?? otherParticipant?.User?.Username ?? conv.Title),
                IsGroup = conv.IsGroup,
                ConversationType = conv.ConversationType,
                PatientId = conv.PatientId,
                LastMessageAt = conv.LastMessageAt,
                LastMessagePreview = conv.LastMessagePreview,
                UnreadCount = await db.Messages
                    .Where(m => m.ConversationId == conv.Id
                             && m.SenderId != userId.Value
                             && !m.Reads.Any(r => r.UserId == userId.Value))
                    .CountAsync(),
                OtherParticipant = otherParticipant != null ? MapParticipant(otherParticipant) : null,
                Participants = conv.Participants.Select(MapParticipant).ToList()
            };
            result.Add(dto);
        }

        return Ok(new
        {
            data = result,
            totalCount = total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        });
    }

    // ─── POST /api/portal/messages/conversations ──────────────────────────────
    /// <summary>Start or open a PatientFacing conversation with the clinic</summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDetailDto>> StartConversation(
        [FromBody] StartConversationRequest? request)
    {
        var userId = await EnsureLinkedUserAsync();
        if (userId == null) return Forbid("حساب البوابة غير مرتبط بحساب مراسلة");

        // Check if a PatientFacing conversation already exists for this patient
        var existingConv = await db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c =>
                c.PatientId == PatientId &&
                c.ConversationType == ConversationType.PatientFacing.ToString()); // SECURITY: PatientFacing only

        if (existingConv != null)
        {
            var isAlreadyParticipant = existingConv.Participants.Any(p => p.UserId == userId.Value);
            if (!isAlreadyParticipant)
            {
                db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = existingConv.Id,
                    UserId = userId.Value,
                    IsAdmin = false,
                    LastReadAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(request?.InitialMessage))
            {
                db.Messages.Add(new Message
                {
                    ConversationId = existingConv.Id,
                    SenderId = userId.Value,
                    Content = request.InitialMessage.Trim()
                });
                existingConv.LastMessageAt = DateTime.UtcNow;
                existingConv.LastMessagePreview = request.InitialMessage.Length > 200
                    ? request.InitialMessage[..200] + "..." : request.InitialMessage;
                await db.SaveChangesAsync();
            }

            return await GetConversationById(existingConv.Id, userId.Value);
        }

        // Create a new PatientFacing conversation
        var patient = await db.Patients.FindAsync(PatientId);
        var convTitle = patient != null
            ? $"محادثة مع المريض: {patient.FirstName} {patient.LastName}"
            : "محادثة مريض";

        var conv = new Conversation
        {
            Title = convTitle,
            IsGroup = true,
            ConversationType = ConversationType.PatientFacing.ToString(), // SECURITY: Always PatientFacing
            PatientId = PatientId,
            CreatedBy = userId.Value,
            LastMessageAt = DateTime.UtcNow
        };

        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        // Add patient as participant
        db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId = userId.Value,
            IsAdmin = false,
            LastReadAt = DateTime.UtcNow
        });

        // Add all active admin users so the clinic can respond
        var adminUsers = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .ToListAsync();
        foreach (var admin in adminUsers)
        {
            db.ConversationParticipants.Add(new ConversationParticipant
            {
                ConversationId = conv.Id,
                UserId = admin.Id,
                IsAdmin = true
            });
        }

        // Add the patient's primary doctor if assigned
        if (patient?.PrimaryDoctorId != null)
        {
            var doctorUserId = await db.Doctors
                .Where(d => d.Id == patient.PrimaryDoctorId)
                .Select(d => d.UserId)
                .FirstOrDefaultAsync();

            if (doctorUserId.HasValue && doctorUserId.Value != Guid.Empty
                && !adminUsers.Any(a => a.Id == doctorUserId.Value))
            {
                db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conv.Id,
                    UserId = doctorUserId.Value,
                    IsAdmin = false
                });
            }
        }

        // System message clarifying this is patient-visible
        db.Messages.Add(new Message
        {
            ConversationId = conv.Id,
            SenderId = userId.Value,
            Content = $"بدأ المريض {patient?.FirstName} {patient?.LastName} ({patient?.PatientNumber}) محادثة من البوابة — محادثة مع المريض (مرئية للمريض)",
            IsSystemMessage = true
        });

        if (!string.IsNullOrWhiteSpace(request?.InitialMessage))
        {
            db.Messages.Add(new Message
            {
                ConversationId = conv.Id,
                SenderId = userId.Value,
                Content = request.InitialMessage.Trim()
            });
            conv.LastMessagePreview = request.InitialMessage.Length > 200
                ? request.InitialMessage[..200] + "..." : request.InitialMessage;
        }

        await db.SaveChangesAsync();
        return await GetConversationById(conv.Id, userId.Value);
    }

    // ─── GET /api/portal/messages/conversations/{conversationId} ─────────────
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var (userId, conv, error) = await VerifyPatientFacingAccessAsync(conversationId);
        if (error != null) return (ActionResult)error;

        return await GetConversationById(conversationId, userId!.Value, page, pageSize);
    }

    // ─── POST /api/portal/messages/conversations/{conversationId}/messages ────
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(
        Guid conversationId,
        [FromBody] SendMessageRequest request)
    {
        var (userId, conv, error) = await VerifyPatientFacingAccessAsync(conversationId);
        if (error != null) return (ActionResult)error;

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "محتوى الرسالة لا يمكن أن يكون فارغاً" });
        if (request.Content.Length > 2000)
            return BadRequest(new { message = "محتوى الرسالة طويل جداً، الحد الأقصى 2000 حرف" });

        var msg = new Message
        {
            ConversationId = conversationId,
            SenderId = userId!.Value,
            Content = request.Content.Trim(),
            AttachmentUrl = request.AttachmentUrl,
            AttachmentName = request.AttachmentName,
            AttachmentType = request.AttachmentType,
            ReplyToId = request.ReplyToId,
        };

        db.Messages.Add(msg);

        conv!.LastMessageAt = DateTime.UtcNow;
        conv.LastMessagePreview = request.Content.Length > 200
            ? request.Content[..200] + "..." : request.Content;

        await db.SaveChangesAsync();

        var loaded = await db.Messages
            .Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo)
            .FirstAsync(m => m.Id == msg.Id);

        var senderName = loaded.Sender?.Doctor?.Name ?? loaded.Sender?.Username ?? "مريض";
        var otherParticipants = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId.Value)
            .Select(cp => cp.UserId)
            .ToListAsync();

        foreach (var pid in otherParticipants)
            await notifications.NotifyAsync(pid, "message", "رسالة جديدة من مريض",
                $"رسالة من {senderName}", "Conversation", conversationId);

        return Ok(MapMessage(loaded));
    }

    // ─── POST /api/portal/messages/conversations/{conversationId}/read ────────
    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        var (userId, _, error) = await VerifyPatientFacingAccessAsync(conversationId);
        if (error != null) return NoContent(); // Silently succeed on auth failure for read

        var unread = await db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != userId!.Value
                     && !m.Reads.Any(r => r.UserId == userId.Value))
            .ToListAsync();

        foreach (var m in unread)
            db.MessageReads.Add(new MessageRead { MessageId = m.Id, UserId = userId!.Value, ReadAt = DateTime.UtcNow });

        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId!.Value);
        if (participant != null) participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ─── GET /api/portal/messages/unread-count ────────────────────────────────
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var userId = await EnsureLinkedUserAsync();
        if (userId == null)
            return Ok(new UnreadCountDto { TotalUnread = 0, UnreadConversations = 0 });

        // SECURITY: Only count PatientFacing conversations belonging to this patient
        var patientFacingConvIds = await db.Conversations
            .Where(c => c.PatientId == PatientId
                     && c.ConversationType == ConversationType.PatientFacing.ToString()
                     && c.Participants.Any(p => p.UserId == userId.Value))
            .Select(c => c.Id)
            .ToListAsync();

        if (patientFacingConvIds.Count == 0)
            return Ok(new UnreadCountDto { TotalUnread = 0, UnreadConversations = 0 });

        var unreadByConv = await db.Messages
            .Where(m => patientFacingConvIds.Contains(m.ConversationId)
                     && m.SenderId != userId.Value
                     && !m.Reads.Any(r => r.UserId == userId.Value))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new UnreadCountDto
        {
            TotalUnread = unreadByConv.Sum(x => x.Count),
            UnreadConversations = unreadByConv.Count(x => x.Count > 0)
        });
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns conversation detail for a given ID and userId (for patient portal).
    /// Assumes access has already been verified.
    /// </summary>
    private async Task<ActionResult<ConversationDetailDto>> GetConversationById(
        Guid conversationId, Guid userId, int page = 1, int pageSize = 50)
    {
        var conv = await db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conv == null) return NotFound();

        var messages = await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        // Mark all unread as read
        var unread = await db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != userId
                     && !m.Reads.Any(r => r.UserId == userId))
            .ToListAsync();

        foreach (var m in unread)
            db.MessageReads.Add(new MessageRead { MessageId = m.Id, UserId = userId, ReadAt = DateTime.UtcNow });

        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);
        if (participant != null) participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return Ok(new ConversationDetailDto
        {
            Id = conv.Id,
            Title = conv.Title,
            IsGroup = conv.IsGroup,
            ConversationType = conv.ConversationType,
            PatientId = conv.PatientId,
            PatientName = conv.Patient != null
                ? $"{conv.Patient.FirstName} {conv.Patient.LastName}".Trim()
                : null,
            PatientNumber = conv.Patient?.PatientNumber,
            PatientPhone = conv.Patient?.Phone,
            Participants = conv.Participants.Select(MapParticipant).ToList(),
            Messages = messages.Select(m => MapMessage(m, userId)).ToList(),
            CreatedAt = conv.CreatedAt
        });
    }

    /// <summary>
    /// Ensures the patient's linked User is a participant in their PatientFacing conversations.
    /// Handles the case where staff created the conversation before the patient logged in.
    /// </summary>
    private async Task EnsurePatientInPatientFacingConversationsAsync(Guid userId)
    {
        // SECURITY: Only PatientFacing conversations for this patient
        var patientFacingConvIds = await db.Conversations
            .Where(c => c.PatientId == PatientId && c.ConversationType == ConversationType.PatientFacing.ToString())
            .Select(c => c.Id)
            .ToListAsync();

        foreach (var convId in patientFacingConvIds)
        {
            var isParticipant = await db.ConversationParticipants
                .AnyAsync(cp => cp.ConversationId == convId && cp.UserId == userId);
            if (!isParticipant)
            {
                db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = convId,
                    UserId = userId,
                    IsAdmin = false,
                    LastReadAt = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static ConversationParticipantDto MapParticipant(ConversationParticipant cp) => new()
    {
        UserId = cp.UserId,
        Username = cp.User?.Username ?? "",
        DisplayName = cp.User?.Doctor?.Name ?? cp.User?.Username,
        Role = cp.User?.Role.ToString(),
        AvatarInitials = cp.User?.Doctor?.AvatarInitials,
        Color = cp.User?.Doctor?.Color,
        IsAdmin = cp.IsAdmin
    };

    private MessageDto MapMessage(Message m, Guid currentUserId) => new()
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
        ReplyToContent = m.ReplyTo?.Content?.Length > 100
            ? m.ReplyTo.Content[..100] + "..." : m.ReplyTo?.Content,
        ReplyToSenderName = m.ReplyTo?.Sender?.Doctor?.Name ?? m.ReplyTo?.Sender?.Username,
        IsSystemMessage = m.IsSystemMessage,
        IsReadByMe = m.Reads.Any(r => r.UserId == currentUserId),
        ReadCount = m.Reads.Count,
        CreatedAt = m.CreatedAt
    };

    // Keep backward-compatible overload used by GetConversation
    private MessageDto MapMessage(Message m) => MapMessage(m, LinkedUserId ?? Guid.Empty);
}
