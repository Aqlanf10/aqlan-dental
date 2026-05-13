using AqlanDentalPro.Application.DTOs.Auth;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<(string accessToken, string refreshToken)?> RefreshAsync(Guid userId, string refreshToken);
    Task LogoutAsync(Guid userId, string refreshToken);
    Task<UserDto?> GetMeAsync(Guid userId);
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
