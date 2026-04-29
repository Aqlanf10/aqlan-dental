using AqlanDentalPro.Application.DTOs.Messages;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Application.Services;

public class MessageService(
    IConversationRepository conversationRepo,
    IMessageRepository messageRepo,
    IGenericRepository<ConversationParticipant> participantRepo,
    ICurrentUserService currentUser,
    IGenericRepository<User> userRepo)  // We'll use AppDbContext directly
{
    private readonly IConversationRepository _conversationRepo = conversationRepo;
    private readonly IMessageRepository _messageRepo = messageRepo;
    private readonly IGenericRepository<ConversationParticipant> _participantRepo = participantRepo;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly IGenericRepository<User> _userRepo = userRepo;

    // ── Get all conversations for current user ──────────────────────────
    public async Task<List<ConversationDto>> GetConversationsAsync()
    {
        var userId = _currentUser.UserId ?? Guid.Empty;
        var conversations = await _conversationRepo.GetUserConversationsAsync(userId);

        var result = new List<ConversationDto>();
        foreach (var conv in conversations)
        {
            var unread = await _messageRepo.GetUnreadCountAsync(conv.Id, userId);
            var lastMsg = conv.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

            result.Add(new ConversationDto
            {
                Id = conv.Id,
                Title = conv.Title,
                Type = conv.Type.ToString(),
                CreatedBy = conv.CreatedBy,
                CreatedAt = conv.CreatedAt,
                UpdatedAt = conv.UpdatedAt,
                Participants = conv.Participants.Select(p => new ConversationParticipantDto
                {
                    UserId = p.UserId,
                    Username = p.User.Username,
                    FullName = p.User.Doctor != null
                        ? $"د. {p.User.Doctor.Name}"
                        : p.User.Username,
                    Role = p.User.Role.ToString(),
                    LastReadAt = p.LastReadAt,
                }).ToList(),
                LastMessage = lastMsg != null ? new LastMessageDto
                {
                    Id = lastMsg.Id,
                    SenderId = lastMsg.SenderId,
                    SenderName = lastMsg.Sender.Doctor != null
                        ? $"د. {lastMsg.Sender.Doctor.Name}"
                        : lastMsg.Sender.Username,
                    Content = lastMsg.Content,
                    Type = lastMsg.Type.ToString(),
                    CreatedAt = lastMsg.CreatedAt,
                } : null,
                UnreadCount = unread,
            });
        }

        return result;
    }

    // ── Create a new conversation ───────────────────────────────────────
    public async Task<(ConversationDto? result, string? error)> CreateConversationAsync(CreateConversationRequest req)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        // Direct message
        if (req.RecipientId.HasValue && req.Type != "Group")
        {
            var recipientId = req.RecipientId.Value;
            if (recipientId == userId)
                return (null, "لا يمكنك إرسال رسالة لنفسك");

            // Check if direct conversation already exists
            var existing = await _conversationRepo.GetDirectConversationAsync(userId, recipientId);
            if (existing != null)
            {
                // If there's an initial message, add it
                if (!string.IsNullOrWhiteSpace(req.InitialMessage))
                {
                    var msg = new Message
                    {
                        ConversationId = existing.Id,
                        SenderId = userId,
                        Content = req.InitialMessage,
                        Type = MessageType.Text,
                    };
                    await _messageRepo.AddAsync(msg);
                    await _messageRepo.SaveChangesAsync();
                }
                return (await GetConversationDtoAsync(existing.Id), null);
            }

            var conversation = new Conversation
            {
                Type = ConversationType.Direct,
                CreatedBy = userId,
            };
            await _conversationRepo.AddAsync(conversation);
            await _conversationRepo.SaveChangesAsync();

            // Add participants
            await _participantRepo.AddAsync(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId,
            });
            await _participantRepo.AddAsync(new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = recipientId,
            });
            await _participantRepo.SaveChangesAsync();

            // Add initial message if provided
            if (!string.IsNullOrWhiteSpace(req.InitialMessage))
            {
                var msg = new Message
                {
                    ConversationId = conversation.Id,
                    SenderId = userId,
                    Content = req.InitialMessage,
                    Type = MessageType.Text,
                };
                await _messageRepo.AddAsync(msg);
                await _messageRepo.SaveChangesAsync();
            }

            return (await GetConversationDtoAsync(conversation.Id), null);
        }

        // Group conversation
        if (req.ParticipantIds != null && req.ParticipantIds.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(req.Title))
                return (null, "يجب تحديد عنوان للمحادثة الجماعية");

            var allParticipantIds = req.ParticipantIds.Concat([userId]).Distinct().ToList();
            if (allParticipantIds.Count < 2)
                return (null, "يجب أن تكون المحادثة بين شخصين على الأقل");

            var conversation = new Conversation
            {
                Title = req.Title,
                Type = ConversationType.Group,
                CreatedBy = userId,
            };
            await _conversationRepo.AddAsync(conversation);
            await _conversationRepo.SaveChangesAsync();

            foreach (var pid in allParticipantIds)
            {
                await _participantRepo.AddAsync(new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = pid,
                });
            }
            await _participantRepo.SaveChangesAsync();

            // Add initial message if provided
            if (!string.IsNullOrWhiteSpace(req.InitialMessage))
            {
                var msg = new Message
                {
                    ConversationId = conversation.Id,
                    SenderId = userId,
                    Content = req.InitialMessage,
                    Type = MessageType.Text,
                };
                await _messageRepo.AddAsync(msg);
                await _messageRepo.SaveChangesAsync();
            }

            return (await GetConversationDtoAsync(conversation.Id), null);
        }

        return (null, "يجب تحديد المستلم أو قائمة المشاركين");
    }

    // ── Get messages in a conversation ──────────────────────────────────
    public async Task<(List<MessageDto>? result, string? error)> GetMessagesAsync(Guid conversationId, int page = 1, int pageSize = 50)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        // Verify user is participant
        var conv = await _conversationRepo.GetWithParticipantsAsync(conversationId);
        if (conv == null || !conv.Participants.Any(p => p.UserId == userId))
            return (null, "ليس لديك صلاحية الوصول لهذه المحادثة");

        var messages = await _messageRepo.GetConversationMessagesAsync(conversationId, page, pageSize);

        return (messages.Select(m => new MessageDto
        {
            Id = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            SenderName = m.Sender.Doctor != null
                ? $"د. {m.Sender.Doctor.Name}"
                : m.Sender.Username,
            SenderRole = m.Sender.Role.ToString(),
            Content = m.Content,
            Type = m.Type.ToString(),
            CreatedAt = m.CreatedAt,
        }).Reverse().ToList(), null); // Reverse to show oldest first
    }

    // ── Send a message ──────────────────────────────────────────────────
    public async Task<(MessageDto? result, string? error)> SendMessageAsync(Guid conversationId, SendMessageRequest req)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        // Verify user is participant
        var conv = await _conversationRepo.GetWithParticipantsAsync(conversationId);
        if (conv == null || !conv.Participants.Any(p => p.UserId == userId))
            return (null, "ليس لديك صلاحية الوصول لهذه المحادثة");

        if (string.IsNullOrWhiteSpace(req.Content))
            return (null, "لا يمكن إرسال رسالة فارغة");

        var messageType = Enum.TryParse<MessageType>(req.Type, out var mt) ? mt : MessageType.Text;

        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Content = req.Content,
            Type = messageType,
        };
        await _messageRepo.AddAsync(message);
        await _messageRepo.SaveChangesAsync();

        // Update conversation's UpdatedAt to move it to top of list
        conv.UpdatedAt = DateTime.UtcNow;
        _conversationRepo.Update(conv);
        await _conversationRepo.SaveChangesAsync();

        // Load sender for DTO
        var sender = await _userRepo.GetByIdAsync(userId);

        return (new MessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderName = sender?.Doctor != null
                ? $"د. {sender.Doctor.Name}"
                : sender?.Username ?? "",
            SenderRole = sender?.Role.ToString() ?? "",
            Content = message.Content,
            Type = message.Type.ToString(),
            CreatedAt = message.CreatedAt,
        }, null);
    }

    // ── Mark conversation as read ───────────────────────────────────────
    public async Task<(bool success, string? error)> MarkAsReadAsync(Guid conversationId)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;

        var participant = await _participantRepo.FindAsync(
            p => p.ConversationId == conversationId && p.UserId == userId);
        var cp = participant.FirstOrDefault();
        if (cp == null)
            return (false, "ليس لديك صلاحية الوصول لهذه المحادثة");

        cp.LastReadAt = DateTime.UtcNow;
        _participantRepo.Update(cp);
        await _participantRepo.SaveChangesAsync();
        return (true, null);
    }

    // ── Get total unread count ──────────────────────────────────────────
    public async Task<int> GetTotalUnreadCountAsync()
    {
        var userId = _currentUser.UserId ?? Guid.Empty;
        return await _messageRepo.GetTotalUnreadCountAsync(userId);
    }

    // ── Get list of doctors/staff for new conversation ──────────────────
    public async Task<List<DoctorForMessagingDto>> GetDoctorsForMessagingAsync()
    {
        var users = await _userRepo.GetAllAsync();
        return users
            .Where(u => u.IsActive && u.Role != UserRole.Admin) // Exclude admin from list, or include?
            .Select(u => new DoctorForMessagingDto
            {
                UserId = u.Id,
                Username = u.Username,
                FullName = u.Doctor != null ? $"د. {u.Doctor.Name}" : u.Username,
                Role = u.Role.ToString(),
                Specialty = u.Doctor?.Specialty,
            })
            .OrderBy(u => u.FullName)
            .ToList();
    }

    // ── Helper: Get ConversationDto by Id ───────────────────────────────
    private async Task<ConversationDto> GetConversationDtoAsync(Guid conversationId)
    {
        var userId = _currentUser.UserId ?? Guid.Empty;
        var conv = await _conversationRepo.GetWithParticipantsAsync(conversationId);
        var unread = await _messageRepo.GetUnreadCountAsync(conversationId, userId);
        var lastMsg = conv?.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

        return new ConversationDto
        {
            Id = conv!.Id,
            Title = conv.Title,
            Type = conv.Type.ToString(),
            CreatedBy = conv.CreatedBy,
            CreatedAt = conv.CreatedAt,
            UpdatedAt = conv.UpdatedAt,
            Participants = conv.Participants.Select(p => new ConversationParticipantDto
            {
                UserId = p.UserId,
                Username = p.User.Username,
                FullName = p.User.Doctor != null
                    ? $"د. {p.User.Doctor.Name}"
                    : p.User.Username,
                Role = p.User.Role.ToString(),
                LastReadAt = p.LastReadAt,
            }).ToList(),
            LastMessage = lastMsg != null ? new LastMessageDto
            {
                Id = lastMsg.Id,
                SenderId = lastMsg.SenderId,
                SenderName = lastMsg.Sender.Doctor != null
                    ? $"د. {lastMsg.Sender.Doctor.Name}"
                    : lastMsg.Sender.Username,
                Content = lastMsg.Content,
                Type = lastMsg.Type.ToString(),
                CreatedAt = lastMsg.CreatedAt,
            } : null,
            UnreadCount = unread,
        };
    }
}
