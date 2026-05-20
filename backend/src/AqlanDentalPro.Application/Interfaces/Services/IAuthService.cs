using AqlanDentalPro.Application.DTOs.Auth;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<(string accessToken, string refreshToken)?> RefreshAsync(Guid userId, string refreshToken);
    Task LogoutAsync(Guid userId, string refreshToken);
    Task<UserDto?> GetMeAsync(Guid userId);
    /// <summary>
    /// Changes the user's password. On success, clears MustChangePassword and returns a new access token
    /// (with mustChangePassword=false claim). Returns null if the current password is wrong.
    /// </summary>
    Task<string?> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
