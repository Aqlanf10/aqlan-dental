namespace AqlanDentalPro.Domain.Entities;

public class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public DateTime? LastReadAt { get; set; }

    // Navigation properties
    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
