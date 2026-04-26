using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace AqlanDentalPro.API.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "غير مصرح"),
            KeyNotFoundException        => (HttpStatusCode.NotFound, "العنصر غير موجود"),
            InvalidOperationException   => (HttpStatusCode.BadRequest, "عملية غير صالحة"),
            _                          => (HttpStatusCode.InternalServerError, "خطأ في الخادم")
        };

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = ex.Message,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
