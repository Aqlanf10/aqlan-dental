using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Application.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task StoreRefreshTokenAsync(Guid userId, string refreshToken);
    Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken);
    Task RevokeRefreshTokenAsync(Guid userId, string refreshToken);
    Task RevokeAllRefreshTokensAsync(Guid userId);
}
