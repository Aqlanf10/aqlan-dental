using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Business logic for patient portal messaging.
/// Extracted from PatientPortalMessagesController to enable reuse, testability, and clean separation.
/// </summary>
public class PatientPortalMessagingService : IPatientPortalMessagingService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IRealTimePushService _pushService;
    private readonly IPatientAccountLinkingService _linkingService;
    private readonly ILogger<PatientPortalMessagingService> _logger;

    private static readonly HashSet<string> ValidRecipientTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TreatingDoctor", "Reception", "Admin"
    };

    public PatientPortalMessagingService(
        AppDbContext db,
        INotificationService notifications,
        IRealTimePushService pushService,
        IPatientAccountLinkingService linkingService,
        ILogger<PatientPortalMessagingService> logger)
    {
        _db = db;
        _notifications = notifications;
        _pushService = pushService;
        _linkingService = linkingService;
        _logger = logger;
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Guid?> EnsureLinkedUserAsync(Guid patientId)
        => _linkingService.EnsureLinkedUserAsync(patientId);

    /// <inheritdoc />
    public async Task<List<PortalRecipientDto>> GetRecipientsAsync(Guid patientId)
    {
        var patient = await _db.Patients
            .Include(p => p.PrimaryDoctor)
                .ThenInclude(d => d!.User)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        var recipients = new List<PortalRecipientDto>();

        if (patient?.PrimaryDoctorId != null && patient.PrimaryDoctor != null)
        {
            var doctor = patient.PrimaryDoctor;
            var doctorUser = doctor.User;
            recipients.Add(new PortalRecipientDto
            {
                Type = "TreatingDoctor",
                UserId = doctor.UserId,
                DisplayName = $"د. {doctor.Name}",
                Role = doctorUser?.Role.ToString() ?? "Doctor",
                AvatarInitials = doctor.AvatarInitials ?? (!string.IsNullOrEmpty(doctor.Name) ? doctor.Name.Substring(0, 1) : null),
                Color = doctor.Color ?? "#0d9488"
            });
        }

        // Reception — find active reception staff
        var receptionUser = await _db.Users
            .Where(u => u.Role == UserRole.Reception && u.IsActive)
            .FirstOrDefaultAsync();
        recipients.Add(new PortalRecipientDto
        {
            Type = "Reception",
            UserId = receptionUser?.Id,
            DisplayName = "الاستقبال",
            Role = receptionUser?.Role.ToString() ?? "Reception"
        });

        // Admin / Support
        var adminUser = await _db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .FirstOrDefaultAsync();
        recipients.Add(new PortalRecipientDto
        {
            Type = "Admin",
            UserId = adminUser?.Id,
            DisplayName = "الإدارة / الدعم",
            Role = adminUser?.Role.ToString() ?? "Admin"
        });

        return recipients;
    }

    /// <inheritdoc />
    public async Task<(object Result, int TotalCount)> GetConversationsAsync(
        Guid patientId, Guid userId, int page, int pageSize, string? search)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        // Ensure patient is a participant in their PatientFacing conversations
        await EnsurePatientInPatientFacingConversationsAsync(patientId, userId);

        // SECURITY: Only PatientFacing conversations for this patient where patient is a participant
        var query = _db.Conversations
            .Where(c => c.ConversationType == ConversationType.PatientFacing.ToString()
                      && c.PatientId == patientId
                      && c.Participants.Any(p => p.UserId == userId))
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

        // N+1 FIX: Batch unread counts — single DB query instead of per-conversation queries
        var convIds = conversations.Select(c => c.Id).ToList();
        var unreadCounts = convIds.Count > 0
            ? await _db.Messages
                .Where(m => convIds.Contains(m.ConversationId)
                         && m.SenderId != userId
                         && !m.Reads.Any(r => r.UserId == userId))
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count)
            : new Dictionary<Guid, int>();

        var result = new List<ConversationListDto>();
        foreach (var conv in conversations)
        {
            var otherParticipant = conv.Participants.FirstOrDefault(p => p.UserId != userId);
            var dto = new ConversationListDto
            {
                Id = conv.Id,
                Title = BuildConversationTitle(conv, otherParticipant),
                IsGroup = conv.IsGroup,
                ConversationType = conv.ConversationType,
                PatientId = conv.PatientId,
                LastMessageAt = conv.LastMessageAt,
                LastMessagePreview = conv.LastMessagePreview,
                UnreadCount = unreadCounts.GetValueOrDefault(conv.Id),
                OtherParticipant = otherParticipant != null ? MapParticipant(otherParticipant) : null,
                Participants = conv.Participants.Select(MapParticipant).ToList(),
                RecipientType = conv.RecipientType,
                RecipientUserId = conv.RecipientUserId
            };
            result.Add(dto);
        }

        return (new
        {
            data = result,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling((double)total / pageSize)
        }, total);
    }

    /// <inheritdoc />
    public async Task<ConversationDetailDto> StartConversationAsync(
        Guid patientId, Guid userId, StartConversationRequest? request)
    {
        var recipientType = request?.RecipientType ?? "Admin";
        var recipientUserId = request?.RecipientUserId;

        // SECURITY: Validate recipientType
        if (!ValidRecipientTypes.Contains(recipientType))
            throw new ServiceException($"نوع المستلم غير صالح: {recipientType}. القيم المسموحة: TreatingDoctor, Reception, Admin", 400);

        // SECURITY: Prevent TreatingDoctor conversation when no primary doctor is assigned
        if (recipientType.Equals("TreatingDoctor", StringComparison.OrdinalIgnoreCase))
        {
            var patientForCheck = await _db.Patients
                .Include(p => p.PrimaryDoctor)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patientForCheck?.PrimaryDoctorId == null || patientForCheck.PrimaryDoctor == null)
                throw new ServiceException("لم يتم تحديد الطبيب المسؤول لهذا المريض", 400);

            // Auto-resolve the doctor's UserId if not explicitly provided
            if (!recipientUserId.HasValue)
                recipientUserId = patientForCheck.PrimaryDoctor.UserId;
        }

        // SECURITY: Validate recipientUserId if provided
        if (recipientUserId.HasValue)
        {
            var allowedUser = await _db.Users
                .Include(u => u.Doctor)
                .Where(u => u.Id == recipientUserId.Value && u.IsActive)
                .FirstOrDefaultAsync();
            if (allowedUser == null)
                throw new ServiceException("المستلم المحدد غير موجود أو غير نشط", 400);

            // SECURITY: Verify the recipientUserId matches the recipientType
            if (recipientType.Equals("TreatingDoctor", StringComparison.OrdinalIgnoreCase))
            {
                var isDoctor = allowedUser.Role == UserRole.Orthodontist
                            || allowedUser.Role == UserRole.GeneralDentist
                            || allowedUser.Role == UserRole.OralSurgeon
                            || allowedUser.Doctor != null;
                if (!isDoctor)
                    throw new ServiceException("المستلم المحدد ليس طبيباً", 400);
            }
        }

        // Check if a PatientFacing conversation with the same recipient type already exists for this patient
        var existingConv = await _db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c =>
                c.PatientId == patientId &&
                c.ConversationType == ConversationType.PatientFacing.ToString() &&
                c.RecipientType == recipientType);

        // Backward compatibility: If no conversation found with recipient type,
        // check for legacy conversations without a recipient type
        if (existingConv == null && !string.IsNullOrEmpty(recipientType))
        {
            var legacyConv = await _db.Conversations
                .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
                .Include(c => c.Patient)
                .FirstOrDefaultAsync(c =>
                    c.PatientId == patientId &&
                    c.ConversationType == ConversationType.PatientFacing.ToString() &&
                    c.RecipientType == null);

            if (legacyConv != null)
            {
                // Migrate legacy conversation to have a recipient type
                legacyConv.RecipientType = recipientType;
                existingConv = legacyConv;
            }
        }

        if (existingConv != null)
        {
            var isAlreadyParticipant = existingConv.Participants.Any(p => p.UserId == userId);
            if (!isAlreadyParticipant)
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = existingConv.Id,
                    UserId = userId,
                    IsAdmin = false,
                    LastReadAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
            }

            if (!string.IsNullOrWhiteSpace(request?.InitialMessage))
            {
                _db.Messages.Add(new Message
                {
                    ConversationId = existingConv.Id,
                    SenderId = userId,
                    Content = request.InitialMessage.Trim()
                });
                existingConv.LastMessageAt = DateTime.UtcNow;
                existingConv.LastMessagePreview = request.InitialMessage.Length > 200
                    ? request.InitialMessage[..200] + "..." : request.InitialMessage;
                await _db.SaveChangesAsync();
            }

            return await BuildConversationDetailAsync(existingConv.Id, userId);
        }

        // Create a new PatientFacing conversation
        var patient = await _db.Patients
            .Include(p => p.PrimaryDoctor)
            .FirstOrDefaultAsync(p => p.Id == patientId);

        var convTitle = BuildNewConversationTitle(recipientType, patient);
        var conv = new Conversation
        {
            Title = convTitle,
            IsGroup = true,
            ConversationType = ConversationType.PatientFacing.ToString(), // SECURITY: Always PatientFacing
            PatientId = patientId,
            CreatedBy = userId,
            LastMessageAt = DateTime.UtcNow,
            RecipientType = recipientType,
            RecipientUserId = recipientUserId
        };

        _db.Conversations.Add(conv);
        await _db.SaveChangesAsync();

        // Add patient as participant
        _db.ConversationParticipants.Add(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId = userId,
            IsAdmin = false,
            LastReadAt = DateTime.UtcNow
        });

        // Add participants based on recipient type
        await AddRecipientsAsParticipantsAsync(conv.Id, recipientType, recipientUserId, patient);

        // System message clarifying the conversation target
        var recipientLabel = GetRecipientTypeLabelArabic(recipientType);
        _db.Messages.Add(new Message
        {
            ConversationId = conv.Id,
            SenderId = userId,
            Content = $"بدأ المريض {patient?.FirstName} {patient?.LastName} ({patient?.PatientNumber}) محادثة من البوابة — موجهة إلى: {recipientLabel}",
            IsSystemMessage = true
        });

        if (!string.IsNullOrWhiteSpace(request?.InitialMessage))
        {
            _db.Messages.Add(new Message
            {
                ConversationId = conv.Id,
                SenderId = userId,
                Content = request.InitialMessage.Trim()
            });
            conv.LastMessagePreview = request.InitialMessage.Length > 200
                ? request.InitialMessage[..200] + "..." : request.InitialMessage;
        }

        await _db.SaveChangesAsync();
        return await BuildConversationDetailAsync(conv.Id, userId);
    }

    /// <inheritdoc />
    public async Task<ConversationDetailDto?> GetConversationAsync(
        Guid patientId, Guid userId, Guid conversationId, int page, int pageSize)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));

        var accessResult = await VerifyPatientFacingAccessAsync(patientId, userId, conversationId);
        if (!accessResult.IsAuthorized)
            return null;

        return await BuildConversationDetailAsync(conversationId, userId, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<MessageDto> SendMessageAsync(
        Guid patientId, Guid userId, Guid conversationId, SendMessageRequest request)
    {
        var accessResult = await VerifyPatientFacingAccessAsync(patientId, userId, conversationId);
        if (!accessResult.IsAuthorized)
            throw new ServiceException(accessResult.ErrorMessage!, accessResult.StatusCode);

        var conv = accessResult.Conversation!;
        var content = request.Content?.Trim() ?? string.Empty;
        var hasLegacyAttachment = !string.IsNullOrWhiteSpace(request.AttachmentUrl);
        var hasMultiAttachments = request.Attachments != null && request.Attachments.Count > 0;

        // Allow attachment-only messages: either Content, AttachmentUrl, or Attachments must be present
        if (string.IsNullOrWhiteSpace(content) && !hasLegacyAttachment && !hasMultiAttachments)
            throw new ServiceException("يجب أن تحتوي الرسالة على نص أو مرفق", 400);
        if (content.Length > 2000)
            throw new ServiceException("محتوى الرسالة طويل جداً، الحد الأقصى 2000 حرف", 400);

        // Validate legacy single attachment if provided
        if (hasLegacyAttachment)
        {
            if (!request.AttachmentUrl!.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                throw new ServiceException("رابط المرفق غير صالح", 400);

            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/png", "application/pdf",
                "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav"
            };
            if (string.IsNullOrWhiteSpace(request.AttachmentType) || !allowedMimeTypes.Contains(request.AttachmentType))
                throw new ServiceException("نوع المرفق غير مدعوم. الأنواع المسموحة: صور JPEG، صور PNG، ملفات PDF، رسائل صوتية", 400);
        }

        // Validate multi-attachments if provided
        if (hasMultiAttachments)
        {
            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/png", "application/pdf",
                "audio/webm", "audio/ogg", "audio/mp4", "audio/mpeg", "audio/wav"
            };
            foreach (var att in request.Attachments!)
            {
                if (string.IsNullOrWhiteSpace(att.FileUrl) || !att.FileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                    throw new ServiceException($"رابط المرفق '{att.FileName}' غير صالح", 400);
                if (string.IsNullOrWhiteSpace(att.MimeType) || !allowedMimeTypes.Contains(att.MimeType))
                    throw new ServiceException($"نوع المرفق '{att.FileName}' غير مدعوم", 400);
                if (att.FileSize > 10 * 1024 * 1024)
                    throw new ServiceException($"حجم المرفق '{att.FileName}' يتجاوز الحد الأقصى (10 ميجابايت)", 400);
            }
        }

        var msg = new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Content = content,
            AttachmentUrl = request.AttachmentUrl,
            AttachmentName = request.AttachmentName,
            AttachmentType = request.AttachmentType,
            ReplyToId = request.ReplyToId,
        };

        _db.Messages.Add(msg);

        // Save multi-attachments
        if (hasMultiAttachments)
        {
            foreach (var att in request.Attachments!)
            {
                _db.MessageAttachments.Add(new MessageAttachment
                {
                    MessageId = msg.Id,
                    FileUrl = att.FileUrl,
                    FileName = att.FileName,
                    FileSize = att.FileSize,
                    MimeType = att.MimeType
                });
            }
        }

        conv.LastMessageAt = DateTime.UtcNow;
        var previewText = !string.IsNullOrWhiteSpace(content) ? content
            : (!string.IsNullOrWhiteSpace(request.AttachmentName) ? "📎 " + request.AttachmentName : "مرفق");
        conv.LastMessagePreview = previewText.Length > 200
            ? previewText[..200] + "..." : previewText;

        await _db.SaveChangesAsync();

        var loaded = await _db.Messages
            .Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo)
            .Include(m => m.Attachments)
            .FirstAsync(m => m.Id == msg.Id);

        var senderName = loaded.Sender?.Doctor?.Name ?? loaded.Sender?.Username ?? "مريض";
        var otherParticipants = await _db.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != userId)
            .Select(cp => cp.UserId)
            .ToListAsync();

        // N+1 FIX: Send notifications in parallel instead of sequential await per participant
        var notificationTasks = otherParticipants.Select(pid =>
            _notifications.NotifyAsync(pid, "message", "رسالة جديدة من مريض",
                $"رسالة من {senderName}", "Conversation", conversationId));
        await Task.WhenAll(notificationTasks);

        // SignalR: دفع الرسالة الجديدة لمشاركي المحادثة
        var messageDto = MapMessage(loaded, userId);
        await _pushService.PushToConversationAsync(conversationId, "NewMessage", messageDto);

        // SignalR: تحديث عدد غير المقروء لكل مشارك
        var pushUnreadTasks = otherParticipants.Select(pid =>
            _pushService.PushToUserAsync(pid, "UnreadCountUpdated", new { conversationId }));
        await Task.WhenAll(pushUnreadTasks);

        return messageDto;
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid patientId, Guid userId, Guid conversationId)
    {
        var accessResult = await VerifyPatientFacingAccessAsync(patientId, userId, conversationId);
        if (!accessResult.IsAuthorized)
            return; // Silently succeed on auth failure for read (idempotent)

        try
        {
            var unread = await _db.Messages
                .Where(m => m.ConversationId == conversationId
                         && m.SenderId != userId
                         && !m.Reads.Any(r => r.UserId == userId))
                .ToListAsync();

            foreach (var m in unread)
                _db.MessageReads.Add(new MessageRead { MessageId = m.Id, UserId = userId, ReadAt = DateTime.UtcNow });

            var participant = await _db.ConversationParticipants
                .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);
            if (participant != null) participant.LastReadAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent mark-as-read already inserted these rows — idempotent success
            _logger.LogDebug(ex, "Concurrent mark-as-read deduplication for conversation {ConversationId}", conversationId);
        }
    }

    /// <inheritdoc />
    public async Task<UnreadCountDto> GetUnreadCountAsync(Guid patientId, Guid userId)
    {
        // SECURITY: Only count PatientFacing conversations belonging to this patient
        var patientFacingConvIds = await _db.Conversations
            .Where(c => c.PatientId == patientId
                     && c.ConversationType == ConversationType.PatientFacing.ToString()
                     && c.Participants.Any(p => p.UserId == userId))
            .Select(c => c.Id)
            .ToListAsync();

        if (patientFacingConvIds.Count == 0)
            return new UnreadCountDto { TotalUnread = 0, UnreadConversations = 0 };

        var unreadByConv = await _db.Messages
            .Where(m => patientFacingConvIds.Contains(m.ConversationId)
                     && m.SenderId != userId
                     && !m.Reads.Any(r => r.UserId == userId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync();

        return new UnreadCountDto
        {
            TotalUnread = unreadByConv.Sum(x => x.Count),
            UnreadConversations = unreadByConv.Count(x => x.Count > 0)
        };
    }

    // ─── Access Verification ─────────────────────────────────────────────────

    /// <summary>
    /// Verifies a conversation is PatientFacing AND belongs to this patient AND patient is a participant.
    /// Returns a result object indicating authorization status.
    /// </summary>
    private async Task<AccessVerificationResult> VerifyPatientFacingAccessAsync(
        Guid patientId, Guid userId, Guid conversationId)
    {
        var conv = await _db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        // Conversation must exist
        if (conv == null)
            return AccessVerificationResult.Fail("المحادثة غير موجودة", 404);

        // SECURITY: Must be PatientFacing — never expose internal staff conversations
        if (conv.ConversationType != ConversationType.PatientFacing.ToString())
            return AccessVerificationResult.Fail("المحادثة غير موجودة", 404);

        // SECURITY: Must belong to this patient
        if (conv.PatientId != patientId)
            return AccessVerificationResult.Fail("ليس لديك صلاحية الوصول لهذه المحادثة", 403);

        // SECURITY: Patient must be a participant
        var isParticipant = conv.Participants.Any(p => p.UserId == userId);
        if (!isParticipant)
            return AccessVerificationResult.Fail("لست مشاركاً في هذه المحادثة", 403);

        return AccessVerificationResult.Success(conv);
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Builds a ConversationDetailDto for a given conversation ID and userId.
    /// Assumes access has already been verified.
    /// </summary>
    private async Task<ConversationDetailDto> BuildConversationDetailAsync(
        Guid conversationId, Guid userId, int page = 1, int pageSize = 50)
    {
        pageSize = Math.Max(1, Math.Min(pageSize, 100));
        var conv = await _db.Conversations
            .Include(c => c.Participants).ThenInclude(p => p.User).ThenInclude(u => u.Doctor)
            .Include(c => c.Patient)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conv == null)
            throw new ServiceException("المحادثة غير موجودة", 404);

        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo)
            .Include(m => m.Attachments)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        return new ConversationDetailDto
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
            CreatedAt = conv.CreatedAt,
            RecipientType = conv.RecipientType,
            RecipientUserId = conv.RecipientUserId
        };
    }

    /// <summary>
    /// Ensures the patient's linked User is a participant in their PatientFacing conversations.
    /// Handles the case where staff created the conversation before the patient logged in.
    /// </summary>
    private async Task EnsurePatientInPatientFacingConversationsAsync(Guid patientId, Guid userId)
    {
        // SECURITY: Only PatientFacing conversations for this patient
        var patientFacingConvIds = await _db.Conversations
            .Where(c => c.PatientId == patientId && c.ConversationType == ConversationType.PatientFacing.ToString())
            .Select(c => c.Id)
            .ToListAsync();

        // Batch check: find which conversations already have this user as participant
        var existingParticipantConvIds = (await _db.ConversationParticipants
            .Where(cp => patientFacingConvIds.Contains(cp.ConversationId) && cp.UserId == userId)
            .Select(cp => cp.ConversationId)
            .ToListAsync())
            .ToHashSet();

        foreach (var convId in patientFacingConvIds)
        {
            if (!existingParticipantConvIds.Contains(convId))
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = convId,
                    UserId = userId,
                    IsAdmin = false,
                    LastReadAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds the appropriate staff participants based on the recipient type.
    /// TreatingDoctor: only the patient's primary doctor
    /// Reception: only reception staff
    /// Admin: admin users
    /// Always adds admin users as fallback participants for visibility.
    /// </summary>
    private async Task AddRecipientsAsParticipantsAsync(
        Guid conversationId,
        string recipientType,
        Guid? recipientUserId,
        Patient? patient)
    {
        var addedUserIds = new HashSet<Guid>();

        switch (recipientType.ToLowerInvariant())
        {
            case "treatingdoctor":
                // Add the specific treating doctor
                if (recipientUserId.HasValue)
                {
                    _db.ConversationParticipants.Add(new ConversationParticipant
                    {
                        ConversationId = conversationId,
                        UserId = recipientUserId.Value,
                        IsAdmin = false
                    });
                    addedUserIds.Add(recipientUserId.Value);
                }
                else if (patient?.PrimaryDoctorId != null)
                {
                    var doctorUserId = await _db.Doctors
                        .Where(d => d.Id == patient.PrimaryDoctorId)
                        .Select(d => d.UserId)
                        .FirstOrDefaultAsync();

                    if (doctorUserId != Guid.Empty)
                    {
                        _db.ConversationParticipants.Add(new ConversationParticipant
                        {
                            ConversationId = conversationId,
                            UserId = doctorUserId,
                            IsAdmin = false
                        });
                        addedUserIds.Add(doctorUserId);
                    }
                }
                // Also add admin users for visibility
                await AddAdminParticipantsAsync(conversationId, addedUserIds);
                break;

            case "reception":
                // Add reception staff
                var receptionUsers = await _db.Users
                    .Where(u => u.Role == UserRole.Reception && u.IsActive)
                    .ToListAsync();
                foreach (var receptionist in receptionUsers)
                {
                    if (addedUserIds.Add(receptionist.Id))
                    {
                        _db.ConversationParticipants.Add(new ConversationParticipant
                        {
                            ConversationId = conversationId,
                            UserId = receptionist.Id,
                            IsAdmin = false
                        });
                    }
                }
                // Also add admin users for visibility
                await AddAdminParticipantsAsync(conversationId, addedUserIds);
                break;

            case "admin":
                // Add admin users as primary recipients
                var adminUsers = await _db.Users
                    .Where(u => u.Role == UserRole.Admin && u.IsActive)
                    .ToListAsync();
                foreach (var admin in adminUsers)
                {
                    if (addedUserIds.Add(admin.Id))
                    {
                        _db.ConversationParticipants.Add(new ConversationParticipant
                        {
                            ConversationId = conversationId,
                            UserId = admin.Id,
                            IsAdmin = true
                        });
                    }
                }
                break;
        }
    }

    private async Task AddAdminParticipantsAsync(Guid conversationId, HashSet<Guid> addedUserIds)
    {
        var adminUsers = await _db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive)
            .ToListAsync();
        foreach (var admin in adminUsers)
        {
            if (addedUserIds.Add(admin.Id))
            {
                _db.ConversationParticipants.Add(new ConversationParticipant
                {
                    ConversationId = conversationId,
                    UserId = admin.Id,
                    IsAdmin = true
                });
            }
        }
    }

    private static string GetRecipientTypeLabelArabic(string recipientType) => recipientType.ToLowerInvariant() switch
    {
        "treatingdoctor" => "الطبيب المسؤول",
        "reception" => "الاستقبال",
        "admin" => "الإدارة / الدعم",
        _ => "المركز"
    };

    private static string BuildConversationTitle(Conversation conv, ConversationParticipant? otherParticipant)
    {
        // If conversation has a recipient type, use it for the title
        if (!string.IsNullOrEmpty(conv.RecipientType))
        {
            var label = GetRecipientTypeLabelArabic(conv.RecipientType);
            var doctorName = otherParticipant?.User?.Doctor?.Name;
            return conv.RecipientType.Equals("TreatingDoctor", StringComparison.OrdinalIgnoreCase) && doctorName != null
                ? $"د. {doctorName} — {label}"
                : label;
        }
        // Fallback for legacy conversations without recipient type
        return conv.IsGroup
            ? conv.Title
            : (otherParticipant?.User?.Doctor?.Name ?? otherParticipant?.User?.Username ?? conv.Title);
    }

    private static string BuildNewConversationTitle(string recipientType, Patient? patient)
    {
        var recipientLabel = GetRecipientTypeLabelArabic(recipientType);
        return patient != null
            ? $"محادثة مع المريض: {patient.FirstName} {patient.LastName} — موجهة إلى: {recipientLabel}"
            : $"محادثة مريض — موجهة إلى: {recipientLabel}";
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

    private static MessageDto MapMessage(Message m, Guid currentUserId) => new()
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
        Attachments = m.Attachments?.Select(a => new MessageAttachmentDto
        {
            Id = a.Id,
            MessageId = a.MessageId,
            FileUrl = a.FileUrl,
            FileName = a.FileName,
            FileSize = a.FileSize,
            MimeType = a.MimeType
        }).ToList() ?? [],
        ReplyToId = m.ReplyToId,
        ReplyToContent = m.ReplyTo?.Content?.Length > 100
            ? m.ReplyTo.Content[..100] + "..." : m.ReplyTo?.Content,
        ReplyToSenderName = m.ReplyTo?.Sender?.Doctor?.Name ?? m.ReplyTo?.Sender?.Username,
        IsSystemMessage = m.IsSystemMessage,
        IsEdited = m.IsEdited,
        EditedAt = m.EditedAt,
        IsReadByMe = m.Reads.Any(r => r.UserId == currentUserId),
        ReadCount = m.Reads.Count,
        CreatedAt = m.CreatedAt
    };

    /// <summary>
    /// Checks whether a DbUpdateException is caused by a unique constraint violation
    /// (PostgreSQL error code 23505). Used to make MarkAsRead idempotent under
    /// concurrent requests.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is NpgsqlException pgEx && pgEx.SqlState == "23505";
    }

    // ─── Nested Result Types ─────────────────────────────────────────────────

    /// <summary>
    /// Result of verifying patient-facing access to a conversation.
    /// Replaces the controller's (userId, conv, IActionResult?) tuple.
    /// </summary>
    private sealed record AccessVerificationResult
    {
        public bool IsAuthorized { get; init; }
        public Conversation? Conversation { get; init; }
        public string? ErrorMessage { get; init; }
        public int StatusCode { get; init; }

        public static AccessVerificationResult Success(Conversation conversation) => new()
        {
            IsAuthorized = true,
            Conversation = conversation
        };

        public static AccessVerificationResult Fail(string errorMessage, int statusCode) => new()
        {
            IsAuthorized = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode
        };
    }
}


