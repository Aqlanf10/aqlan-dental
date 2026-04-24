using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AqlanDentalPro.Application.Services;

public class AuthService(IUserRepository userRepo, ITokenService tokenService, IConfiguration config)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        if (!VerifyPassword(request.Password, user.PasswordHash)) return null;

        user.LastLogin = DateTime.UtcNow;
        await userRepo.SaveChangesAsync();

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();
        await tokenService.StoreRefreshTokenAsync(user.Id, refreshToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            User = MapToDto(user),
            RefreshToken = refreshToken
        };
    }

    public async Task<(string accessToken, string refreshToken)?> RefreshAsync(Guid userId, string refreshToken)
    {
        if (!await tokenService.ValidateRefreshTokenAsync(userId, refreshToken))
            return null;

        var user = await userRepo.GetByIdWithDoctorAsync(userId);
        if (user == null || !user.IsActive) return null;

        await tokenService.RevokeRefreshTokenAsync(userId, refreshToken);

        var newAccess = tokenService.GenerateAccessToken(user);
        var newRefresh = tokenService.GenerateRefreshToken();
        await tokenService.StoreRefreshTokenAsync(userId, newRefresh);

        return (newAccess, newRefresh);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken) =>
        await tokenService.RevokeRefreshTokenAsync(userId, refreshToken);

    public async Task<UserDto?> GetMeAsync(Guid userId)
    {
        var user = await userRepo.GetByIdWithDoctorAsync(userId);
        return user == null ? null : MapToDto(user);
    }

    private static UserDto MapToDto(Domain.Entities.User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role.ToString(),
        BranchId = user.BranchId,
        DoctorName = user.Doctor?.Name,
        DoctorColor = user.Doctor?.Color,
        DoctorInitials = user.Doctor?.AvatarInitials
    };

    private bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            var salt = config["Security:Argon2Salt"] ?? "AqlanDentalSalt!";
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = Encoding.UTF8.GetBytes(salt),
                DegreeOfParallelism = 1,
                MemorySize = 65536,
                Iterations = 3
            };
            var hash = Convert.ToBase64String(argon2.GetBytes(32));
            return hash == storedHash;
        }
        catch
        {
            return false;
        }
    }
}
