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
/// Generates JWT access tokens and manages opaque refresh tokens in Redis.
/// Only SHA-256 hashes are persisted. Rotation is performed atomically so two
/// concurrent refresh requests cannot both consume the same credential.
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

    public string GenerateAccessToken(User user) => GenerateAccessToken(user, null, null);

    public string GenerateAccessToken(User user, Guid? originalUserId = null, string? originalRole = null)
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
            new("mustChangePassword", user.MustChangePassword.ToString().ToLowerInvariant()),
        };

        if (originalUserId.HasValue)
        {
            claims.Add(new Claim("originalUserId", originalUserId.Value.ToString()));
            claims.Add(new Claim("isImpersonating", "true"));
            if (!string.IsNullOrEmpty(originalRole))
                claims.Add(new Claim("originalRole", originalRole));
        }

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
            var expiry = GetRefreshExpiry();
            var prefix = hash[..16];
            var batch = _db.CreateBatch();
            var tokenTask = batch.StringSetAsync($"refresh:{userId}:{prefix}", hash, expiry);
            var ownerTask = batch.StringSetAsync($"refresh:owner:{prefix}", userId.ToString(), expiry);
            batch.Execute();
            await Task.WhenAll(tokenTask, ownerTask);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex,
                "Redis unavailable during StoreRefreshTokenAsync for user '{UserId}'. Refresh token was not persisted.",
                userId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex,
                "Redis timeout during StoreRefreshTokenAsync for user '{UserId}'. Refresh token was not persisted.",
                userId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during StoreRefreshTokenAsync for user '{UserId}'. Refresh token was not persisted.",
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
            return stored.HasValue && FixedTimeEquals(stored.ToString(), hash);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex,
                "Redis unavailable during ValidateRefreshTokenAsync for user '{UserId}'. Treating token as invalid.",
                userId);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex,
                "Redis timeout during ValidateRefreshTokenAsync for user '{UserId}'. Treating token as invalid.",
                userId);
            return false;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during ValidateRefreshTokenAsync for user '{UserId}'. Treating token as invalid.",
                userId);
            return false;
        }
    }

    public async Task<bool> RotateRefreshTokenAsync(
        Guid userId,
        string currentRefreshToken,
        string replacementRefreshToken)
    {
        try
        {
            var currentHash = HashToken(currentRefreshToken);
            var replacementHash = HashToken(replacementRefreshToken);
            var currentPrefix = currentHash[..16];
            var replacementPrefix = replacementHash[..16];
            var expiryMilliseconds = checked((long)GetRefreshExpiry().TotalMilliseconds);

            const string script = """
                local current = redis.call('GET', KEYS[1])
                if (not current) or current ~= ARGV[1] then
                    return 0
                end

                redis.call('DEL', KEYS[1])
                redis.call('DEL', KEYS[2])
                redis.call('SET', KEYS[3], ARGV[2], 'PX', ARGV[4])
                redis.call('SET', KEYS[4], ARGV[3], 'PX', ARGV[4])
                return 1
                """;

            var result = await _db.ScriptEvaluateAsync(
                script,
                new RedisKey[]
                {
                    $"refresh:{userId}:{currentPrefix}",
                    $"refresh:owner:{currentPrefix}",
                    $"refresh:{userId}:{replacementPrefix}",
                    $"refresh:owner:{replacementPrefix}"
                },
                new RedisValue[]
                {
                    currentHash,
                    replacementHash,
                    userId.ToString(),
                    expiryMilliseconds
                });

            return (long)result == 1;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex,
                "Redis unavailable during atomic refresh-token rotation for user '{UserId}'.",
                userId);
            return false;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex,
                "Redis timeout during atomic refresh-token rotation for user '{UserId}'.",
                userId);
            return false;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during atomic refresh-token rotation for user '{UserId}'.",
                userId);
            return false;
        }
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, string refreshToken)
    {
        try
        {
            var hash = HashToken(refreshToken);
            var prefix = hash[..16];
            await _db.KeyDeleteAsync(new RedisKey[]
            {
                $"refresh:{userId}:{prefix}",
                $"refresh:owner:{prefix}"
            });
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex,
                "Redis unavailable during RevokeRefreshTokenAsync for user '{UserId}'. Token will expire naturally.",
                userId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex,
                "Redis timeout during RevokeRefreshTokenAsync for user '{UserId}'.",
                userId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during RevokeRefreshTokenAsync for user '{UserId}'.",
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
            _logger.LogError(ex,
                "Redis unavailable during GetOwnerOfRefreshTokenAsync. Treating token as unowned.");
            return null;
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex,
                "Redis timeout during GetOwnerOfRefreshTokenAsync. Treating token as unowned.");
            return null;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during GetOwnerOfRefreshTokenAsync. Treating token as unowned.");
            return null;
        }
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var tokenKeys = server.Keys(pattern: $"refresh:{userId}:*").ToArray();
            if (tokenKeys.Length == 0) return;

            // Owner lookup keys must be removed too. Leaving them behind allowed a
            // revoked token to resolve to a userId before validation, producing
            // stale session metadata and unnecessary refresh attempts.
            var storedHashes = await _db.StringGetAsync(tokenKeys);
            var ownerKeys = storedHashes
                .Where(value => value.HasValue && value.ToString().Length >= 16)
                .Select(value => (RedisKey)$"refresh:owner:{value.ToString()[..16]}")
                .ToArray();

            await _db.KeyDeleteAsync(tokenKeys.Concat(ownerKeys).ToArray());
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex,
                "Redis unavailable during RevokeAllRefreshTokensAsync for user '{UserId}'. Tokens will expire naturally.",
                userId);
        }
        catch (RedisTimeoutException ex)
        {
            _logger.LogError(ex,
                "Redis timeout during RevokeAllRefreshTokensAsync for user '{UserId}'.",
                userId);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error during RevokeAllRefreshTokensAsync for user '{UserId}'.",
                userId);
        }
    }

    private TimeSpan GetRefreshExpiry() =>
        TimeSpan.FromDays(int.Parse(_config["Jwt:RefreshTokenExpiryDays"] ?? "7"));

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
