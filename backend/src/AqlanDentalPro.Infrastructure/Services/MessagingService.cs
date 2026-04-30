using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

public class MessagingService(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications)
{
    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedAccessException();

    // ─── محادثاتي ──────────────────────────────────────────────────────────────
    public async Task<PaginatedResponse<ConversationListDto>> GetMyConversationsAsync(
        int page = 1, int pageSize = 20, string? search = null)
    {
        // Query conversations directly (not through participants) to allow Include
        var myConversationIds = await db.ConversationParticipants
            .Where(cp => cp.UserId == UserId)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        var query = db.Conversations
            .Where(c => myConversationIds.Contains(c.Id))
            .Include(c => c.Patient)
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.Doctor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Title.Contains(search) ||
                c.Participants.Any(p => p.User.Doctor != null && p.User.Doctor.Name.Contains(search)) ||
                c.Participants.Any(p => p.User.Username.Contains(search)));
        }

        var total = await query.CountAsync();
        var conversations = await query
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Batch fetch unread counts to avoid N+1
        var unreadCounts = await db.Messages
            .Where(m => myConversationIds.Contains(m.ConversationId)
                     && m.SenderId != UserId
                     && !m.Reads.Any(r => r.UserId == UserId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

        var result = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var dto = MapToListDto(conv);
            dto.UnreadCount = unreadCounts.GetValueOrDefault(conv.Id);
            result.Add(dto);
        }

        return new PaginatedResponse<ConversationListDto>
        {
            Data = result,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    // ─── تفاصيل محادثة ──────────────────────────────────────────────────────────
    public async Task<ConversationDetailDto?> GetConversationAsync(Guid conversationId, int page = 1, int pageSize = 50)
    {
        var conv = await db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.Doctor)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conv == null || !await IsParticipantAsync(conversationId))
            return null;

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
        await MarkAsReadAsync(conversationId);

        // Get patient info if this is a patient conversation
        string? patientName = null;
        string? patientPhone = null;
        if (conv.PatientId.HasValue)
        {
            var patient = await db.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == conv.PatientId.Value);
            if (patient != null)
            {
                patientName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim();
                patientPhone = patient.Phone;
            }
        }

        return new ConversationDetailDto
        {
            Id = conv.Id,
            Title = conv.Title,
            IsGroup = conv.IsGroup,
            ConversationType = conv.ConversationType.ToString(),
            PatientId = conv.PatientId,
            PatientName = patientName,
            PatientPhone = patientPhone,
            Participants = conv.Participants.Select(MapParticipantDto).ToList(),
            Messages = messages.Select(m => MapMessageDto(m)).ToList(),
            CreatedAt = conv.CreatedAt
        };
    }

    // ─── إنشاء محادثة ──────────────────────────────────────────────────────────
    public async Task<ConversationDetailDto> CreateConversationAsync(CreateConversationRequest request)
    {
        var participantIds = request.ParticipantIds.Distinct().ToList();

        // Validate messaging permissions for each participant
        foreach (var pid in participantIds)
        {
            if (pid != UserId && !await CanMessageUserAsync(pid))
                throw new UnauthorizedAccessException($"ليس لديك صلاحية مراسلة هذا المستخدم");
        }

        // Add current user if not included
        if (!participantIds.Contains(UserId))
            participantIds.Add(UserId);

        // For direct messages (2 participants), check if conversation already exists
        if (!request.IsGroup && participantIds.Count == 2)
        {
            var existing = await FindDirectConversationAsync(participantIds[0], participantIds[1]);
            if (existing != null)
                return (await GetConversationAsync(existing.Id))!;
        }

        var conv = new Conversation
        {
            Title = request.IsGroup
                ? (request.Title ?? "مجموعة جديدة")
                : await GenerateDirectTitleAsync(participantIds),
            IsGroup = request.IsGroup,
            CreatedBy = UserId,
        };

        await db.Conversations.AddAsync(conv);

        // Add participants
        foreach (var pid in participantIds)
        {
            await db.ConversationParticipants.AddAsync(new ConversationParticipant
            {
                ConversationId = conv.Id,
                UserId = pid,
                IsAdmin = pid == UserId
            });
        }

        // Add initial message if provided
        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            var msg = new Message
            {
                ConversationId = conv.Id,
                SenderId = UserId,
                Content = request.InitialMessage,
                IsSystemMessage = false
            };
            await db.Messages.AddAsync(msg);
            conv.LastMessageAt = DateTime.UtcNow;
            conv.LastMessagePreview = request.InitialMessage.Length > 200
                ? request.InitialMessage[..200] + "..."
                : request.InitialMessage;
        }

        await db.SaveChangesAsync();
        return (await GetConversationAsync(conv.Id))!;
    }

    // ─── إرسال رسالة ──────────────────────────────────────────────────────────
    public async Task<MessageDto> SendMessageAsync(Guid conversationId, SendMessageRequest request)
    {
        if (!await IsParticipantAsync(conversationId))
            throw new UnauthorizedAccessException("لست مشاركاً في هذه المحادثة");

        var msg = new Message
        {
            ConversationId = conversationId,
            SenderId = UserId,
            Content = request.Content,
            AttachmentUrl = request.AttachmentUrl,
            AttachmentName = request.AttachmentName,
            AttachmentType = request.AttachmentType,
            ReplyToId = request.ReplyToId,
        };

        await db.Messages.AddAsync(msg);

        // Update conversation preview
        var conv = await db.Conversations.FindAsync(conversationId);
        if (conv != null)
        {
            conv.LastMessageAt = DateTime.UtcNow;
            conv.LastMessagePreview = request.Content.Length > 200
                ? request.Content[..200] + "..."
                : request.Content;
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
        var senderName = loaded.Sender?.Doctor?.Name ?? loaded.Sender?.Username ?? "مستخدم";
        var otherParticipants = await db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != UserId)
            .Select(cp => cp.UserId)
            .ToListAsync();

        foreach (var pid in otherParticipants)
        {
            await notifications.NotifyAsync(pid, "message", "رسالة جديدة",
                $"رسالة جديدة من {senderName}", "Conversation", conversationId);
        }

        return MapMessageDto(loaded);
    }

    // ─── تحديد كمقروء ──────────────────────────────────────────────────────────
    public async Task MarkAsReadAsync(Guid conversationId)
    {
        var unreadMessages = await db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != UserId
                     && !m.Reads.Any(r => r.UserId == UserId))
            .ToListAsync();

        foreach (var msg in unreadMessages)
        {
            await db.MessageReads.AddAsync(new MessageRead
            {
                MessageId = msg.Id,
                UserId = UserId,
                ReadAt = DateTime.UtcNow
            });
        }

        // Update participant's LastReadAt
        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == UserId);
        if (participant != null)
            participant.LastReadAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }

    // ─── عدد غير المقروء ──────────────────────────────────────────────────────
    public async Task<UnreadCountDto> GetUnreadCountAsync()
    {
        var myConversationIds = await db.ConversationParticipants
            .Where(cp => cp.UserId == UserId)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        // Batch fetch all unread counts in one query
        var unreadByConv = await db.Messages
            .Where(m => myConversationIds.Contains(m.ConversationId)
                     && m.SenderId != UserId
                     && !m.Reads.Any(r => r.UserId == UserId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalUnread = unreadByConv.Sum(x => x.Count);
        var unreadConvs = unreadByConv.Count(x => x.Count > 0);

        return new UnreadCountDto
        {
            TotalUnread = totalUnread,
            UnreadConversations = unreadConvs
        };
    }

    // ─── حذف محادثة (مغادرة) ──────────────────────────────────────────────────
    public async Task LeaveConversationAsync(Guid conversationId)
    {
        var participant = await db.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == UserId);
        if (participant == null) return;

        db.ConversationParticipants.Remove(participant);

        // Add system message
        await db.Messages.AddAsync(new Message
        {
            ConversationId = conversationId,
            SenderId = UserId,
            Content = "غادر المستخدم المحادثة",
            IsSystemMessage = true
        });

        await db.SaveChangesAsync();
    }

    // ─── التحقق من صلاحية المراسلة ─────────────────────────────────────────────
    private async Task<bool> CanMessageUserAsync(Guid targetUserId)
    {
        var currentUser = await db.Users.Include(u => u.Doctor).FirstOrDefaultAsync(u => u.Id == UserId);
        var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId);
        if (currentUser == null || targetUser == null) return false;

        // Admin and BranchManager can message everyone
        if (currentUser.Role is UserRole.Admin or UserRole.BranchManager) return true;

        var targetRole = targetUser.Role;

        // All internal staff can message each other
        return currentUser.Role switch
        {
            UserRole.Orthodontist or UserRole.GeneralDentist or UserRole.OralSurgeon
                => targetRole is UserRole.Reception or UserRole.Accountant or UserRole.Admin
                    or UserRole.Orthodontist or UserRole.GeneralDentist or UserRole.OralSurgeon
                    or UserRole.Assistant or UserRole.BranchManager,
            UserRole.Reception
                => targetRole is UserRole.Orthodontist or UserRole.GeneralDentist or UserRole.OralSurgeon
                    or UserRole.Admin or UserRole.Accountant or UserRole.Assistant or UserRole.BranchManager,
            UserRole.Accountant
                => targetRole is UserRole.Admin or UserRole.Orthodontist or UserRole.GeneralDentist
                    or UserRole.OralSurgeon or UserRole.Reception or UserRole.Assistant or UserRole.BranchManager,
            UserRole.Assistant
                => targetRole is UserRole.Orthodontist or UserRole.GeneralDentist or UserRole.OralSurgeon
                    or UserRole.Reception or UserRole.Admin or UserRole.Accountant or UserRole.BranchManager,
            _ => false
        };
    }

    // ─── محادثة مع مريض ──────────────────────────────────────────────────────
    public async Task<ConversationDetailDto?> GetOrCreatePatientConversationAsync(Guid patientId)
    {
        // Find patient
        var patient = await db.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == patientId);
        if (patient == null) return null;

        // Look for existing conversation linked to this patient
        var existingConv = await db.Conversations
            .Include(c => c.Participants)
                .ThenInclude(p => p.User)
                    .ThenInclude(u => u.Doctor)
            .Where(c => c.PatientId == patientId && c.ConversationType == ConversationType.StaffToPatient)
            .FirstOrDefaultAsync();

        if (existingConv != null)
        {
            // Check if current user is already a participant
            var isParticipant = existingConv.Participants.Any(p => p.UserId == UserId);
            if (!isParticipant)
            {
                // Add current user as participant
                await db.ConversationParticipants.AddAsync(new ConversationParticipant
                {
                    ConversationId = existingConv.Id,
                    UserId = UserId,
                    IsAdmin = false
                });
                await db.SaveChangesAsync();
            }
            return await GetConversationAsync(existingConv.Id);
        }

        // Create new StaffToPatient conversation
        var patientName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim();
        var conv = new Conversation
        {
            Title = $"محادثة المريض - {patientName}",
            IsGroup = false,
            CreatedBy = UserId,
            PatientId = patientId,
            ConversationType = ConversationType.StaffToPatient,
            BranchId = patient.BranchId,
        };

        await db.Conversations.AddAsync(conv);

        // Add current user as participant (admin)
        await db.ConversationParticipants.AddAsync(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId = UserId,
            IsAdmin = true
        });

        // Add system message
        await db.Messages.AddAsync(new Message
        {
            ConversationId = conv.Id,
            SenderId = UserId,
            Content = $"تم إنشاء محادثة حول المريض: {patientName}",
            IsSystemMessage = true
        });

        await db.SaveChangesAsync();
        return await GetConversationAsync(conv.Id);
    }

    // ─── التحقق من صلاحية المراسلة (عام) ────────────────────────────────────────
    public async Task<bool> CanMessageUserPublicAsync(Guid targetUserId) => await CanMessageUserAsync(targetUserId);

    // ─── Private Helpers ─────────────────────────────────────────────────────
    private async Task<bool> IsParticipantAsync(Guid conversationId)
    {
        return await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == UserId);
    }

    private async Task<int> GetUnreadCountAsync(Guid conversationId)
    {
        return await db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != UserId
                     && !m.Reads.Any(r => r.UserId == UserId))
            .CountAsync();
    }

    private async Task<Conversation?> FindDirectConversationAsync(Guid userId1, Guid userId2)
    {
        return await db.Conversations
            .Include(c => c.Participants)
            .Where(c => !c.IsGroup
                     && c.Participants.Any(p => p.UserId == userId1)
                     && c.Participants.Any(p => p.UserId == userId2))
            .FirstOrDefaultAsync();
    }

    private async Task<string> GenerateDirectTitleAsync(List<Guid> participantIds)
    {
        var otherId = participantIds.FirstOrDefault(id => id != UserId);
        if (otherId == Guid.Empty) return "محادثة";

        var other = await db.Users
            .Include(u => u.Doctor)
            .FirstOrDefaultAsync(u => u.Id == otherId);

        return other?.Doctor?.Name ?? other?.Username ?? "محادثة";
    }

    private ConversationListDto MapToListDto(Conversation conv)
    {
        var otherParticipant = conv.Participants
            .FirstOrDefault(p => p.UserId != UserId);

        // For patient conversations, show patient info
        string displayTitle = conv.Title;
        if (conv.ConversationType == ConversationType.StaffToPatient && conv.Patient != null)
        {
            displayTitle = $"مريض: {conv.Patient.FirstName} {conv.Patient.LastName}";
        }
        else if (!conv.IsGroup && otherParticipant != null)
        {
            displayTitle = otherParticipant.User?.Doctor?.Name ?? otherParticipant.User?.Username ?? conv.Title;
        }

        return new ConversationListDto
        {
            Id = conv.Id,
            Title = displayTitle,
            IsGroup = conv.IsGroup,
            ConversationType = conv.ConversationType.ToString(),
            PatientId = conv.PatientId,
            LastMessageAt = conv.LastMessageAt,
            LastMessagePreview = conv.LastMessagePreview,
            OtherParticipant = otherParticipant != null ? MapParticipantDto(otherParticipant) : null,
            Participants = conv.Participants.Select(MapParticipantDto).ToList(),
        };
    }

    private ConversationParticipantDto MapParticipantDto(ConversationParticipant cp)
    {
        return new ConversationParticipantDto
        {
            UserId = cp.UserId,
            Username = cp.User?.Username ?? "",
            DisplayName = cp.User?.Doctor?.Name ?? cp.User?.Username,
            Role = cp.User?.Role.ToString(),
            AvatarInitials = cp.User?.Doctor?.AvatarInitials,
            Color = cp.User?.Doctor?.Color,
            IsAdmin = cp.IsAdmin
        };
    }

    private MessageDto MapMessageDto(Message msg)
    {
        return new MessageDto
        {
            Id = msg.Id,
            ConversationId = msg.ConversationId,
            SenderId = msg.SenderId,
            SenderName = msg.Sender?.Doctor?.Name ?? msg.Sender?.Username ?? "غير معروف",
            SenderInitials = msg.Sender?.Doctor?.AvatarInitials,
            SenderColor = msg.Sender?.Doctor?.Color,
            Content = msg.Content,
            AttachmentUrl = msg.AttachmentUrl,
            AttachmentName = msg.AttachmentName,
            AttachmentType = msg.AttachmentType,
            ReplyToId = msg.ReplyToId,
            ReplyToContent = msg.ReplyTo?.Content?.Length > 100
                ? msg.ReplyTo.Content[..100] + "..."
                : msg.ReplyTo?.Content,
            ReplyToSenderName = msg.ReplyTo?.Sender?.Doctor?.Name ?? msg.ReplyTo?.Sender?.Username,
            IsSystemMessage = msg.IsSystemMessage,
            IsReadByMe = msg.Reads.Any(r => r.UserId == UserId),
            ReadCount = msg.Reads.Count,
            CreatedAt = msg.CreatedAt
        };
    }
}
