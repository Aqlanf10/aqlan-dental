namespace AqlanDentalPro.Application.DTOs.Auth;

public class UserPermissionsDto
{
    public string Role { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}
