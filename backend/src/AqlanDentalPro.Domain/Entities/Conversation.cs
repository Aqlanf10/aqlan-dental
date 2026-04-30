using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// محادثة بين مستخدمين — يمكن أن تكون مباشرة (شخصين) أو جماعية أو مرتبطة بمريض.
/// </summary>
public class Conversation : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public bool IsGroup { get; set; } = false;
    public Guid? CreatedBy { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }

    // Patient-linked conversation support
    public Guid? PatientId { get; set; }
    public ConversationType ConversationType { get; set; } = ConversationType.StaffToStaff;
    public Guid? BranchId { get; set; }

    // Navigation
    public User? Creator { get; set; }
    public Patient? Patient { get; set; }
    public Branch? Branch { get; set; }
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}
