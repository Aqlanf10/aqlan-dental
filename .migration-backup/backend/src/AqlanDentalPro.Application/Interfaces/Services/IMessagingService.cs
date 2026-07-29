using AqlanDentalPro.Application.DTOs.Common;
using AqlanDentalPro.Application.DTOs.Messaging;

namespace AqlanDentalPro.Application.Interfaces.Services;

/// <summary>
/// واجهة خدمة المراسلة — تدير المحادثات والرسائل بين المستخدمين.
/// </summary>
public interface IMessagingService
{
    Task<PaginatedResponse<ConversationListDto>> GetMyConversationsAsync(
        int page = 1, int pageSize = 20, string? search = null, string? type = null, bool? hasUnread = null);

    Task<ConversationDetailDto?> GetConversationAsync(Guid conversationId, int page = 1, int pageSize = 50);

    Task<ConversationDetailDto> GetOrCreatePatientConversationAsync(Guid patientId);

    Task<ConversationDetailDto> GetOrCreatePatientFacingConversationAsync(Guid patientId);

    Task<ConversationDetailDto?> GetPatientConversationAsync(Guid patientId);

    Task<ConversationDetailDto> CreateConversationAsync(CreateConversationRequest request);

    Task<MessageDto> SendMessageAsync(Guid conversationId, SendMessageRequest request);

    Task MarkAsReadAsync(Guid conversationId);

    Task<UnreadCountDto> GetUnreadCountAsync();

    Task<(bool Success, string? Error, MessageDto? Message)> EditMessageAsync(
        Guid conversationId, Guid messageId, EditMessageRequest request);

    Task<bool> DeleteMessageAsync(Guid conversationId, Guid messageId);

    Task LeaveConversationAsync(Guid conversationId);

    Task<MessagingStatsDto> GetStatsAsync();

    Task<bool> CanMessageUserPublicAsync(Guid targetUserId);

    Task<Dictionary<Guid, bool>> CanMessageUsersBatchAsync(IReadOnlyList<Guid> targetUserIds);

    /// <summary>
    /// جلب الرسائل الجديدة منذ تاريخ محدد (لتصفية Poll بالسيرفر).
    /// </summary>
    Task<List<MessageDto>> PollNewMessagesAsync(Guid conversationId, DateTime since);
}
