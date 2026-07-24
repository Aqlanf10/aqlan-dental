using AqlanDentalPro.Application.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for FluentValidation registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class FluentValidationConfiguration
{
    /// <summary>
    /// Registers FluentValidation auto-validation and validators from Application and API assemblies.
    /// </summary>
    public static void AddFluentValidationConfiguration(this IServiceCollection services)
    {
        // ── FluentValidation ──────────────────────────────────────────────────────────
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(); // Application validators
        services.AddValidatorsFromAssemblyContaining<Program>();               // API-level validators

        // CORE-PAT-018: validation failures used to return the stock English
        // ValidationProblemDetails ("One or more validation errors occurred.")
        // with the carefully-written Arabic validator messages buried in the
        // errors dictionary. The frontend contract reads `message` first — join
        // the Arabic messages into it so the user sees the real reason.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var messages = context.ModelState
                    .SelectMany(kvp => kvp.Value == null
                        ? Enumerable.Empty<string>()
                        : kvp.Value.Errors.Select(e => e.ErrorMessage))
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct()
                    .ToList();

                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "طلب غير صالح",
                };
                problem.Extensions["message"] = messages.Count > 0
                    ? string.Join("، ", messages)
                    : "البيانات المدخلة غير صالحة";
                return new BadRequestObjectResult(problem)
                {
                    ContentTypes = { "application/problem+json" },
                };
            };
        });
    }
}
