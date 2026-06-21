using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.PortalAuth;

/// <summary>
/// SEC-11 — integration-style tests for the portal ChangePassword endpoint.
/// Verifies that a weak password is rejected with an Arabic error before any
/// hashing happens, and that the stored password hash is left untouched.
///
/// These tests cover the service layer (PatientPortalService.ChangePasswordAsync)
/// which the PatientPortalController.ChangePassword endpoint delegates to. The
/// controller returns the service's Arabic error verbatim as
/// BadRequest(new { message = error }) — so the contract holds end-to-end.
/// </summary>
public class Sec11PortalChangePasswordPolicyTests
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
        AppDbContext db, string username = "PT0001", string currentPassword = "CurrentPass1")
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
        var hash = AuthService.HashPassword(currentPassword, salt);

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

    // ─── 1. Weak password (too short) is rejected with Arabic error ──────────

    [Fact]
    public async Task ChangePassword_TooShortPassword_RejectedWithArabicError()
    {
        await using var db = CreateInMemoryDb();
        var (patientId, account) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);
        var originalHash = account.PasswordHash;
        var originalSalt = account.PasswordSalt;

        var (response, error) = await service.ChangePasswordAsync(patientId, "CurrentPass1", "Ab1");

        response.Should().BeNull("a weak password must not produce an auth response");
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("8", "the error should explain the minimum length");

        // Defense-in-depth: the stored hash MUST be untouched after a rejected change.
        var unchanged = await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.PatientId == patientId);
        unchanged.PasswordHash.Should().Be(originalHash);
        unchanged.PasswordSalt.Should().Be(originalSalt);
    }

    // ─── 2. Weak password (no digit) is rejected with Arabic error ───────────

    [Fact]
    public async Task ChangePassword_NoDigit_RejectedWithArabicError()
    {
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        var (response, error) = await service.ChangePasswordAsync(patientId, "CurrentPass1", "NoDigitsHere");

        response.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("رقم");
    }

    // ─── 3. Weak password (no uppercase) is rejected with Arabic error ───────

    [Fact]
    public async Task ChangePassword_NoUppercase_RejectedWithArabicError()
    {
        await using var db = CreateInMemoryDb();
        var (patientId, _) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);

        var (response, error) = await service.ChangePasswordAsync(patientId, "CurrentPass1", "alllowercase1");

        response.Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("كبير");
    }

    // ─── 4. Strong password is accepted and rotates the hash ─────────────────

    [Fact]
    public async Task ChangePassword_StrongPassword_SucceedsAndRotatesHash()
    {
        await using var db = CreateInMemoryDb();
        var (patientId, account) = await SeedPatientWithAccountAsync(db);
        var service = CreateService(db);
        // Capture BEFORE the call — `account` is a tracked entity that the
        // service mutates in-place, so reading account.PasswordHash afterwards
        // would return the NEW value.
        var originalSalt = account.PasswordSalt;
        var originalHash = account.PasswordHash;

        var (response, error) = await service.ChangePasswordAsync(patientId, "CurrentPass1", "NewStrong1");

        error.Should().BeNull();
        response.Should().NotBeNull();
        response!.AccessToken.Should().NotBeNullOrEmpty();
        response.RefreshToken.Should().NotBeNullOrEmpty();

        // The salt must have been rotated (new random salt per change).
        var updated = await db.PatientAccounts.AsNoTracking().FirstAsync(a => a.PatientId == patientId);
        updated.PasswordSalt.Should().NotBe(originalSalt);
        updated.PasswordHash.Should().NotBe(originalHash);
        updated.MustChangePassword.Should().BeFalse();
    }

    // ─── 5. Controller returns BadRequest with the Arabic message ─────────────
    //
    // Verifies the HTTP-level contract: PatientPortalController.ChangePassword
    // unwraps the (null, error) tuple into a 400 BadRequest with { message }.

    [Fact]
    public async Task Controller_ChangePassword_WeakPassword_Returns400WithArabicMessage()
    {
        // Arrange: a portal service mock that simulates the policy rejection.
        var patientId = Guid.NewGuid();
        var portalService = new Mock<IPatientPortalService>();
        portalService
            .Setup(s => s.ChangePasswordAsync(patientId, "CurrentPass1", "weak"))
            .ReturnsAsync((null as PatientAuthResponse, "كلمة المرور يجب ألا تقل عن 8 أحرف"));

        var config = new Mock<IConfiguration>();
        var logger = new Mock<ILogger<PatientPortalController>>();
        var recaptcha = new Mock<IRecaptchaService>();
        var controller = new PatientPortalController(portalService.Object, config.Object, logger.Object, recaptcha.Object);

        // Stub User claims so GetPatientId() returns our test patient.
        var claims = new[] { new System.Security.Claims.Claim("patientId", patientId.ToString()) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "Test"))
            }
        };

        var req = new PatientChangePasswordRequest
        {
            CurrentPassword = "CurrentPass1",
            NewPassword = "weak"
        };

        // Act
        var result = await controller.ChangePassword(req);

        // Assert — 400 BadRequest with the Arabic policy message.
        result.Should().BeOfType<BadRequestObjectResult>();
        var bad = (BadRequestObjectResult)result;
        var messageProp = bad.Value!.GetType().GetProperty("message");
        messageProp.Should().NotBeNull();
        var message = (string?)messageProp!.GetValue(bad.Value);
        message.Should().NotBeNullOrEmpty();
        message.Should().Contain("8", "the response message should be the Arabic policy error");
    }
}
