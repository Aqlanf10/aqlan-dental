using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.Application.Services;

public class AuthService(IUserRepository userRepo, ITokenService tokenService, ILogger<AuthService> logger) : IAuthService
{
    private readonly ILogger<AuthService> _logger = logger;

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        var (passwordValid, isLegacyHash) = VerifyPasswordWithMigrationFlag(
            request.Password,
            user.PasswordHash,
            user.PasswordSalt);
        if (!passwordValid) return null;

        if (isLegacyHash)
        {
            var newSalt = GenerateSalt();
            var newHash = HashPassword(request.Password, newSalt);
            user.PasswordHash = newHash;
            user.PasswordSalt = newSalt;
            user.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("SEC-02: Auto-migrated legacy password hash for user {UserId}", user.Id);
        }

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

    public async Task<(string accessToken, string refreshToken)?> RefreshAsync(
        Guid userId,
        string refreshToken)
    {
        var user = await userRepo.GetByIdWithDoctorAsync(userId);
        if (user == null || !user.IsActive) return null;

        var replacementRefreshToken = tokenService.GenerateRefreshToken();
        var replacementAccessToken = tokenService.GenerateAccessToken(user);

        // W04: validation, invalidation of the old token, and persistence of the
        // replacement must be one atomic operation. The former validate-then-
        // revoke sequence allowed two simultaneous browser windows to both pass
        // validation before either one deleted the old token.
        var rotated = await tokenService.RotateRefreshTokenAsync(
            userId,
            refreshToken,
            replacementRefreshToken);
        if (!rotated) return null;

        return (replacementAccessToken, replacementRefreshToken);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken) =>
        await tokenService.RevokeRefreshTokenAsync(userId, refreshToken);

    public async Task<UserDto?> GetMeAsync(Guid userId)
    {
        var user = await userRepo.GetByIdWithDoctorAsync(userId);
        return user == null ? null : MapToDto(user);
    }

    public async Task<string?> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await userRepo.GetByIdWithDoctorAsync(userId);
        if (user == null || !user.IsActive) return null;

        if (!VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
            return null;

        var newSalt = GenerateSalt();
        var newHash = HashPassword(newPassword, newSalt);

        user.PasswordHash = newHash;
        user.PasswordSalt = newSalt;
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        await userRepo.SaveChangesAsync();

        return tokenService.GenerateAccessToken(user);
    }

    private static UserDto MapToDto(Domain.Entities.User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role.ToString(),
        BranchId = user.BranchId,
        Email = user.Email,
        IsActive = user.IsActive,
        DeletedAt = user.DeletedAt,
        DoctorName = user.Doctor?.Name,
        DoctorId = user.Doctor?.Id,
        DoctorColor = user.Doctor?.Color,
        DoctorInitials = user.Doctor?.AvatarInitials,
        MustChangePassword = user.MustChangePassword
    };

    public static string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(saltBytes);
    }

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

    private (bool isValid, bool isLegacyHash) VerifyPasswordWithMigrationFlag(
        string password,
        string storedHash,
        string storedSalt)
    {
        try
        {
            if (!string.IsNullOrEmpty(storedSalt))
            {
                var hash = HashPassword(password, storedSalt);
                if (CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(hash),
                    Convert.FromBase64String(storedHash)))
                {
                    return (true, false);
                }
            }

            _logger.LogWarning(
                "SEC-02 DEPRECATION: Legacy fixed-salt hash used for user verification. Username={Username}",
                "REDACTED");
#pragma warning disable CS0618
            var legacyHash = HashPasswordLegacy(password);
#pragma warning restore CS0618
            if (CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(legacyHash),
                Convert.FromBase64String(storedHash)))
            {
                _logger.LogWarning("SEC-02: Legacy fixed-salt hash verified — will auto-migrate on login");
                return (true, true);
            }

            return (false, false);
        }
        catch
        {
            return (false, false);
        }
    }

    private bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var (isValid, _) = VerifyPasswordWithMigrationFlag(password, storedHash, storedSalt);
        return isValid;
    }

    [Obsolete("Legacy Phase 1 hash — remove after full user migration to per-user salts")]
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
