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
    public string? Username { get; set; }            // Auto-generated (e.g., "GM0001")
    public string? PasswordHash { get; set; }         // Argon2id hashed
    public string? PasswordSalt { get; set; }          // Random salt
    public string? InitialPassword { get; set; }       // Plain-text initial password shown to staff

    [NotMapped]
    public string? PlainPassword { get; set; }         // NOT persisted - for display only

    public Patient Patient { get; set; } = null!;
}
