using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Repositories;

public class ConversationRepository(AppDbContext context)
    : GenericRepository<Conversation>(context), IConversationRepository
{
    public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(Guid userId)
    {
        return await DbSet
            .Include(c => c.Participants)
                .ThenInclude(cp => cp.User)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .ThenInclude(m => m.Sender)
            .Where(c => c.Participants.Any(cp => cp.UserId == userId))
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Conversation?> GetDirectConversationAsync(Guid user1Id, Guid user2Id)
    {
        return await DbSet
            .Include(c => c.Participants)
            .Where(c => c.Type == ConversationType.Direct)
            .Where(c => c.Participants.Any(cp => cp.UserId == user1Id))
            .Where(c => c.Participants.Any(cp => cp.UserId == user2Id))
            .FirstOrDefaultAsync();
    }

    public async Task<Conversation?> GetWithParticipantsAsync(Guid conversationId)
    {
        return await DbSet
            .Include(c => c.Participants)
                .ThenInclude(cp => cp.User)
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
                .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == conversationId);
    }
}
