using AqlanDentalPro.Domain.Enums;
using System.Text.Json;

namespace AqlanDentalPro.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public AuditAction Action { get; set; }
    public string Resource { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public JsonDocument? OldData { get; set; }
    public JsonDocument? NewData { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
}
