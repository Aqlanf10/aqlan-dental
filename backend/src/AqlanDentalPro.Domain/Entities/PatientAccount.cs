using AqlanDentalPro.Domain.Enums;

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

    public Patient Patient { get; set; } = null!;
}
