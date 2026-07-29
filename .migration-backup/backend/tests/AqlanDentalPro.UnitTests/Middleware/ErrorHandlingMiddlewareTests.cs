using AqlanDentalPro.API.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace AqlanDentalPro.UnitTests.Middleware;

/// <summary>
/// Tests for ErrorHandlingMiddleware — verifies that ArgumentException
/// is mapped to HTTP 400 (not 500) and that the Arabic validation
/// message is exposed in the response body.
/// </summary>
public class ErrorHandlingMiddlewareTests
{
    private static (ErrorHandlingMiddleware middleware, DefaultHttpContext context) CreateMiddleware(
        RequestDelegate next, bool isProduction = false)
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns(isProduction ? "Production" : "Development");

        var loggerMock = new Mock<ILogger<ErrorHandlingMiddleware>>();

        var services = new ServiceCollection();
        services.AddSingleton(envMock.Object);
        var sp = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = sp,
            Response = { Body = new MemoryStream() }
        };
        context.Request.Path = "/api/test";

        var middleware = new ErrorHandlingMiddleware(next, loggerMock.Object);
        return (middleware, context);
    }

    private static async Task<ProblemDetails?> ReadResponse(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    // ── ArgumentException → 400 ──────────────────────────────────────────────

    [Fact]
    public async Task ArgumentException_Returns400_WithArabicMessage()
    {
        // Arrange
        var (middleware, context) = CreateMiddleware(_ =>
            throw new ArgumentException("تكلفة المواد لا يمكن أن تكون سالبة"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadResponse(context);
        problem.Should().NotBeNull();
        problem!.Title.Should().Be("بيانات غير صالحة");
        problem.Detail.Should().Be("تكلفة المواد لا يمكن أن تكون سالبة");
    }

    [Fact]
    public async Task ArgumentException_NegativeLabCost_Returns400()
    {
        var (middleware, context) = CreateMiddleware(_ =>
            throw new ArgumentException("تكلفة المعمل لا يمكن أن تكون سالبة"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadResponse(context);
        problem!.Detail.Should().Be("تكلفة المعمل لا يمكن أن تكون سالبة");
    }

    [Fact]
    public async Task ArgumentException_PercentageOver100_Returns400()
    {
        var (middleware, context) = CreateMiddleware(_ =>
            throw new ArgumentException("نسبة العمولة يجب أن تكون بين 0 و 100"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadResponse(context);
        problem!.Detail.Should().Be("نسبة العمولة يجب أن تكون بين 0 و 100");
    }

    [Fact]
    public async Task ArgumentException_PercentageNegative_Returns400()
    {
        var (middleware, context) = CreateMiddleware(_ =>
            throw new ArgumentException("نسبة العمولة يجب أن تكون بين 0 و 100"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadResponse(context);
        problem!.Detail.Should().Contain("نسبة العمولة");
    }

    // ── Other exception types still work ─────────────────────────────────────

    [Fact]
    public async Task InvalidOperationException_Returns400()
    {
        var (middleware, context) = CreateMiddleware(_ =>
            throw new InvalidOperationException("العمولة معتمدة"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        var problem = await ReadResponse(context);
        problem!.Title.Should().Be("عملية غير صالحة");
    }

    [Fact]
    public async Task KeyNotFoundException_Returns404()
    {
        var (middleware, context) = CreateMiddleware(_ =>
            throw new KeyNotFoundException("العنصر غير موجود"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnknownException_Returns500_InProduction()
    {
        var (middleware, context) = CreateMiddleware(_ =>
            throw new Exception("some internal error"), isProduction: true);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        var problem = await ReadResponse(context);
        problem!.Detail.Should().Be("حدث خطأ غير متوقع. يرجى المحاولة لاحقاً.");
    }

    // ── No exception → pass through ──────────────────────────────────────────

    [Fact]
    public async Task NoException_PassesThrough()
    {
        var (middleware, context) = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        // No exception thrown, response not modified by middleware
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.OK);
    }
}
