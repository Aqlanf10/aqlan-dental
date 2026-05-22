namespace AqlanDentalPro.Application.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public string? DoctorName { get; set; }
    public Guid? DoctorId { get; set; }
    public string? DoctorColor { get; set; }
    public string? DoctorInitials { get; set; }
    // SEC-02 FIX: Expose mustChangePassword flag so frontend can enforce password change
    public bool MustChangePassword { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime? DeletedAt { get; set; }
}
