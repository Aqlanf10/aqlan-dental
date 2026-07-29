namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// سجل قراءة الرسالة — لمعرفة من قرأ الرسالة ومتى.
/// </summary>
public class MessageRead : BaseEntity
{
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Message Message { get; set; } = null!;
    public User User { get; set; } = null!;
}
