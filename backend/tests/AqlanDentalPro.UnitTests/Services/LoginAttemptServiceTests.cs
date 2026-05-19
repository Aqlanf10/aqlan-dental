using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Tests for LoginAttemptService Redis resilience.
/// Verifies that when Redis is unavailable, login still works
/// (fail-open) and no exceptions propagate to the caller.
/// </summary>
public class LoginAttemptServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _mockConnection;
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<LoginAttemptService>> _mockLogger;

    public LoginAttemptServiceTests()
    {
        _mockConnection = new Mock<IConnectionMultiplexer>();
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<LoginAttemptService>>();

        _mockConnection.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);
    }

    private LoginAttemptService CreateService() => new(_mockConnection.Object, _mockLogger.Object);

    // ── IsLockedOutAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisThrowsConnectionException_ReturnsNotLocked()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var (isLocked, remainingMinutes) = await service.IsLockedOutAsync("testuser");

        // Assert
        isLocked.Should().BeFalse();
        remainingMinutes.Should().Be(0);
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisThrowsTimeoutException_ReturnsNotLocked()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisTimeoutException("Timeout performing StringGet", CommandStatus.Unknown));

        var service = CreateService();

        // Act
        var (isLocked, remainingMinutes) = await service.IsLockedOutAsync("testuser");

        // Assert
        isLocked.Should().BeFalse();
        remainingMinutes.Should().Be(0);
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisThrowsGenericRedisException_ReturnsNotLocked()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var (isLocked, remainingMinutes) = await service.IsLockedOutAsync("testuser");

        // Assert
        isLocked.Should().BeFalse();
        remainingMinutes.Should().Be(0);
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisReturnsLockedValue_ReturnsLocked()
    {
        // Arrange
        var lockUntil = DateTime.UtcNow.AddMinutes(10);
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockUntil.ToString("O")));

        var service = CreateService();

        // Act
        var (isLocked, remainingMinutes) = await service.IsLockedOutAsync("testuser");

        // Assert
        isLocked.Should().BeTrue();
        remainingMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisReturnsExpiredLock_ReturnsNotLocked()
    {
        // Arrange
        var lockUntil = DateTime.UtcNow.AddMinutes(-5); // expired
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue(lockUntil.ToString("O")));
        _mockDatabase
            .Setup(db => db.KeyDelete(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(true);

        var service = CreateService();

        // Act
        var (isLocked, remainingMinutes) = await service.IsLockedOutAsync("testuser");

        // Assert
        isLocked.Should().BeFalse();
        remainingMinutes.Should().Be(0);
    }

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisReturnsEmpty_ReturnsNotLocked()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = CreateService();

        // Act
        var (isLocked, remainingMinutes) = await service.IsLockedOutAsync("testuser");

        // Assert
        isLocked.Should().BeFalse();
        remainingMinutes.Should().Be(0);
    }

    // ── RecordFailedAttemptAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordFailedAttemptAsync_WhenRedisThrowsConnectionException_ReturnsZero()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var count = await service.RecordFailedAttemptAsync("testuser");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_WhenRedisThrowsTimeoutException_ReturnsZero()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Throws(new RedisTimeoutException("Timeout performing StringGet", CommandStatus.Unknown));

        var service = CreateService();

        // Act
        var count = await service.RecordFailedAttemptAsync("testuser");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_WhenRedisThrowsGenericRedisException_ReturnsZero()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Throws(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var count = await service.RecordFailedAttemptAsync("testuser");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_WhenRedisWorks_ReturnsIncrementedCount()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(RedisValue.Null);
        _mockDatabase
            .Setup(db => db.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(true);

        var service = CreateService();

        // Act
        var count = await service.RecordFailedAttemptAsync("testuser");

        // Assert — first attempt, no previous count, should return 1
        count.Should().Be(1);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_WithExistingCount_IncrementsAndReturns()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGet(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns(new RedisValue("3")); // 3 previous failures
        _mockDatabase
            .Setup(db => db.StringSet(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Returns(true);

        var service = CreateService();

        // Act
        var count = await service.RecordFailedAttemptAsync("testuser");

        // Assert
        count.Should().Be(4);
    }

    // ── ResetFailedAttemptsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task ResetFailedAttemptsAsync_WhenRedisThrowsConnectionException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.KeyDelete(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "It was not possible to connect to the redis server(s)"));

        var service = CreateService();

        // Act
        var act = () => service.ResetFailedAttemptsAsync("testuser");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetFailedAttemptsAsync_WhenRedisThrowsTimeoutException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.KeyDelete(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Throws(new RedisTimeoutException("Timeout performing KeyDelete", CommandStatus.Unknown));

        var service = CreateService();

        // Act
        var act = () => service.ResetFailedAttemptsAsync("testuser");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetFailedAttemptsAsync_WhenRedisThrowsGenericRedisException_DoesNotThrow()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.KeyDelete(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Throws(new RedisException("Unexpected Redis error"));

        var service = CreateService();

        // Act
        var act = () => service.ResetFailedAttemptsAsync("testuser");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetFailedAttemptsAsync_WhenRedisWorks_CompletesSuccessfully()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.KeyDelete(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .Returns(2);

        var service = CreateService();

        // Act
        var act = () => service.ResetFailedAttemptsAsync("testuser");

        // Assert
        await act.Should().NotThrowAsync();
    }

    // ── No sensitive data in logs ──────────────────────────────────────────────

    [Fact]
    public async Task IsLockedOutAsync_WhenRedisFails_DoesNotLogPasswordsOrTokens()
    {
        // Arrange
        _mockDatabase
            .Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect,
                "Connection failed"));

        var service = CreateService();

        // Act
        await service.IsLockedOutAsync("testuser");

        // Assert — verify logger was called but never with passwords/tokens
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    !v.ToString()!.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                    !v.ToString()!.Contains("token", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
