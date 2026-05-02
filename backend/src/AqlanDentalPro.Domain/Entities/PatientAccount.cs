using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class PatientAccount : BaseEntity
{
    public Guid PatientId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;

    // Portal credentials
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; } = true;
    public bool PortalAccountActive { get; set; } = true;

    // Legacy OTP fields (kept for migration compatibility)
    public string? VerificationCode { get; set; }
    public DateTime? VerificationCodeExpiry { get; set; }
    public bool IsVerified { get; set; } = false;

    public DateTime? LastLogin { get; set; }
    public string? DeviceToken { get; set; }  // For push notifications
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Linked User account for messaging system integration
    public Guid? LinkedUserId { get; set; }
    public User? LinkedUser { get; set; }

    public Patient Patient { get; set; } = null!;
}
