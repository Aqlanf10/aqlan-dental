namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// رسالة داخل محادثة — تدعم نص، مرفقات، وإشارات لرسالة سابقة (رد).
/// </summary>
public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentType { get; set; }
    public Guid? ReplyToId { get; set; }
    public bool IsSystemMessage { get; set; } = false;
    public bool IsEdited { get; set; } = false;
    public DateTime? EditedAt { get; set; }

    // Navigation
    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public Message? ReplyTo { get; set; }
    public ICollection<MessageRead> Reads { get; set; } = [];
    public ICollection<MessageAttachment> Attachments { get; set; } = [];
}
