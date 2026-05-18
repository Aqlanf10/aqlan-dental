using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Tests for TokenService Redis resilience.
/// Verifies that when Redis is unavailable, the auth flow does not crash:
/// - StoreRefreshTokenAsync silently fails (login still succeeds)
/// - ValidateRefreshTokenAsync returns false (user must re-login)
/// - RevokeRefreshTokenAsync silently continues
/// - GetOwnerOfRefreshTokenAsync returns null
/// - RevokeAllRefreshTokensAsync silently continues
/// </summary>
public class TokenServiceRedisResilienceTests
{
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<IConnectionMultiplexer> _mockConnection;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<TokenService>> _mockLogger;

    public TokenServiceRedisResilienceTests()
    {
        _mockConfig = new Mock<IConfiguration>();
        _mockConnection = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<TokenService>>();

        _mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        // Setup config for JWT
        _mockConfig.Setup(c => c["Jwt:SecretKey"]).Returns("ThisIsATestSecretKeyThatIsLongEnoughForHmacSha256Algorithm");
        _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        _mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        _mockConfig.Setup(c => c["Jwt:AccessTokenExpiryMinutes"]).Returns("15");
        _mockConfig.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");
    }

    private TokenService CreateService() => new(_mockConfig.Object, _mockConnection.Object, _mockLogger.Object);

    // ── StoreRefreshTokenAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenRedisThrowsConnectionException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.CreateBatch(It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var act = () => service.StoreRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenRedisThrowsTimeoutException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.CreateBatch(It.IsAny<object>()))
            .Throws(new RedisTimeoutException("Timeout performing CreateBatch", CommandStatus.Unknown));

        var service = CreateService();

        // Act
        var act = () => service.StoreRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenRedisThrowsGenericException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.CreateBatch(It.IsAny<object>()))
            .Throws(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var act = () => service.StoreRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── ValidateRefreshTokenAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ValidateRefreshTokenAsync_WhenRedisThrowsConnectionException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var result = await service.ValidateRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WhenRedisThrowsTimeoutException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisTimeoutException("Timeout performing StringGet", CommandStatus.Unknown));

        var service = CreateService();

        // Act
        var result = await service.ValidateRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateRefreshTokenAsync_WhenRedisThrowsGenericException_ReturnsFalse()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var result = await service.ValidateRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        result.Should().BeFalse();
    }

    // ── RevokeRefreshTokenAsync ────────────────────────────────────────────────

    [Fact]
    public async Task RevokeRefreshTokenAsync_WhenRedisThrowsConnectionException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var act = () => service.RevokeRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_WhenRedisThrowsGenericException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var act = () => service.RevokeRefreshTokenAsync(Guid.NewGuid(), "sometoken");

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── GetOwnerOfRefreshTokenAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetOwnerOfRefreshTokenAsync_WhenRedisThrowsConnectionException_ReturnsNull()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var result = await service.GetOwnerOfRefreshTokenAsync("sometoken");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOwnerOfRefreshTokenAsync_WhenRedisThrowsGenericException_ReturnsNull()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var result = await service.GetOwnerOfRefreshTokenAsync("sometoken");

        // Assert
        result.Should().BeNull();
    }

    // ── RevokeAllRefreshTokensAsync ────────────────────────────────────────────

    [Fact]
    public async Task RevokeAllRefreshTokensAsync_WhenRedisThrowsConnectionException_DoesNotThrow()
    {
        // Arrange
        _mockConnection
            .Setup(c => c.GetEndPoints(It.IsAny<bool>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var act = () => service.RevokeAllRefreshTokensAsync(Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAllRefreshTokensAsync_WhenRedisThrowsGenericException_DoesNotThrow()
    {
        // Arrange
        _mockConnection
            .Setup(c => c.GetEndPoints(It.IsAny<bool>()))
            .Throws(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var act = () => service.RevokeAllRefreshTokensAsync(Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── No sensitive data in logs ──────────────────────────────────────────────

    [Fact]
    public async Task StoreRefreshTokenAsync_WhenRedisFails_DoesNotLogPasswordsOrTokens()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.CreateBatch(It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "Connection failed"));

        var service = CreateService();

        // Act
        await service.StoreRefreshTokenAsync(Guid.NewGuid(), "superSecretToken123");

        // Assert — verify logger was called but never with passwords/tokens
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    !v.ToString()!.Contains("superSecretToken123", StringComparison.OrdinalIgnoreCase) &&
                    !v.ToString()!.Contains("password", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
