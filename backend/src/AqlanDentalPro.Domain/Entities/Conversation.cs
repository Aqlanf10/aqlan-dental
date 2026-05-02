namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// محادثة بين مستخدمين — يمكن أن تكون مباشرة (شخصين) أو جماعية.
/// </summary>
public class Conversation : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public bool IsGroup { get; set; } = false;
    public Guid? CreatedBy { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }

    // Navigation
    public User? Creator { get; set; }
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
