namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// SMS message templates with {{placeholder}} syntax.
/// Separate from WhatsApp templates because SMS has 160-char limit per segment.
/// </summary>
public class SmsTemplate : BaseEntity
{
    /// <summary>Unique key for this template (e.g. "appointment_reminder").</summary>
    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>Arabic display name.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Template content with {{placeholder}} variables.</summary>
    public string ContentTemplate { get; set; } = string.Empty;

    /// <summary>Whether this template is active.</summary>
    public bool IsTemplateActive { get; set; } = true;

    /// <summary>Category: "appointment", "payment", "follow_up", "general".</summary>
    public string Category { get; set; } = "general";

    /// <summary>Max character count before splitting into multiple segments.</summary>
    public int MaxLength { get; set; } = 160;
}
