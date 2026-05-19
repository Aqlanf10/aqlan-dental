using AqlanDentalPro.Application.DTOs.Messaging;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IPatientPortalMessagingService
{
    Task<Guid?> EnsureLinkedUserAsync(Guid patientId);
    Task<List<PortalRecipientDto>> GetRecipientsAsync(Guid patientId);
    Task<(object Result, int TotalCount)> GetConversationsAsync(Guid patientId, Guid userId, int page, int pageSize, string? search);
    Task<ConversationDetailDto> StartConversationAsync(Guid patientId, Guid userId, StartConversationRequest? request);
    Task<ConversationDetailDto?> GetConversationAsync(Guid patientId, Guid userId, Guid conversationId, int page, int pageSize);
    Task<MessageDto> SendMessageAsync(Guid patientId, Guid userId, Guid conversationId, SendMessageRequest request);
    Task MarkAsReadAsync(Guid patientId, Guid userId, Guid conversationId);
    Task<UnreadCountDto> GetUnreadCountAsync(Guid patientId, Guid userId);
}
