namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Tracks SMS messages sent through the local Android SIM SMS Gateway.
/// Follows same pattern as WhatsAppMessage.
/// </summary>
public class SmsMessage : BaseEntity
{
    public Guid PatientId { get; set; }
    public Patient? Patient { get; set; }

    /// <summary>Phone number in normalized international format (+967XXXXXXXXX).</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Template type: "appointment_reminder", "payment_reminder", "follow_up", "custom".</summary>
    public string TemplateType { get; set; } = "custom";

    /// <summary>Final message content after template expansion.</summary>
    public string MessageContent { get; set; } = string.Empty;

    /// <summary>Message status lifecycle: pending → sent → delivered → failed.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>ID returned by the Android SMS Gateway API.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Error details if sending failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Retry count (max 3).</summary>
    public int RetryCount { get; set; }

    /// <summary>When the message was successfully sent.</summary>
    public DateTime? SentAt { get; set; }

    /// <summary>When delivery was confirmed (if gateway supports delivery reports).</summary>
    public DateTime? DeliveredAt { get; set; }

    /// <summary>FK to related entity (e.g. Appointment, Contract).</summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>Type of related entity.</summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>SMS gateway used: "local_android", "manual".</summary>
    public string Gateway { get; set; } = "local_android";

    /// <summary>Character count of the message (for tracking SMS segments).</summary>
    public int CharacterCount { get; set; }

    /// <summary>Number of SMS segments (160 GSM-7 / 70 UCS-2 for Arabic).</summary>
    public int SegmentCount { get; set; }
}
