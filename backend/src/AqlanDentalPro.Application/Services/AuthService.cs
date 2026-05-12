using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.Application.Services;

public class AuthService(IUserRepository userRepo, ITokenService tokenService) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        if (!VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt)) return null;

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

    /// <summary>
    /// Generates a unique random salt for a new user.
    /// </summary>
    public static string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Hashes a password with the given salt using Argon2id.
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = Convert.FromBase64String(salt),
            DegreeOfParallelism = 2,
            MemorySize = 65536,
            Iterations = 3
        };
        return Convert.ToBase64String(argon2.GetBytes(32));
    }

    /// <summary>
    /// Verifies a password against the stored hash and salt.
    /// Supports both per-user salt (current) and legacy fixed-salt (Phase 1) hashes.
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        try
        {
            // Primary: per-user salt (Phase 2+)
            if (!string.IsNullOrEmpty(storedSalt))
            {
                var hash = HashPassword(password, storedSalt);
                // C-03 FIX: Use constant-time comparison to prevent timing attacks
                if (CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(hash),
                    Convert.FromBase64String(storedHash))) return true;
            }

            // Fallback: legacy Phase 1 fixed-salt hash (DOP=1, fixed salt)
            var legacyHash = HashPasswordLegacy(password);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(legacyHash),
                Convert.FromBase64String(storedHash));
        }
        catch
        {
            return false;
        }
    }

    // Legacy hash format from Phase 1 (fixed global salt, DOP=1)
    private static string HashPasswordLegacy(string password)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = Encoding.UTF8.GetBytes("AqlanDentalSalt!"),
            DegreeOfParallelism = 1,
            MemorySize = 65536,
            Iterations = 3
        };
        return Convert.ToBase64String(argon2.GetBytes(32));
    }
}
