namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// مرفق رسالة — يدعم عدة مرفقات لكل رسالة.
/// Message also retains legacy single-attachment fields (AttachmentUrl/Name/Type)
/// for backward compatibility; new attachments use this join entity.
/// </summary>
public class MessageAttachment : BaseEntity
{
    public Guid MessageId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;

    // Navigation
    public Message Message { get; set; } = null!;
}
