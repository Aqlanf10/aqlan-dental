namespace AqlanDentalPro.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsRead { get; set; } = false;
    public string? RelatedEntity { get; set; }
    public Guid? RelatedId { get; set; }

    public User User { get; set; } = null!;
}
