using AqlanDentalPro.Domain.Interfaces;

namespace AqlanDentalPro.Domain.Entities;

public abstract class BaseEntity : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
