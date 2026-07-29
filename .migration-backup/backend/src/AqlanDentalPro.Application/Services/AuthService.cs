using AqlanDentalPro.Application.DTOs.Auth;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Application.Services;

public class AuthService(IUserRepository userRepo, ITokenService tokenService, ILogger<AuthService> logger) : IAuthService
{
    private readonly ILogger<AuthService> _logger = logger;
    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await userRepo.GetByUsernameAsync(request.Username);
        if (user == null) return null;

        var (passwordValid, isLegacyHash) = VerifyPasswordWithMigrationFlag(request.Password, user.PasswordHash, user.PasswordSalt);
        if (!passwordValid) return null;

        // SEC-02 FIX: Auto-migrate legacy fixed-salt hashes to per-user salts on successful login.
        // This eliminates the need for users to explicitly change their password.
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
        user.MustChangePassword = false; // SEC-02 FIX: Clear flag after successful password change
        user.UpdatedAt = DateTime.UtcNow;

        await userRepo.SaveChangesAsync();

        // LOGIN FIX: Return a new access token with mustChangePassword=false.
        // Without this, the old JWT still carries mustChangePassword=true,
        // and MustChangePasswordMiddleware blocks ALL subsequent API calls,
        // making the system unusable until the user manually re-logs in.
        return tokenService.GenerateAccessToken(user);
    }

    private static UserDto MapToDto(Domain.Entities.User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role.ToString(),
        BranchId = user.BranchId,
        Email = user.Email,                    // HOTFIX PR165: Was missing — all auth responses showed email: null
        IsActive = user.IsActive,              // HOTFIX PR165: Was missing — all auth responses showed isActive: false
        DeletedAt = user.DeletedAt,            // HOTFIX PR165: Was missing — needed for frontend deleted-user handling
        DoctorName = user.Doctor?.Name,
        DoctorId = user.Doctor?.Id,
        DoctorColor = user.Doctor?.Color,
        DoctorInitials = user.Doctor?.AvatarInitials,
        MustChangePassword = user.MustChangePassword
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
    /// Verifies a password and indicates whether it matched the legacy hash format.
    /// Used by LoginAsync to trigger automatic migration from fixed-salt to per-user-salt.
    /// </summary>
    private (bool isValid, bool isLegacyHash) VerifyPasswordWithMigrationFlag(string password, string storedHash, string storedSalt)
    {
        try
        {
            // Primary: per-user salt (Phase 2+)
            if (!string.IsNullOrEmpty(storedSalt))
            {
                var hash = HashPassword(password, storedSalt);
                if (CryptographicOperations.FixedTimeEquals(
                    Convert.FromBase64String(hash),
                    Convert.FromBase64String(storedHash))) return (true, false);
            }

            // Fallback: legacy Phase 1 fixed-salt hash (DOP=1, fixed salt)
            // SEC-02 FIX: Log deprecation warning — this path should be removed once all users are migrated
            _logger.LogWarning(
                "SEC-02 DEPRECATION: Legacy fixed-salt hash used for user verification. " +
                "This indicates a user still has a Phase 1 hash. " +
                "User should change their password to migrate to per-user salt. " +
                "Username={Username}",
                "REDACTED"); // Don't log username for privacy
#pragma warning disable CS0618 // Suppress obsolete warning — intentionally calling legacy method
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

    /// <summary>
    /// Verifies a password against the stored hash and salt.
    /// Supports both per-user salt (current) and legacy fixed-salt (Phase 1) hashes.
    /// </summary>
    private bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var (isValid, _) = VerifyPasswordWithMigrationFlag(password, storedHash, storedSalt);
        return isValid;
    }

    // SEC-02 TODO: Legacy hash format from Phase 1 (fixed global salt, DOP=1)
    // This method MUST be removed once all users have been migrated to per-user salts.
    // Migration path: Users are auto-migrated on login (VerifyPasswordWithMigrationFlag)
    // and when they use ChangePasswordAsync().
    // Track migration progress via the deprecation log above.
    // After confirming zero legacy-hash log entries for 30+ days, remove this method
    // and simplify VerifyPassword to per-user-salt only.
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
