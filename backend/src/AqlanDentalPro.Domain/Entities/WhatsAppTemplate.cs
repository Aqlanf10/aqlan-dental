namespace AqlanDentalPro.Domain.Entities;

public class WhatsAppTemplate : BaseEntity
{
    public string TemplateKey { get; set; } = string.Empty; // appointment_reminder, payment_reminder, etc.
    public string NameAr { get; set; } = string.Empty;
    public string ContentTemplate { get; set; } = string.Empty; // Template with placeholders like {{patient_name}}, {{date}}, etc.
    public bool IsActive { get; set; } = true;
    public string Category { get; set; } = string.Empty; // appointment, payment, general

    public ICollection<WhatsAppMessage> Messages { get; set; } = [];
}
