using AqlanDentalPro.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Canonical names for policies that protect clinical patient data.
/// Keeping these names in one place prevents controller annotations and
/// authorization registration from drifting apart.
/// </summary>
public static class AuthorizationPolicyNames
{
    public const string ClinicalRead = "ClinicalRead";
    public const string ClinicalWrite = "ClinicalWrite";
    public const string SuperAdminOnly = "SuperAdminOnly";
}

/// <summary>
/// Extension method for Role-Based Authorization Policy registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class AuthorizationPolicyConfiguration
{
    /// <summary>
    /// Registers all role-based authorization policies used throughout the application.
    /// SuperAdmin is a strict superset of Admin for operational access, while owner-only
    /// security operations use the dedicated SuperAdminOnly policy.
    /// </summary>
    public static void AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(opts =>
        {
            opts.AddPolicy(AuthorizationPolicyNames.SuperAdminOnly, policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin)));

            // Admin operational policies: SuperAdmin inherits all Admin access.
            opts.AddPolicy("AdminOnly", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin)));

            opts.AddPolicy("OrthoAccess", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Orthodontist)));

            opts.AddPolicy("GeneralAccess", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.GeneralDentist)));

            opts.AddPolicy("SurgeryAccess", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.OralSurgeon)));

            opts.AddPolicy("OrthoSurgicalAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.OralSurgeon)));

            opts.AddPolicy("FinanceAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Reception),
                    nameof(UserRole.Accountant)));

            opts.AddPolicy("ReportsAccess", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("FinanceWrite", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("AdminAccess", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin)));

            opts.AddPolicy("CashierAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Reception),
                    nameof(UserRole.Accountant)));

            opts.AddPolicy("DoctorAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon)));

            opts.AddPolicy(AuthorizationPolicyNames.ClinicalRead, policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon)));

            opts.AddPolicy(AuthorizationPolicyNames.ClinicalWrite, policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon)));

            opts.AddPolicy("AppointmentAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon),
                    nameof(UserRole.Reception)));

            opts.AddPolicy("AIAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.SuperAdmin),
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon)));

            opts.AddPolicy("AdminOrReception", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Reception)));

            opts.AddPolicy("CommissionView", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("CommissionEdit", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("CommissionApprove", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("CommissionPay", policy =>
                policy.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("PatientAccess", policy =>
                policy.RequireRole(nameof(UserRole.Patient)));

            // Staff-only excludes patient portal accounts; SuperAdmin is naturally staff.
            opts.AddPolicy("StaffOnly", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => !ctx.User.IsInRole(nameof(UserRole.Patient))));
        });
    }
}
