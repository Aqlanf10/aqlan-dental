namespace AqlanDentalPro.Application.DTOs.Sms;

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class SendSmsRequest
{
    public Guid PatientId { get; set; }
    public string TemplateType { get; set; } = "custom";
    public Dictionary<string, string>? Parameters { get; set; }
    public string? CustomMessage { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
}

public class BulkSendSmsRequest
{
    public List<Guid> PatientIds { get; set; } = [];
    public string TemplateType { get; set; } = "custom";
    public Dictionary<string, string>? CommonParameters { get; set; }
}

public class SendSmsAppointmentReminderRequest
{
    public Guid AppointmentId { get; set; }
    public int HoursBefore { get; set; } = 24;
}

public class UpdateSmsGatewaySettingsRequest
{
    public bool Enabled { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? SenderName { get; set; }
    public int DailyLimit { get; set; } = 500;
    public bool SendAppointmentReminders { get; set; } = true;
    public bool SendPaymentReminders { get; set; } = true;
    public string? ReminderHours { get; set; } = "24,2";
}

// ─── Response DTOs ───────────────────────────────────────────────────────────

public class SmsMessageDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string TemplateType { get; set; } = "";
    public string MessageContent { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string Gateway { get; set; } = "";
    public int CharacterCount { get; set; }
    public int SegmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SmsTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = "";
    public string NameAr { get; set; } = "";
    public string ContentTemplate { get; set; } = "";
    public bool IsTemplateActive { get; set; }
    public string Category { get; set; } = "";
    public int MaxLength { get; set; }
}

public class SmsDashboardDto
{
    public bool IsEnabled { get; set; }
    public bool IsGatewayConnected { get; set; }
    public string? GatewayUrl { get; set; }
    public string? SenderName { get; set; }
    public int DailyLimit { get; set; }
    public int SentToday { get; set; }
    public int SentThisMonth { get; set; }
    public int FailedToday { get; set; }
    public int PendingCount { get; set; }
    public int DeliveredToday { get; set; }
    public decimal DeliveryRate { get; set; }
    public List<SmsMessageDto> RecentMessages { get; set; } = [];
}

public class SmsGatewaySettingsDto
{
    public bool Enabled { get; set; }
    public string? ApiUrl { get; set; }
    public bool HasApiKey { get; set; }
    public string? SenderName { get; set; }
    public int DailyLimit { get; set; }
    public bool SendAppointmentReminders { get; set; }
    public bool SendPaymentReminders { get; set; }
    public string? ReminderHours { get; set; }
    public bool IsGatewayConnected { get; set; }
}
