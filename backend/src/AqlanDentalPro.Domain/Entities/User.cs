using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty; // Per-user unique salt
    public UserRole Role { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }

    public Branch? Branch { get; set; }
    public Doctor? Doctor { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
    public ICollection<ConversationParticipant> ConversationParticipants { get; set; } = [];
    public ICollection<Message> SentMessages { get; set; } = [];
    public ICollection<Conversation> CreatedConversations { get; set; } = [];
}
