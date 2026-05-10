using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AqlanDentalPro.Infrastructure.Services;

public class MessagingService(AppDbContext db, ICurrentUserService currentUser, INotificationService notifications)
{
    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedAccessException();

    private const int MaxMessageLength = 2000;

    // ─── محادثاتي ──────────────────────────────────────────────────────────────
    public async Task<PaginatedResponse<ConversationListDto>> GetMyConversationsAsync(
        int page = 1, int pageSize = 20, string? search = null, string? type = null)
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

        // Filter by conversation type
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<ConversationType>(type, true, out var convType))
        {
            var typeStr = convType.ToString();
            query = query.Where(c => c.ConversationType == typeStr);
        }

        // Search across title, participant names, patient name, patient number, and message content
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Title.Contains(search) ||
                c.Participants.Any(p => p.User.Doctor != null && p.User.Doctor.Name.Contains(search)) ||
                c.Participants.Any(p => p.User.Username.Contains(search)) ||
                (c.Patient != null && (c.Patient.FirstName + " " + c.Patient.LastName).Contains(search)) ||
                (c.Patient != null && c.Patient.PatientNumber.Contains(search)));
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
            .Include(c => c.Patient)
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

        // NOTE: Mark-as-read is now handled exclusively by the explicit
        // POST /conversations/{id}/read endpoint to avoid race conditions
        // between concurrent GET and POST requests (fix/portal-message-read-500).

        // Get patient info if this is a patient conversation
        string? patientName = null;
        string? patientPhone = null;
        string? patientNumber = null;
        if (conv.PatientId.HasValue)
        {
            var patient = await db.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == conv.PatientId.Value);
            if (patient != null)
            {
                patientName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim();
                patientPhone = patient.Phone;
                patientNumber = patient.PatientNumber;
            }
        }

        return new ConversationDetailDto
        {
            Id = conv.Id,
            Title = conv.ConversationType == "StaffToPatient" && patientName != null
                ? $"المريض: {patientName}"
                : conv.Title,
            IsGroup = conv.IsGroup,
            ConversationType = conv.ConversationType,
            PatientId = conv.PatientId,
            PatientName = patientName,
            PatientNumber = patientNumber,
            PatientPhone = patientPhone,
            Participants = conv.Participants.Select(MapParticipantDto).ToList(),
            Messages = messages.Select(MapMessageDto).ToList(),
            CreatedAt = conv.CreatedAt,
            RecipientType = conv.RecipientType,
            RecipientUserId = conv.RecipientUserId
        };
    }

    // ─── إنشاء/جلب محادثة مريض ──────────────────────────────────────────────────
    public async Task<ConversationDetailDto> GetOrCreatePatientConversationAsync(Guid patientId)
    {
        var patient = await db.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == patientId)
            ?? throw new KeyNotFoundException("المريض غير موجود");

        // Find existing StaffToPatient conversation for this patient
        var existing = await db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.PatientId == patientId && c.ConversationType == "StaffToPatient");

        if (existing != null)
        {
            // Add current staff user as participant if not already
            if (!existing.Participants.Any(p => p.UserId == UserId))
            {
                await db.ConversationParticipants.AddAsync(new ConversationParticipant
                {
                    ConversationId = existing.Id,
                    UserId = UserId,
                    IsAdmin = false
                });
            }

            // Also add patient's linked user as participant if not already
            await EnsurePatientParticipantAsync(existing.Id, patientId);

            await db.SaveChangesAsync();
            return (await GetConversationAsync(existing.Id))!;
        }

        var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
        var conv = new Conversation
        {
            Title = $"المريض: {patientName}",
            IsGroup = true,
            ConversationType = "StaffToPatient",
            CreatedBy = UserId,
            PatientId = patientId,
        };

        await db.Conversations.AddAsync(conv);
        await db.ConversationParticipants.AddAsync(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId = UserId,
            IsAdmin = true
        });

        // Add patient's linked user as participant so they can see/reply from portal
        await EnsurePatientParticipantAsync(conv.Id, patientId);

        // Add initial system message
        await db.Messages.AddAsync(new Message
        {
            ConversationId = conv.Id,
            SenderId = UserId,
            Content = $"تم إنشاء محادثة للمريض {patientName} — {patient.PatientNumber}",
            IsSystemMessage = true
        });

        conv.LastMessageAt = DateTime.UtcNow;
        conv.LastMessagePreview = $"محادثة: {patientName}";

        await db.SaveChangesAsync();
        return (await GetConversationAsync(conv.Id))!;
    }

    /// <summary>
    /// Ensures the patient's linked User (for messaging) is a participant in the conversation.
    /// If no PatientAccount or linked User exists yet, creates them.
    /// </summary>
    private async Task EnsurePatientParticipantAsync(Guid conversationId, Guid patientId)
    {
        var account = await db.PatientAccounts.FirstOrDefaultAsync(a => a.PatientId == patientId);
        if (account?.LinkedUserId == null)
        {
            // Patient doesn't have a messaging account yet — create one
            var patient = await db.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == patientId);
            if (patient == null) return;

            // Check if a User with this patient's number already exists (from seed or previous runs)
            var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Username == patient.PatientNumber);

            if (account == null)
            {
                // No PatientAccount exists — create linked User + PatientAccount
                if (existingUser == null)
                {
                    existingUser = new User
                    {
                        Username = patient.PatientNumber,
                        PasswordHash = "",
                        PasswordSalt = "",
                        Role = UserRole.Patient,
                        IsActive = true
                    };
                    db.Users.Add(existingUser);
                    await db.SaveChangesAsync();
                }

                account = new PatientAccount
                {
                    PatientId = patientId,
                    PhoneNumber = patient.Phone ?? "",
                    Username = patient.PatientNumber,
                    IsVerified = false,
                    MustChangePassword = true,
                    PortalAccountActive = false,
                    IsActive = true,
                    LinkedUserId = existingUser.Id
                };
                db.PatientAccounts.Add(account);
                await db.SaveChangesAsync();
            }
            else
            {
                // PatientAccount exists but no linked user — create or reuse one
                if (existingUser == null)
                {
                    existingUser = new User
                    {
                        Username = patient.PatientNumber,
                        PasswordHash = "",
                        PasswordSalt = "",
                        Role = UserRole.Patient,
                        IsActive = true
                    };
                    db.Users.Add(existingUser);
                    await db.SaveChangesAsync();
                }

                account.LinkedUserId = existingUser.Id;
                await db.SaveChangesAsync();
            }
        }

        // Add linked user as participant if not already
        var alreadyParticipant = await db.ConversationParticipants
            .AnyAsync(cp => cp.ConversationId == conversationId && cp.UserId == account.LinkedUserId.Value);
        if (!alreadyParticipant)
        {
            await db.ConversationParticipants.AddAsync(new ConversationParticipant
            {
                ConversationId = conversationId,
                UserId = account.LinkedUserId.Value,
                IsAdmin = false
            });
        }
    }

    // ─── إنشاء/جلب محادثة PatientFacing (مرئية للمريض) ─────────────────────────
    public async Task<ConversationDetailDto> GetOrCreatePatientFacingConversationAsync(Guid patientId)
    {
        var patient = await db.Patients
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == patientId)
            ?? throw new KeyNotFoundException("المريض غير موجود");

        // Find existing PatientFacing conversation for this patient
        var existing = await db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.PatientId == patientId && c.ConversationType == "PatientFacing");

        if (existing != null)
        {
            // Add current staff user as participant if not already
            if (!existing.Participants.Any(p => p.UserId == UserId))
            {
                await db.ConversationParticipants.AddAsync(new ConversationParticipant
                {
                    ConversationId = existing.Id,
                    UserId = UserId,
                    IsAdmin = false
                });
            }

            // Ensure patient's linked user is a participant
            await EnsurePatientParticipantAsync(existing.Id, patientId);
            await db.SaveChangesAsync();
            return (await GetConversationAsync(existing.Id))!;
        }

        var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
        var conv = new Conversation
        {
            Title = $"محادثة مع المريض: {patientName}",
            IsGroup = true,
            ConversationType = "PatientFacing",
            CreatedBy = UserId,
            PatientId = patientId,
        };

        await db.Conversations.AddAsync(conv);
        await db.ConversationParticipants.AddAsync(new ConversationParticipant
        {
            ConversationId = conv.Id,
            UserId = UserId,
            IsAdmin = true
        });

        // Add patient's linked user as participant so they can see and reply from portal
        await EnsurePatientParticipantAsync(conv.Id, patientId);

        // Initial system message clearly stating this is visible to the patient
        await db.Messages.AddAsync(new Message
        {
            ConversationId = conv.Id,
            SenderId = UserId,
            Content = $"تم فتح محادثة مع المريض {patientName} ({patient.PatientNumber}) — هذه المحادثة مرئية للمريض في بوابته",
            IsSystemMessage = true
        });

        conv.LastMessageAt = DateTime.UtcNow;
        conv.LastMessagePreview = $"محادثة مع المريض: {patientName}";

        await db.SaveChangesAsync();
        return (await GetConversationAsync(conv.Id))!;
    }

    // ─── جلب محادثة مريض بدون إنشاء ──────────────────────────────────────────────
    public async Task<ConversationDetailDto?> GetPatientConversationAsync(Guid patientId)
    {
        // Verify patient exists
        var patientExists = await db.Patients.IgnoreQueryFilters().AnyAsync(p => p.Id == patientId);
        if (!patientExists)
            throw new KeyNotFoundException("المريض غير موجود");

        // Find existing StaffToPatient conversation for this patient
        var existing = await db.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.PatientId == patientId && c.ConversationType == "StaffToPatient");

        if (existing == null)
            return null;

        // Verify current user is a participant
        if (!existing.Participants.Any(p => p.UserId == UserId))
            return null;

        return await GetConversationAsync(existing.Id);
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

        // Parse ConversationType from request
        var conversationType = ConversationType.StaffToStaff;
        if (!string.IsNullOrWhiteSpace(request.ConversationType) && Enum.TryParse<ConversationType>(request.ConversationType, true, out var parsedType))
            conversationType = parsedType;

        var conv = new Conversation
        {
            Title = request.IsGroup
                ? (request.Title ?? "مجموعة جديدة")
                : await GenerateDirectTitleAsync(participantIds),
            IsGroup = request.IsGroup,
            CreatedBy = UserId,
            ConversationType = conversationType.ToString(),
            PatientId = request.PatientId,
            BranchId = currentUser.BranchId,
        };

        // For StaffToPatient with a patient, set a descriptive title
        if (conversationType == ConversationType.StaffToPatient && request.PatientId.HasValue)
        {
            var patient = await db.Patients.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == request.PatientId.Value);
            if (patient != null)
            {
                var patientName = $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Replace("  ", " ").Trim();
                conv.Title = $"محادثة المريض - {patientName}";
            }
        }

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

        // Validate content: allow attachment-only messages (either Content or AttachmentUrl must be present)
        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content) && string.IsNullOrWhiteSpace(request.AttachmentUrl))
            throw new ArgumentException("يجب أن تحتوي الرسالة على نص أو مرفق");
        if (content.Length > MaxMessageLength)
            throw new ArgumentException($"محتوى الرسالة طويل جداً. الحد الأقصى {MaxMessageLength} حرف");

        // Validate attachment if provided
        if (!string.IsNullOrWhiteSpace(request.AttachmentUrl))
        {
            if (!request.AttachmentUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("رابط المرفق غير صالح");

            var allowedMimeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg", "image/png", "application/pdf",
                "audio/webm", "audio/ogg", "audio/mp4"
            };
            if (string.IsNullOrWhiteSpace(request.AttachmentType) || !allowedMimeTypes.Contains(request.AttachmentType))
                throw new ArgumentException("نوع المرفق غير مدعوم. الأنواع المسموحة: صور JPEG، صور PNG، ملفات PDF، رسائل صوتية");
        }

        var msg = new Message
        {
            ConversationId = conversationId,
            SenderId = UserId,
            Content = content,
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
            var previewText = !string.IsNullOrWhiteSpace(content) ? content
                : (!string.IsNullOrWhiteSpace(request.AttachmentName) ? "📎 " + request.AttachmentName : "مرفق");
            conv.LastMessagePreview = previewText.Length > 200
                ? previewText[..200] + "..."
                : previewText;
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
        try
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
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Concurrent mark-as-read already inserted these rows — idempotent success
        }
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

    // ─── تعديل رسالة (المرسل فقط، خلال 15 دقيقة) ────────────────────────────────
    public async Task<(bool Success, string? Error, MessageDto? Message)> EditMessageAsync(
        Guid conversationId, Guid messageId, EditMessageRequest request)
    {
        var content = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
            return (false, "محتوى الرسالة لا يمكن أن يكون فارغاً", null);
        if (content.Length > MaxMessageLength)
            return (false, $"محتوى الرسالة طويل جداً. الحد الأقصى {MaxMessageLength} حرف", null);

        var message = await db.Messages
            .Include(m => m.Sender).ThenInclude(u => u.Doctor)
            .Include(m => m.Reads)
            .Include(m => m.ReplyTo).ThenInclude(r => r!.Sender).ThenInclude(u => u.Doctor)
            .FirstOrDefaultAsync(m => m.Id == messageId
                && m.ConversationId == conversationId
                && m.SenderId == UserId
                && !m.IsSystemMessage);

        if (message is null) return (false, null, null);

        if ((DateTime.UtcNow - message.CreatedAt).TotalMinutes > 15)
            return (false, "لا يمكن تعديل الرسالة بعد مرور 15 دقيقة من الإرسال", null);

        message.Content = content;
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return (true, null, MapMessageDto(message));
    }

    // ─── إحصائيات المراسلة ───────────────────────────────────────────────────────
    public async Task<MessagingStatsDto> GetStatsAsync()
    {
        var myConversationIds = await db.ConversationParticipants
            .Where(cp => cp.UserId == UserId)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        var todayUtc = DateTime.UtcNow.Date;
        var weekAgoUtc = DateTime.UtcNow.AddDays(-7);

        var conversations = await db.Conversations
            .Where(c => myConversationIds.Contains(c.Id))
            .Select(c => new { c.ConversationType, c.LastMessageAt })
            .ToListAsync();

        var messagesToday = await db.Messages
            .Where(m => myConversationIds.Contains(m.ConversationId)
                     && m.CreatedAt >= todayUtc
                     && !m.IsSystemMessage)
            .CountAsync();

        var messagesThisWeek = await db.Messages
            .Where(m => myConversationIds.Contains(m.ConversationId)
                     && m.CreatedAt >= weekAgoUtc
                     && !m.IsSystemMessage)
            .CountAsync();

        return new MessagingStatsDto
        {
            TotalConversations = conversations.Count,
            ActiveConversations = conversations.Count(c => c.LastMessageAt >= weekAgoUtc),
            MessagesToday = messagesToday,
            MessagesThisWeek = messagesThisWeek,
            StaffToStaffConversations = conversations.Count(c => c.ConversationType == "StaffToStaff"),
            StaffToPatientConversations = conversations.Count(c => c.ConversationType == "StaffToPatient"),
            PatientFacingConversations = conversations.Count(c => c.ConversationType == "PatientFacing"),
        };
    }

    // ─── حذف رسالة (المرسل فقط، soft delete عبر تغيير المحتوى) ──────────────────
    public async Task<bool> DeleteMessageAsync(Guid conversationId, Guid messageId)
    {
        var message = await db.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId && m.SenderId == UserId);

        if (message is null) return false;

        message.Content = "تم حذف هذه الرسالة";
        message.AttachmentUrl = null;
        message.AttachmentName = null;
        message.AttachmentType = null;
        await db.SaveChangesAsync();
        return true;
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

        // All staff roles can message Patient users
        var targetRole = targetUser.Role;
        if (targetRole == UserRole.Patient) return true;

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

        var patientName = conv.Patient != null
            ? $"{conv.Patient.FirstName} {conv.Patient.LastName}".Trim()
            : null;
        var patientNumber = conv.Patient?.PatientNumber;

        return new ConversationListDto
        {
            Id = conv.Id,
            Title = conv.ConversationType == "StaffToPatient"
                ? (patientName != null ? $"المريض: {patientName}" : conv.Title)
                : (conv.IsGroup ? conv.Title : (otherParticipant?.User?.Doctor?.Name ?? otherParticipant?.User?.Username ?? conv.Title)),
            IsGroup = conv.IsGroup,
            ConversationType = conv.ConversationType,
            PatientId = conv.PatientId,
            PatientName = patientName,
            PatientNumber = patientNumber,
            LastMessageAt = conv.LastMessageAt,
            LastMessagePreview = conv.LastMessagePreview,
            OtherParticipant = otherParticipant != null ? MapParticipantDto(otherParticipant) : null,
            Participants = conv.Participants.Select(MapParticipantDto).ToList(),
            RecipientType = conv.RecipientType,
            RecipientUserId = conv.RecipientUserId
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
            IsEdited = msg.IsEdited,
            EditedAt = msg.EditedAt,
            IsReadByMe = msg.Reads.Any(r => r.UserId == UserId),
            ReadCount = msg.Reads.Count,
            CreatedAt = msg.CreatedAt
        };
    }

    /// <summary>
    /// Checks whether a DbUpdateException is caused by a unique constraint violation
    /// (PostgreSQL error code 23505). Used to make MarkAsReadAsync idempotent under
    /// concurrent requests.
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        return ex.InnerException is NpgsqlException pgEx && pgEx.SqlState == "23505";
    }
}
