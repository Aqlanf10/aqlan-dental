using System.ComponentModel.DataAnnotations.Schema;

namespace AqlanDentalPro.Domain.Entities;

public class PatientAccount : BaseEntity
{
    public Guid PatientId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? VerificationCode { get; set; }
    public DateTime? VerificationCodeExpiry { get; set; }
    public bool IsVerified { get; set; } = false;
    public DateTime? LastLogin { get; set; }
    public string? DeviceToken { get; set; }  // For push notifications
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Username/Password authentication
    public string? Username { get; set; }            // Auto-generated (= patient number)
    public string? PasswordHash { get; set; }         // Argon2id hashed
    public string? PasswordSalt { get; set; }          // Random salt
    public bool MustChangePassword { get; set; } = true;
    public bool PortalAccountActive { get; set; } = true;

    // Linked User account for messaging system integration
    public Guid? LinkedUserId { get; set; }
    public User? LinkedUser { get; set; }

    public Patient Patient { get; set; } = null!;
}
