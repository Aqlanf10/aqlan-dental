using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public Guid? BranchId { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool TwoFactorEnabled { get; set; } = false;
    public string? TwoFactorSecret { get; set; }

    public Branch? Branch { get; set; }
    public Doctor? Doctor { get; set; }
    public ICollection<AuditLog> AuditLogs { get; set; } = [];
    public ICollection<Notification> Notifications { get; set; } = [];
}
