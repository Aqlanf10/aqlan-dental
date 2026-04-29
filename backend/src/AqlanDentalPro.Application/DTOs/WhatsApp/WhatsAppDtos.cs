namespace AqlanDentalPro.Application.DTOs.WhatsApp;

public class SendMessageRequest
{
    public Guid PatientId { get; set; }
    public string TemplateType { get; set; } = string.Empty;
    public Dictionary<string, string>? Parameters { get; set; }
    public string? CustomMessage { get; set; }
}

public class BulkSendMessageRequest
{
    public List<Guid> PatientIds { get; set; } = [];
    public string TemplateType { get; set; } = string.Empty;
    public Dictionary<string, string>? CommonParameters { get; set; }
}

public class SendAppointmentReminderRequest
{
    public Guid AppointmentId { get; set; }
    public int HoursBefore { get; set; } = 24; // Send reminder N hours before appointment
}

public class WhatsAppMessageDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

public class WhatsAppTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class UpdateWhatsAppTemplateRequest
{
    public string ContentTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class WhatsAppDashboardDto
{
    public int SentToday { get; set; }
    public int DeliveredToday { get; set; }
    public int FailedToday { get; set; }
    public int PendingCount { get; set; }
    public List<WhatsAppMessageDto> RecentMessages { get; set; } = [];
}
