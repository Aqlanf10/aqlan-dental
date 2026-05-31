using AqlanDentalPro.Application.Validators;
using FluentValidation;
using FluentValidation.AspNetCore;
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
    }
}
