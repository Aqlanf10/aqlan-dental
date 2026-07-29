using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateAccessToken(User user, Guid? originalUserId = null, string? originalRole = null);
    string GenerateRefreshToken();
    Task StoreRefreshTokenAsync(Guid userId, string refreshToken);
    Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken);
    Task RevokeRefreshTokenAsync(Guid userId, string refreshToken);
    Task RevokeAllRefreshTokensAsync(Guid userId);
    /// <summary>Returns the userId that owns <paramref name="refreshToken"/>, or null if not found.</summary>
    Task<Guid?> GetOwnerOfRefreshTokenAsync(string refreshToken);
}
