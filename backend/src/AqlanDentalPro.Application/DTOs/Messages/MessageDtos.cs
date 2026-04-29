namespace AqlanDentalPro.Application.DTOs.Messages;

public class CreateConversationRequest
{
    public Guid? RecipientId { get; set; }           // For direct messages
    public List<Guid>? ParticipantIds { get; set; }  // For group conversations
    public string? Title { get; set; }                // Optional title for group conversations
    public string? InitialMessage { get; set; }       // Optional first message
    public string? Type { get; set; }                 // "Direct" or "Group"
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";  // Text, Inquiry
}

public class ConversationDto
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string Type { get; set; } = "Direct";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ConversationParticipantDto> Participants { get; set; } = [];
    public LastMessageDto? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

public class ConversationParticipantDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime? LastReadAt { get; set; }
}

public class LastMessageDto
{
    public Guid Id { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";
    public DateTime CreatedAt { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "Text";
    public DateTime CreatedAt { get; set; }
}

public class DoctorForMessagingDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? Specialty { get; set; }
}
