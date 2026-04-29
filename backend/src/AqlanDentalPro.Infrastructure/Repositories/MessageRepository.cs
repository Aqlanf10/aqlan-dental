using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class MessageRepository(AppDbContext context)
    : GenericRepository<Message>(context), IMessageRepository
{
    public async Task<IEnumerable<Message>> GetConversationMessagesAsync(Guid conversationId, int page = 1, int pageSize = 50)
    {
        return await DbSet
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Message?> GetLatestMessageAsync(Guid conversationId)
    {
        return await DbSet
            .Include(m => m.Sender)
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid conversationId, Guid userId)
    {
        var participant = await Context.ConversationParticipants
            .FirstOrDefaultAsync(cp => cp.ConversationId == conversationId && cp.UserId == userId);
        if (participant is null) return 0;

        var query = DbSet.Where(m => m.ConversationId == conversationId && m.SenderId != userId);
        if (participant.LastReadAt.HasValue)
            query = query.Where(m => m.CreatedAt > participant.LastReadAt.Value);

        return await query.CountAsync();
    }

    public async Task<int> GetTotalUnreadCountAsync(Guid userId)
    {
        var conversationIds = await Context.ConversationParticipants
            .Where(cp => cp.UserId == userId)
            .Select(cp => cp.ConversationId)
            .ToListAsync();

        int total = 0;
        foreach (var convId in conversationIds)
        {
            total += await GetUnreadCountAsync(convId, userId);
        }
        return total;
    }
}
