namespace AqlanDentalPro.Domain.Entities;

public class PasswordResetRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string UsernameOrEmail { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Completed, Expired
    public string? RequestedIpHash { get; set; }
    public string? UserAgent { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public User? User { get; set; }
    public User? ApprovedBy { get; set; }
}
