namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Tracks all outgoing emails for statistics, debugging, and daily limit monitoring.
/// Each row represents one email attempt (successful or failed).
/// </summary>
public class EmailLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Recipient email address.</summary>
    public string ToEmail { get; set; } = string.Empty;

    /// <summary>Email subject line.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Category: "password_reset", "appointment_reminder", "general", etc.</summary>
    public string Category { get; set; } = "general";

    /// <summary>Provider used: "resend" or "smtp".</summary>
    public string? Provider { get; set; }

    /// <summary>Was the email sent successfully?</summary>
    public bool IsSent { get; set; }

    /// <summary>Error message if sending failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Resend email ID (returned from API on success).</summary>
    public string? ExternalId { get; set; }

    /// <summary>Related entity type (e.g., "Appointment", "User").</summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>Related entity ID.</summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>When the email was attempted.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
