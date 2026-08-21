using System.Reflection;
using AqlanDentalPro.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// MOBILE-01 — the native client reuses the existing staff-auth actions through
/// explicit route aliases. This keeps the web cookie contract unchanged while
/// making the native token transport visible in review.
/// </summary>
public sealed class MobileAuthRouteContractTests
{
    [Fact]
    public void Mobile_login_is_an_alias_of_the_existing_rate_limited_login_action()
    {
        var method = Method(nameof(AuthController.Login));

        Templates(method).Should().BeEquivalentTo(["login", "mobile/login"]);
        method.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<EnableRateLimitingAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void Mobile_refresh_is_an_alias_of_the_existing_rate_limited_refresh_action()
    {
        var method = Method(nameof(AuthController.RefreshToken));

        Templates(method).Should().BeEquivalentTo(["refresh-token", "mobile/refresh-token"]);
        method.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        method.GetCustomAttribute<EnableRateLimitingAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void Mobile_logout_reuses_the_authenticated_staff_logout_action()
    {
        var method = Method(nameof(AuthController.Logout));

        Templates(method).Should().BeEquivalentTo(["logout", "mobile/logout"]);
        method.GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be("StaffOnly");
        method.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull();
    }

    private static MethodInfo Method(string name) =>
        typeof(AuthController).GetMethod(name, BindingFlags.Public | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"AuthController.{name} was not found.");

    private static string[] Templates(MethodInfo method) =>
        method.GetCustomAttributes<HttpPostAttribute>()
            .Select(attribute => attribute.Template)
            .Where(template => template is not null)
            .Cast<string>()
            .OrderBy(template => template, StringComparer.Ordinal)
            .ToArray();
}
