using System.Text.Json;
using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Security;

/// <summary>
/// W04 regression coverage for refresh-token rotation and response secrecy.
/// </summary>
public sealed class W04AuthSessionSecurityTests
{
    [Fact]
    public async Task StaffRefresh_UsesSingleAtomicConsumeAndRotateOperation()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt"
        };

        var users = new Mock<IUserRepository>();
        users.Setup(repo => repo.GetByIdWithDoctorAsync(userId)).ReturnsAsync(user);

        var tokens = new Mock<ITokenService>(MockBehavior.Strict);
        tokens.Setup(service => service.GenerateRefreshToken()).Returns("replacement-refresh");
        tokens.Setup(service => service.GenerateAccessToken(user)).Returns("replacement-access");
        tokens.Setup(service => service.RotateRefreshTokenAsync(
                userId,
                "current-refresh",
                "replacement-refresh"))
            .ReturnsAsync(true);

        var logger = new Mock<ILogger<AuthService>>();
        var service = new AuthService(users.Object, tokens.Object, logger.Object);

        var result = await service.RefreshAsync(userId, "current-refresh");

        result.Should().NotBeNull();
        result!.Value.accessToken.Should().Be("replacement-access");
        result.Value.refreshToken.Should().Be("replacement-refresh");
        tokens.Verify(service => service.RotateRefreshTokenAsync(
            userId,
            "current-refresh",
            "replacement-refresh"), Times.Once);
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StaffRefresh_WhenTokenWasAlreadyConsumed_DoesNotIssueSession()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Username = "admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = "hash",
            PasswordSalt = "salt"
        };

        var users = new Mock<IUserRepository>();
        users.Setup(repo => repo.GetByIdWithDoctorAsync(userId)).ReturnsAsync(user);

        var tokens = new Mock<ITokenService>(MockBehavior.Strict);
        tokens.Setup(service => service.GenerateRefreshToken()).Returns("replacement-refresh");
        tokens.Setup(service => service.GenerateAccessToken(user)).Returns("replacement-access");
        tokens.Setup(service => service.RotateRefreshTokenAsync(
                userId,
                "replayed-refresh",
                "replacement-refresh"))
            .ReturnsAsync(false);

        var logger = new Mock<ILogger<AuthService>>();
        var service = new AuthService(users.Object, tokens.Object, logger.Object);

        var result = await service.RefreshAsync(userId, "replayed-refresh");

        result.Should().BeNull("a consumed refresh token must not be replayable");
        tokens.Verify(service => service.RotateRefreshTokenAsync(
            userId,
            "replayed-refresh",
            "replacement-refresh"), Times.Once);
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public void PatientAuthResponse_NeverSerializesPlaintextRefreshToken()
    {
        const string secret = "plain-refresh-token-that-must-stay-in-the-cookie";
        var response = new PatientAuthResponse
        {
            AccessToken = "short-lived-access",
            Profile = new PatientPortalProfileDto
            {
                Id = Guid.NewGuid(),
                PatientNumber = "PT-001",
                FullName = "Test Patient"
            },
            MustChangePassword = false,
            RefreshToken = secret
        };

        var json = JsonSerializer.Serialize(response);

        json.Should().Contain("short-lived-access");
        json.Should().NotContain(secret);
        json.Should().NotContain("RefreshToken");
        json.Should().NotContain("refreshToken");
    }
}
