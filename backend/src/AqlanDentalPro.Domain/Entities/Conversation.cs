using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Conversation : BaseEntity
{
    public string? Title { get; set; }
    public ConversationType Type { get; set; } = ConversationType.Direct;
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public User Creator { get; set; } = null!;
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
