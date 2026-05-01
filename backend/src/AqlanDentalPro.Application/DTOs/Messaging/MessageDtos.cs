namespace AqlanDentalPro.Application.DTOs.Messaging;

/// <summary>محادثة في القائمة الجانبية</summary>
public class ConversationListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public string ConversationType { get; set; } = "StaffToStaff";
    public Guid? PatientId { get; set; }
    public string? PatientName { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public int UnreadCount { get; set; }
    public ConversationParticipantDto? OtherParticipant { get; set; }
    public List<ConversationParticipantDto> Participants { get; set; } = [];
}

/// <summary>مشارك في محادثة</summary>
public class ConversationParticipantDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public string? AvatarInitials { get; set; }
    public string? Color { get; set; }
    public bool IsAdmin { get; set; }
}

/// <summary>رسالة في المحادثة</summary>
public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string? SenderInitials { get; set; }
    public string? SenderColor { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AttachmentName { get; set; }
    public string? AttachmentType { get; set; }
    public Guid? ReplyToId { get; set; }
    public string? ReplyToContent { get; set; }
    public string? ReplyToSenderName { get; set; }
    public bool IsSystemMessage { get; set; }
    public bool IsReadByMe { get; set; }
    public int ReadCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>تفاصيل محادثة كاملة</summary>
public class ConversationDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public string ConversationType { get; set; } = "StaffToStaff";
    public Guid? PatientId { get; set; }
    public string? PatientName { get; set; }
    public string? PatientPhone { get; set; }
    public List<ConversationParticipantDto> Participants { get; set; } = [];
    public List<MessageDto> Messages { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
