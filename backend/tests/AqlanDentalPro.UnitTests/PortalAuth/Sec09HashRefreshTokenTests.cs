using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace AqlanDentalPro.UnitTests.PortalAuth;

/// <summary>
/// SEC-09 — Patient portal refresh tokens must be stored as SHA-256 hashes,
/// never as plaintext. These tests verify the four required behaviors:
///   1. Login stores a hash, not plaintext.
///   2. Refresh with a valid token succeeds AND rotates (issues a new token).
///   3. After rotation, the OLD token no longer works.
///   4. An invalid token fails.
///   5. Logout clears the stored hash.
/// </summary>
public class Sec09HashRefreshTokenTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static PatientPortalService CreateService(AppDbContext db)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Jwt:SecretKey"]).Returns("ThisIsAVeryLongSecretKeyForTestingPurposesOnly123!");
        config.Setup(c => c["Jwt:Issuer"]).Returns("AqlanDentalPro");
        config.Setup(c => c["Jwt:Audience"]).Returns("AqlanDentalPro");
        config.Setup(c => c["WhatsApp:ApiUrl"]).Returns(string.Empty);
        config.Setup(c => c["WhatsApp:ApiToken"]).Returns(string.Empty);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var linkingService = new Mock<IPatientAccountLinkingService>();
        var logger = new Mock<ILogger<PatientPortalService>>();
        return new PatientPortalService(db, config.Object, httpClientFactory.Object, linkingService.Object, logger.Object);
    }

    private static async Task<(Guid patientId, PatientAccount account)> SeedPatientWithAccountAsync(
        AppDbContext db, string username = "PT0001")
    {
        var patientId = Guid.NewGuid();
        db.Patients.Add(new Patient
        {
            Id = patientId,
            PatientNumber = username,
            FirstName = "سارة",
            LastName = "أحمد",
            Phone = "+967770222001",
            IsActive = true
        });

        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword("TempPass1", salt);

        var account = new PatientAccount
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            PhoneNumber = "+967770222001",
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            MustChangePassword = false,
            PortalAccountActive = true,
            IsVerified = true,
            IsActive = true
        };
        db.PatientAccounts.Add(account);
        await db.SaveChangesAsync();
        return (patientId, account);
    }

    /// <summary>
    /// Local SHA-256 helper that mirrors the production HashToken implementation.
    /// Used to compute the EXPECTED hash of the returned plaintext token so the
    /// test can assert that the stored value equals SHA-256(plaintext), not the
    /// plaintext itself.
    /// </summary>
    private static string ExpectedHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // ─── 1. Login stores hash, not plaintext ──────────────────────────────────

    [Fact]
    public async Task Login_StoresHashNotPlaintext()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        // Act
        var (response, error) = await service.LoginAsync("PT0001", "TempPass1");

        // Assert — login must succeed and return a plaintext refresh token
        error.Should().BeNull();
        response.Should().NotBeNull();
        response!.RefreshToken.Should().NotBeNullOrEmpty("login must return the plaintext refresh token to the client");

        // The persisted value must be the SHA-256 hash of the returned plaintext token,
        // NOT the plaintext token itself.
        var account = await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.PatientId == patientId);
        account.RefreshTokenHash.Should().NotBeNullOrEmpty("a hash must be persisted after login");
        account.RefreshTokenHash.Should().NotBe(response.RefreshToken, "must not store the plaintext token");
        account.RefreshTokenHash.Should().Be(
            ExpectedHash(response.RefreshToken!),
            "stored value must equal SHA-256(plaintext)");
        account.RefreshTokenExpiry.Should().NotBeNull();
        account.RefreshTokenExpiry!.Value.Should().BeAfter(DateTime.UtcNow.AddMinutes(1));
    }

    // ─── 2. Refresh with valid token succeeds AND rotates ─────────────────────

    [Fact]
    public async Task Refresh_WithValidToken_Succeeds_AndRotates()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        var (loginResponse, _) = await service.LoginAsync("PT0001", "TempPass1");
        var firstToken = loginResponse!.RefreshToken!;

        // Act — refresh using the plaintext token from login
        var (refreshResponse, refreshError) = await service.RefreshTokenAsync(patientId, firstToken);

        // Assert — refresh succeeds and returns a NEW, different plaintext token
        refreshError.Should().BeNull();
        refreshResponse.Should().NotBeNull();
        refreshResponse!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResponse.RefreshToken.Should().NotBeNullOrEmpty("rotation must issue a new plaintext token");
        refreshResponse.RefreshToken.Should().NotBe(firstToken, "rotation must produce a different token");

        // The persisted hash must be the hash of the NEW token, not the old token.
        var account = await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.PatientId == patientId);
        account.RefreshTokenHash.Should().Be(ExpectedHash(refreshResponse.RefreshToken!));
        account.RefreshTokenHash.Should().NotBe(ExpectedHash(firstToken));
    }

    // ─── 3. After rotation, the old token no longer works ─────────────────────

    [Fact]
    public async Task Refresh_WithOldTokenAfterRotation_Fails()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        var (loginResponse, _) = await service.LoginAsync("PT0001", "TempPass1");
        var oldToken = loginResponse!.RefreshToken!;

        // First refresh succeeds and rotates
        var (firstRefresh, firstError) = await service.RefreshTokenAsync(patientId, oldToken);
        firstError.Should().BeNull();
        firstRefresh!.RefreshToken.Should().NotBe(oldToken);

        // Act — try to refresh again with the OLD (rotated-away) token
        var (secondResponse, secondError) = await service.RefreshTokenAsync(patientId, oldToken);

        // Assert — old token is dead (rotation invalidated it server-side)
        secondResponse.Should().BeNull("rotated tokens must not be replayable");
        secondError.Should().NotBeNull();
        secondError.Should().Be("جلسة منتهية، يرجى تسجيل الدخول مجددًا");

        // The new token from the first refresh should still work (sanity check)
        var (thirdResponse, thirdError) = await service.RefreshTokenAsync(patientId, firstRefresh.RefreshToken!);
        thirdError.Should().BeNull();
        thirdResponse.Should().NotBeNull();
    }

    // ─── 4. Refresh with an invalid token fails ───────────────────────────────

    [Fact]
    public async Task Refresh_WithInvalidToken_Fails()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        // Login to populate a valid hash, then attempt refresh with a random string
        var (loginResponse, _) = await service.LoginAsync("PT0001", "TempPass1");
        loginResponse!.RefreshToken.Should().NotBeNullOrEmpty();

        // Act — a random string that hashes to nothing stored in the DB
        var bogusToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var (response, error) = await service.RefreshTokenAsync(patientId, bogusToken);

        // Assert — invalid token is rejected with Arabic message
        response.Should().BeNull();
        error.Should().NotBeNull();
        error.Should().Be("جلسة منتهية، يرجى تسجيل الدخول مجددًا");
    }

    // ─── 5. Logout clears the persisted hash ──────────────────────────────────

    [Fact]
    public async Task Logout_ClearsHash()
    {
        // Arrange
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        var (loginResponse, _) = await service.LoginAsync("PT0001", "TempPass1");
        loginResponse!.RefreshToken.Should().NotBeNullOrEmpty();

        // Pre-condition: hash is set after login
        var beforeLogout = await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.PatientId == patientId);
        beforeLogout.RefreshTokenHash.Should().NotBeNullOrEmpty();
        beforeLogout.RefreshTokenExpiry.Should().NotBeNull();

        // Act
        await service.LogoutAsync(patientId);

        // Assert — hash and expiry are both cleared
        var afterLogout = await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.PatientId == patientId);
        afterLogout.RefreshTokenHash.Should().BeNull();
        afterLogout.RefreshTokenExpiry.Should().BeNull();

        // The pre-logout token can no longer refresh (it has no matching hash)
        var (response, error) = await service.RefreshTokenAsync(patientId, loginResponse.RefreshToken!);
        response.Should().BeNull();
        error.Should().NotBeNull();
    }
}
