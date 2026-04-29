using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Repositories;

public interface IMessageRepository : IGenericRepository<Message>
{
    Task<IEnumerable<Message>> GetConversationMessagesAsync(Guid conversationId, int page = 1, int pageSize = 50);
    Task<Message?> GetLatestMessageAsync(Guid conversationId);
    Task<int> GetUnreadCountAsync(Guid conversationId, Guid userId);
    Task<int> GetTotalUnreadCountAsync(Guid userId);
}

public interface IConversationRepository : IGenericRepository<Conversation>
{
    Task<IEnumerable<Conversation>> GetUserConversationsAsync(Guid userId);
    Task<Conversation?> GetDirectConversationAsync(Guid user1Id, Guid user2Id);
    Task<Conversation?> GetWithParticipantsAsync(Guid conversationId);
}
