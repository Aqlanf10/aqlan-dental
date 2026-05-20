using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Generates and manages JWT access/refresh tokens.
///
/// HOTFIX: All Redis operations are wrapped in try/catch to prevent
/// Redis unavailability from breaking the auth flow. When Redis is down:
/// - StoreRefreshTokenAsync silently fails — refresh token not persisted, user must re-login to refresh
/// - ValidateRefreshTokenAsync returns false — user must re-login
/// - RevokeRefreshTokenAsync silently continues — token not revoked but will expire naturally
/// - GetOwnerOfRefreshTokenAsync returns null — user must re-login
/// - RevokeAllRefreshTokensAsync silently continues — tokens expire naturally
///
/// Access token generation (JWT) is unaffected — it does not use Redis.
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IConfiguration config, IConnectionMultiplexer redis, ILogger<TokenService> logger)
    {
        _config = config;
        _redis = redis;
        _db = redis.GetDatabase();
        _logger = logger;
    }

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15"));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("branchId", user.BranchId?.ToString() ?? ""),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // SEC-02 FIX: Include mustChangePassword claim so middleware can enforce password change
            new("mustChangePassword", user.MustChangePassword.ToString().ToLowerInvariant()),
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
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
        try
        {
            var hash = HashToken(refreshToken);
            var expiry = TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));
            var tokenKey = $"refresh:{userId}:{hash[..16]}";
            var ownerKey = $"refresh:owner:{hash[..16]}";
            var batch = _db.CreateBatch();
            var task1 = batch.StringSetAsync(tokenKey, hash, expiry);
            var task2 = batch.StringSetAsync(ownerKey, userId.ToString(), expiry);
            batch.Execute();
            // LOGIN FIX: Properly await the batch tasks instead of Task.CompletedTask.
            // Without this, the refresh token may not be persisted to Redis before the
            // login response is sent, causing refresh-token operations to fail.
            await Task.WhenAll(task1, task2);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during StoreRefreshTokenAsync for user '{UserId}'. Refresh token not persisted — user will need to re-login to refresh.",
                userId);
            // Silently continue — login still succeeds, but refresh token won't be validatable
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during StoreRefreshTokenAsync for user '{UserId}'. Refresh token not persisted.",
                userId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during StoreRefreshTokenAsync for user '{UserId}'. Refresh token not persisted.",
                userId);
        }
    }

    public async Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken)
    {
        try
        {
            var hash = HashToken(refreshToken);
            var key = $"refresh:{userId}:{hash[..16]}";
            var stored = await _db.StringGetAsync(key);
            return stored.HasValue && stored == hash;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during ValidateRefreshTokenAsync for user '{UserId}'. Assuming token is invalid — user must re-login.",
                userId);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during ValidateRefreshTokenAsync for user '{UserId}'. Assuming token is invalid.",
                userId);
            return false;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during ValidateRefreshTokenAsync for user '{UserId}'. Assuming token is invalid.",
                userId);
            return false;
        }
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, string refreshToken)
    {
        try
        {
            var hash = HashToken(refreshToken);
            await _db.KeyDeleteAsync(new RedisKey[]
            {
                $"refresh:{userId}:{hash[..16]}",
                $"refresh:owner:{hash[..16]}"
            });
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during RevokeRefreshTokenAsync for user '{UserId}'. Token not revoked — will expire naturally.",
                userId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during RevokeRefreshTokenAsync for user '{UserId}'. Token not revoked.",
                userId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during RevokeRefreshTokenAsync for user '{UserId}'. Token not revoked.",
                userId);
        }
    }

    public async Task<Guid?> GetOwnerOfRefreshTokenAsync(string refreshToken)
    {
        try
        {
            var hash = HashToken(refreshToken);
            var stored = await _db.StringGetAsync($"refresh:owner:{hash[..16]}");
            if (!stored.HasValue) return null;
            return Guid.TryParse(stored.ToString(), out var id) ? id : null;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during GetOwnerOfRefreshTokenAsync. Assuming token has no owner — user must re-login.");
            return null;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during GetOwnerOfRefreshTokenAsync. Assuming token has no owner.");
            return null;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during GetOwnerOfRefreshTokenAsync. Assuming token has no owner.");
            return null;
        }
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"refresh:{userId}:*").ToArray();
            if (keys.Length > 0)
                await _db.KeyDeleteAsync(keys);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis unavailable during RevokeAllRefreshTokensAsync for user '{UserId}'. Tokens not revoked — will expire naturally.",
                userId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex, "Redis timeout during RevokeAllRefreshTokensAsync for user '{UserId}'. Tokens not revoked.",
                userId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error during RevokeAllRefreshTokensAsync for user '{UserId}'. Tokens not revoked.",
                userId);
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
