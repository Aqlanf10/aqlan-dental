using System.Reflection;
using AqlanDentalPro.Application.Services;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// Tests for AuthService static hashing methods.
/// Uses reflection to test the private VerifyPassword method.
/// These tests validate password hashing consistency, salt uniqueness,
/// and verification correctness without requiring database context.
/// </summary>
public class AuthServiceTests
{
    // ─── HashPassword Tests ─────────────────────────────────────────────────

    [Fact]
    public void HashPassword_SamePasswordAndSalt_ProducesConsistentHash()
    {
        var password = "TestPassword123!";
        var salt = AuthService.GenerateSalt();

        var hash1 = AuthService.HashPassword(password, salt);
        var hash2 = AuthService.HashPassword(password, salt);

        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashPassword_DifferentPasswords_ProducesDifferentHashes()
    {
        var salt = AuthService.GenerateSalt();

        var hash1 = AuthService.HashPassword("PasswordOne", salt);
        var hash2 = AuthService.HashPassword("PasswordTwo", salt);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPassword_DifferentSalts_ProducesDifferentHashes()
    {
        var password = "SamePassword123!";
        var salt1 = AuthService.GenerateSalt();
        var salt2 = AuthService.GenerateSalt();

        var hash1 = AuthService.HashPassword(password, salt1);
        var hash2 = AuthService.HashPassword(password, salt2);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPassword_ReturnsBase64String()
    {
        var hash = AuthService.HashPassword("test", AuthService.GenerateSalt());

        // Should be valid Base64 (no exceptions when converting)
        var bytes = Convert.FromBase64String(hash);
        bytes.Should().NotBeEmpty();
    }

    // ─── GenerateSalt Tests ─────────────────────────────────────────────────

    [Fact]
    public void GenerateSalt_ReturnsNonNullString()
    {
        var salt = AuthService.GenerateSalt();
        salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateSalt_ProducesUniqueSalts()
    {
        var salts = new HashSet<string>();
        const int iterations = 50;

        for (var i = 0; i < iterations; i++)
        {
            salts.Add(AuthService.GenerateSalt());
        }

        salts.Count.Should().Be(iterations, "each generated salt should be unique");
    }

    [Fact]
    public void GenerateSalt_ReturnsBase64String()
    {
        var salt = AuthService.GenerateSalt();

        // Should be valid Base64
        var bytes = Convert.FromBase64String(salt);
        bytes.Should().NotBeEmpty();
    }

    // ─── VerifyPassword Tests (via reflection) ──────────────────────────────

    private static readonly MethodInfo VerifyPasswordMethod =
        typeof(AuthService).GetMethod("VerifyPassword",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [typeof(string), typeof(string), typeof(string)],
            null)!;

    private static bool CallVerifyPassword(string password, string storedHash, string storedSalt)
    {
        return (bool)VerifyPasswordMethod.Invoke(null, [password, storedHash, storedSalt])!;
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var password = "CorrectPassword123!";
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(password, salt);

        var result = CallVerifyPassword(password, hash, salt);
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var wrongPassword = "WrongPassword999!";
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(password, salt);

        var result = CallVerifyPassword(wrongPassword, hash, salt);
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_EmptyPassword_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var salt = AuthService.GenerateSalt();
        var hash = AuthService.HashPassword(password, salt);

        var result = CallVerifyPassword("", hash, salt);
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_CorruptedHash_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var salt = AuthService.GenerateSalt();

        var result = CallVerifyPassword(password, "not-a-valid-base64-hash!!!", salt);
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_CorruptedSalt_ReturnsFalse()
    {
        var password = "CorrectPassword123!";
        var hash = AuthService.HashPassword(password, AuthService.GenerateSalt());

        var result = CallVerifyPassword(password, hash, "not-valid-salt!!!");
        result.Should().BeFalse();
    }
}
