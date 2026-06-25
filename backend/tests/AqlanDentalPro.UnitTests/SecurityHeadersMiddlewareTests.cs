using AqlanDentalPro.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests;

/// <summary>
/// SEC (audit §5.10): verifies the Content-Security-Policy is environment-aware —
/// production drops 'unsafe-inline'/'unsafe-eval' from script-src (the API serves only
/// JSON + uploaded files there; Swagger UI is disabled), while non-production keeps them
/// so Swagger UI still works. Also checks the hardening directives and core headers.
/// </summary>
public class SecurityHeadersMiddlewareTests
{
    private static async Task<HttpContext> RunAsync(string environment)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(environment);
        var ctx = new DefaultHttpContext();
        var mw = new SecurityHeadersMiddleware(_ => Task.CompletedTask);
        await mw.InvokeAsync(ctx, env.Object);
        return ctx;
    }

    private static string ScriptSrc(string csp) =>
        csp.Split(';').First(s => s.TrimStart().StartsWith("script-src"));

    [Fact]
    public async Task Production_ScriptSrc_HasNoUnsafeInlineOrEval()
    {
        var ctx = await RunAsync("Production");
        var csp = ctx.Response.Headers["Content-Security-Policy"].ToString();

        var scriptSrc = ScriptSrc(csp);
        scriptSrc.Should().NotContain("'unsafe-inline'", "production script-src must lock down inline scripts");
        scriptSrc.Should().NotContain("'unsafe-eval'", "production script-src must lock down eval");
        scriptSrc.Should().Contain("'self'");

        csp.Should().Contain("object-src 'none'");
        csp.Should().Contain("base-uri 'self'");
        csp.Should().Contain("frame-ancestors 'self'");
    }

    [Fact]
    public async Task Development_ScriptSrc_AllowsSwaggerInlineAndEval()
    {
        var ctx = await RunAsync("Development");
        var scriptSrc = ScriptSrc(ctx.Response.Headers["Content-Security-Policy"].ToString());

        scriptSrc.Should().Contain("'unsafe-inline'", "Swagger UI needs inline scripts in non-production");
        scriptSrc.Should().Contain("'unsafe-eval'");
    }

    [Fact]
    public async Task CoreSecurityHeaders_AreAlwaysSet()
    {
        var ctx = await RunAsync("Production");

        ctx.Response.Headers["X-Frame-Options"].ToString().Should().Be("SAMEORIGIN");
        ctx.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
        ctx.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
        ctx.Response.Headers["Strict-Transport-Security"].ToString().Should().Contain("max-age=31536000");
        ctx.Response.Headers["Permissions-Policy"].ToString().Should().Contain("camera=()");
    }
}
