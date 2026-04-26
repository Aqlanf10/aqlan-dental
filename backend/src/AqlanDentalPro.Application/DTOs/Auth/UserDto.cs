namespace AqlanDentalPro.Application.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public string? DoctorName { get; set; }
    public string? DoctorColor { get; set; }
    public string? DoctorInitials { get; set; }
}
