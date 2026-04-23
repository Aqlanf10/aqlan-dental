using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.Infrastructure.Services;

public class TokenService(IConfiguration config, IConnectionMultiplexer redis) : ITokenService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:AccessTokenExpiryMinutes"] ?? "15"));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("branchId", user.BranchId?.ToString() ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public async Task StoreRefreshTokenAsync(Guid userId, string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var expiry = TimeSpan.FromDays(int.Parse(config["Jwt:RefreshTokenExpiryDays"] ?? "7"));
        await _db.StringSetAsync($"refresh:{userId}:{hash[..16]}", hash, expiry);
    }

    public async Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var key = $"refresh:{userId}:{hash[..16]}";
        var stored = await _db.StringGetAsync(key);
        return stored.HasValue && stored == hash;
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, string refreshToken)
    {
        var hash = HashToken(refreshToken);
        await _db.KeyDeleteAsync($"refresh:{userId}:{hash[..16]}");
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        var server = redis.GetServer(redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"refresh:{userId}:*").ToArray();
        if (keys.Length > 0)
            await _db.KeyDeleteAsync(keys);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
