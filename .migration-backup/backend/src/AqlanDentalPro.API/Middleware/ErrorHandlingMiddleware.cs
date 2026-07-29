using AqlanDentalPro.Application.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            ServiceException sex          => ((HttpStatusCode)sex.StatusCode, "خطأ في الطلب"),
            UnauthorizedAccessException  => (HttpStatusCode.Forbidden, "غير مصرح"),
            KeyNotFoundException         => (HttpStatusCode.NotFound, "العنصر غير موجود"),
            ArgumentException            => (HttpStatusCode.BadRequest, "بيانات غير صالحة"),
            InvalidOperationException    => (HttpStatusCode.BadRequest, "عملية غير صالحة"),
            // ERR-02 FIX: Handle concurrency conflicts with 409 Conflict
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "تعارض في التحديث"),
            _                           => (HttpStatusCode.InternalServerError, "خطأ في الخادم")
        };

        // H3 FIX: Don't expose raw exception messages to clients in production
        var detail = ex switch
        {
            ServiceException => ex.Message, // Business-rule messages are safe to expose
            UnauthorizedAccessException => ex.Message, // Safe to expose auth errors
            KeyNotFoundException => ex.Message,         // Safe to expose not-found errors
            ArgumentException => SanitizeArgumentMessage(ex.Message),            // Validation messages are safe to expose (sanitized)
            DbUpdateConcurrencyException => "تم تعديل البيانات بواسطة مستخدم آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.",
            // CORE-PAT-018: gate on IsDevelopment, not !IsProduction — a
            // Staging deployment was leaking raw exception text to clients.
            _ => context.RequestServices?.GetService<IWebHostEnvironment>()?.IsDevelopment() == true
                ? ex.Message // Only expose details in development
                : "حدث خطأ غير متوقع. يرجى المحاولة لاحقاً."
        };

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        // CORE-PAT-018: the frontend contract reads `message` (extractErrorMessage
        // checks it FIRST); ProblemDetails alone forced every uncaught error down
        // to the generic fallback even though `detail` carried a real Arabic text.
        problem.Extensions["message"] = detail;

        // Diagnostic: Include exception type in Development environment only
        var isDevelopment = context.RequestServices?.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
        if (isDevelopment)
        {
            problem.Extensions["errorType"] = ex.GetType().Name;
            problem.Extensions["errorSource"] = ex.TargetSite?.DeclaringType?.Name ?? "unknown";
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static string SanitizeArgumentMessage(string message)
    {
        // Remove .NET parameter hints like "(Parameter 'source')" from ArgumentException messages
        var parenIdx = message.IndexOf(" (Parameter ");
        return parenIdx > 0 ? message[..parenIdx] : message;
    }
}
