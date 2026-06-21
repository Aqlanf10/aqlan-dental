using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.PatientPortal;
using AqlanDentalPro.Application.Interfaces.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace AqlanDentalPro.UnitTests.PortalAuth;

/// <summary>
/// SEC-10 — Patient portal refresh token must be delivered as an HttpOnly +
/// Secure + SameSite cookie (named "portal_refresh"), NOT in the response body
/// for the frontend to store in localStorage. These tests verify the controller
/// behaviors required by the audit fix:
///   1. Login sets the cookie with HttpOnly + Secure + SameSite flags.
///   2. Refresh reads the token from the cookie, ignoring the body.
///   3. Logout deletes the cookie.
///   4. Refresh body-fallback still works (one-release backward compat),
///      and logs a warning so operators can track stale frontends.
///   5. No cookie + no body → 401.
/// Uses DefaultHttpContext + inspects Response.Headers["Set-Cookie"] /
/// sets Request.Headers["Cookie"] — the recommended ASP.NET Core controller-test pattern.
/// </summary>
public class Sec10PortalCookieTests
{
    private const string TestSecret = "ThisIsAVeryLongSecretKeyForTestingPurposesOnly123!";
    private const string TestIssuer = "AqlanDentalPro";
    private const string TestAudience = "AqlanDentalPro";

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a REAL IConfiguration from in-memory values. We can't Moq the
    /// IConfiguration.GetValue&lt;T&gt; extension method (Moq can't mock static
    /// extension methods), so we use a real ConfigurationRoot instead — this
    /// exercises the actual ConfigurationBinder.GetValue path.
    /// </summary>
    private static IConfiguration CreateConfig(bool cookieSecure = true, string sameSite = "Lax")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestSecret,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["PatientPortal:CookieSecure"] = cookieSecure.ToString(),
                ["PatientPortal:CookieSameSite"] = sameSite,
                ["PatientPortal:CookieExpiryDays"] = "7"
            })
            .Build();
    }

    private static PatientPortalController CreateController(
        Mock<IPatientPortalService> svc,
        IConfiguration? config = null,
        Mock<ILogger<PatientPortalController>>? logger = null)
    {
        var recaptcha = new Mock<IRecaptchaService>();
        return new PatientPortalController(
            svc.Object,
            config ?? CreateConfig(),
            (logger ?? new Mock<ILogger<PatientPortalController>>()).Object,
            recaptcha.Object);
    }

    private static PatientAuthResponse OkResponse(string refreshToken = "rt-plain-xyz") => new()
    {
        AccessToken = "access-token-123",
        Profile = new PatientPortalProfileDto
        {
            Id = Guid.NewGuid(),
            PatientNumber = "PT0001",
            FullName = "سارة أحمد"
        },
        MustChangePassword = false,
        RefreshToken = refreshToken
    };

    /// <summary>
    /// Builds a (possibly expired) JWT carrying the patientId claim, signed with
    /// the test secret. Mirrors what the portal frontend sends in the
    /// Authorization header on the refresh-token call.
    /// </summary>
    private static string MakeJwt(Guid patientId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: new[] { new Claim("patientId", patientId.ToString()) },
            expires: DateTime.UtcNow.AddMinutes(-5), // expired — refresh flow allows this
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─── 1. Login sets HttpOnly + Secure + SameSite cookie ──────────────────

    [Fact]
    public async Task Login_SetsPortalRefreshCookie_HttpOnlySecure()
    {
        // Arrange
        var svc = new Mock<IPatientPortalService>();
        svc.Setup(s => s.LoginAsync("PT0001", "TempPass1"))
           .ReturnsAsync((OkResponse("rt-plain-xyz"), (string?)null));

        var controller = CreateController(svc);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await controller.Login(new PatientLoginRequest { Username = "PT0001", Password = "TempPass1" });

        // Assert — response is 200
        result.Should().BeOfType<OkObjectResult>();

        // The Set-Cookie header must carry the refresh token + all security flags.
        // ASP.NET Core writes Set-Cookie attributes in lowercase (e.g. `httponly`,
        // `secure`, `samesite=lax`) per RFC 6265 — use case-insensitive contains.
        var setCookie = controller.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain("portal_refresh=rt-plain-xyz", "the refresh token must be set as a cookie value");
        setCookie.Should().ContainEquivalentOf("HttpOnly", "JS must not be able to read the cookie (XSS protection)");
        setCookie.Should().ContainEquivalentOf("Secure", "cookie must only travel over HTTPS");
        setCookie.Should().ContainEquivalentOf("SameSite=Lax", "default SameSite must be Lax (CSRF protection)");

        // Backward compat — the body still contains the refresh token for one release
        var body = ((OkObjectResult)result).Value.Should().BeOfType<PatientAuthResponse>().Subject;
        body.RefreshToken.Should().Be("rt-plain-xyz", "body field retained for one release (backward compat)");
    }

    // ─── 2. Refresh reads from cookie, NOT body ─────────────────────────────

    [Fact]
    public async Task Refresh_ReadsFromCookie_NotBody()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var svc = new Mock<IPatientPortalService>();
        svc.Setup(s => s.RefreshTokenAsync(patientId, "cookie-token"))
           .ReturnsAsync((OkResponse("rt-rotated-new"), (string?)null))
           .Verifiable();

        var controller = CreateController(svc);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Headers.Authorization = $"Bearer {MakeJwt(patientId)}";
        // Cookie takes precedence over body — set via the raw Cookie header
        // (DefaultHttpContext.Request.Cookies is read-only; the cookie collection
        // is parsed from this header on first access).
        controller.HttpContext.Request.Headers.Cookie = "portal_refresh=cookie-token";

        // Act — body has a DIFFERENT token, must be ignored
        var result = await controller.RefreshToken(new PatientRefreshTokenRequest { RefreshToken = "body-token" });

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        // The service was called with the COOKIE token, never with the body token
        svc.Verify(s => s.RefreshTokenAsync(patientId, "cookie-token"), Times.Once);
        svc.Verify(s => s.RefreshTokenAsync(patientId, "body-token"), Times.Never);

        // A new rotated cookie is set on the response
        var setCookie = controller.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain("portal_refresh=rt-rotated-new");
    }

    // ─── 3. Logout deletes the cookie ───────────────────────────────────────

    [Fact]
    public async Task Logout_DeletesCookie()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var svc = new Mock<IPatientPortalService>();
        svc.Setup(s => s.LogoutAsync(patientId)).Returns(Task.CompletedTask).Verifiable();

        var controller = CreateController(svc);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        // The [Authorize(Policy="PatientAccess")] would normally populate User —
        // for unit tests we set the principal directly. GetPatientId() reads
        // User.FindFirst("patientId").
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("patientId", patientId.ToString())
        }, "TestAuth"));

        // Act
        var result = await controller.Logout();

        // Assert — 200 + service clears DB hash + cookie is deleted
        result.Should().BeOfType<OkObjectResult>();
        svc.Verify(s => s.LogoutAsync(patientId), Times.Once);

        var setCookie = controller.Response.Headers["Set-Cookie"].ToString();
        // ASP.NET Core's Cookies.Delete emits a Set-Cookie header with an empty
        // value + an expired Expires date (the Unix epoch) to instruct the
        // browser to discard the cookie immediately.
        setCookie.Should().Contain("portal_refresh=", "the cookie must be cleared on logout");
        setCookie.Should().Contain("expires=Thu, 01 Jan 1970 00:00:00 GMT", "the cookie must be expired immediately (epoch)");
    }

    // ─── 4. Body fallback (backward compat, one release) ────────────────────

    [Fact]
    public async Task Refresh_BodyFallback_StillWorks_BackwardCompat()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var svc = new Mock<IPatientPortalService>();
        svc.Setup(s => s.RefreshTokenAsync(patientId, "body-token"))
           .ReturnsAsync((OkResponse("rt-rotated-from-body"), (string?)null))
           .Verifiable();

        var logger = new Mock<ILogger<PatientPortalController>>();
        var controller = CreateController(svc, logger: logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Headers.Authorization = $"Bearer {MakeJwt(patientId)}";
        // NO cookie set — exercises the body-fallback path

        // Act
        var result = await controller.RefreshToken(new PatientRefreshTokenRequest { RefreshToken = "body-token" });

        // Assert — refresh still succeeds via the body fallback
        result.Should().BeOfType<OkObjectResult>();
        svc.Verify(s => s.RefreshTokenAsync(patientId, "body-token"), Times.Once);

        // A warning was logged so operators can track remaining stale frontends
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("SEC-10")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "body-fallback path must log a deprecation warning");

        // The rotated token is set as a cookie even on the body-fallback path —
        // so the next refresh will use the cookie, not the body.
        var setCookie = controller.Response.Headers["Set-Cookie"].ToString();
        setCookie.Should().Contain("portal_refresh=rt-rotated-from-body");
    }

    // ─── 5. No cookie + no body → 401 ───────────────────────────────────────

    [Fact]
    public async Task Refresh_NoCookieNoBody_Returns401()
    {
        // Arrange
        var patientId = Guid.NewGuid();
        var svc = new Mock<IPatientPortalService>();
        // Must NEVER be called
        svc.Setup(s => s.RefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string>()))
           .ReturnsAsync((OkResponse(), (string?)null));

        var controller = CreateController(svc);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Headers.Authorization = $"Bearer {MakeJwt(patientId)}";
        // No cookie, empty body

        // Act
        var result = await controller.RefreshToken(new PatientRefreshTokenRequest { RefreshToken = "" });

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        svc.Verify(s => s.RefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }
}
