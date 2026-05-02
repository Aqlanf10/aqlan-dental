namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// مشارك في محادثة — يربط المستخدم بالمحادثة مع حالة القراءة.
/// </summary>
public class ConversationParticipant : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public bool IsAdmin { get; set; } = false;
    public DateTime? LastReadAt { get; set; }
    public bool IsMuted { get; set; } = false;

    // Navigation
    public Conversation Conversation { get; set; } = null!;
    public User User { get; set; } = null!;
}
