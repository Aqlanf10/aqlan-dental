using System.Reflection;
using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.PatientPortal;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AqlanDentalPro.UnitTests.PortalAuth;

/// <summary>
/// MOBILE-PAT-01 — native patient authentication is explicit and cannot silently
/// fall back to staff authentication or expose browser refresh tokens.
/// </summary>
public sealed class PatientMobileAuthRouteContractTests
{
    private static MethodInfo Method(string name) =>
        typeof(PatientPortalController).GetMethod(name)
        ?? throw new InvalidOperationException($"Missing {name}");

    private static string[] Templates(MethodInfo method) =>
        method.GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template ?? string.Empty)
            .ToArray();

    [Fact]
    public void Mobile_patient_login_is_an_explicit_alias_of_portal_login()
    {
        Templates(Method(nameof(PatientPortalController.Login)))
            .Should().BeEquivalentTo(["auth/login", "mobile/auth/login"]);
    }

    [Fact]
    public void Mobile_patient_refresh_is_an_explicit_alias_of_portal_refresh()
    {
        Templates(Method(nameof(PatientPortalController.RefreshToken)))
            .Should().BeEquivalentTo(["auth/refresh-token", "mobile/auth/refresh-token"]);
    }

    [Fact]
    public void Mobile_patient_logout_keeps_the_patient_policy()
    {
        var method = Method(nameof(PatientPortalController.Logout));

        Templates(method).Should().BeEquivalentTo(["auth/logout", "mobile/auth/logout"]);
        method.GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be("PatientAccess");
    }

    [Fact]
    public void Browser_portal_refresh_token_remains_non_serializable()
    {
        typeof(PatientAuthResponse)
            .GetProperty(nameof(PatientAuthResponse.RefreshToken))!
            .GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>()
            .Should().NotBeNull();
    }

    [Fact]
    public void Native_patient_refresh_header_is_distinct_from_staff_header()
    {
        PatientPortalController.MobileRefreshTokenHeader
            .Should().Be("X-Aqlan-Portal-Refresh-Token");
        PatientPortalController.MobileRefreshTokenHeader
            .Should().NotBe("X-Aqlan-Refresh-Token");
    }
}
