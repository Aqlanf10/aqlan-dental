using System.Security.Claims;
using AqlanDentalPro.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Security;

public class QueueDisplayAuthenticationMiddlewareTests
{
    private static (DefaultHttpContext context, IAuthorizationService authorization) CreateContext(
        string path,
        ClaimsPrincipal? user = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("StaffOnly", policy =>
                policy.RequireAuthenticatedUser()
                    .RequireAssertion(authContext => !authContext.User.IsInRole("Patient")));
        });

        var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
        };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        return (context, provider.GetRequiredService<IAuthorizationService>());
    }

    private static ClaimsPrincipal AuthenticatedUser(string role) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, role),
            },
            authenticationType: "Test"));

    [Theory]
    [InlineData("/api/clinic-queue/display")]
    [InlineData("/api/public/queue")]
    public async Task ProtectedQueuePath_AnonymousRequest_Returns401(string path)
    {
        var nextCalled = false;
        var middleware = new QueueDisplayAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, authorization) = CreateContext(path);

        await middleware.InvokeAsync(context, authorization);

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        nextCalled.Should().BeFalse();
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        (await reader.ReadToEndAsync()).Should().Contain("يلزم تسجيل دخول أحد موظفي المركز");
    }

    [Fact]
    public async Task ProtectedQueuePath_PatientPortalUser_Returns403()
    {
        var nextCalled = false;
        var middleware = new QueueDisplayAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, authorization) = CreateContext(
            "/api/clinic-queue/display",
            AuthenticatedUser("Patient"));

        await middleware.InvokeAsync(context, authorization);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ProtectedQueuePath_AuthenticatedStaff_ReachesEndpoint()
    {
        var nextCalled = false;
        var middleware = new QueueDisplayAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, authorization) = CreateContext(
            "/api/clinic-queue/display",
            AuthenticatedUser("Reception"));

        await middleware.InvokeAsync(context, authorization);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task UnrelatedAnonymousPublicEndpoint_IsNotBlocked()
    {
        var nextCalled = false;
        var middleware = new QueueDisplayAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var (context, authorization) = CreateContext("/api/public/website-settings");

        await middleware.InvokeAsync(context, authorization);

        nextCalled.Should().BeTrue();
    }
}
