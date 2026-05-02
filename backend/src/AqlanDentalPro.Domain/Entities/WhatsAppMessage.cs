namespace AqlanDentalPro.Domain.Entities;

public class WhatsAppMessage : BaseEntity
{
    public Guid PatientId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty; // appointment_reminder, appointment_confirmation, payment_reminder, custom
    public string MessageContent { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, sent, delivered, read, failed
    public string? ExternalId { get; set; }  // ID from WhatsApp API
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; } = 0;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public Guid? RelatedEntityId { get; set; } // Appointment ID, Payment ID, etc.
    public string? RelatedEntityType { get; set; } // Appointment, Payment, etc.

    public Patient Patient { get; set; } = null!;
}
