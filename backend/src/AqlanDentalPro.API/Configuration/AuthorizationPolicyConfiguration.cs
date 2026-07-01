using AqlanDentalPro.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Extension method for Role-Based Authorization Policy registration.
/// Extracted from Program.cs for cleaner service configuration.
/// </summary>
public static class AuthorizationPolicyConfiguration
{
    /// <summary>
    /// Registers all role-based authorization policies used throughout the application.
    /// </summary>
    public static void AddAuthorizationPolicies(this IServiceCollection services)
    {
        // ── Role-Based Authorization Policies ─────────────────────────────────────────
        services.AddAuthorization(opts =>
        {
            // Admin-only policies
            opts.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(UserRole.Admin)));

            // Orthodontist + Admin policies
            opts.AddPolicy("OrthoAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Orthodontist)));

            // General Dentist + Admin policies
            opts.AddPolicy("GeneralAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.GeneralDentist)));

            // Oral Surgeon + Admin policies
            opts.AddPolicy("SurgeryAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.OralSurgeon)));

            // Ortho-Surgical (orthognathic) shared workspace: orthodontist + oral surgeon + admin.
            // Granular actions (approve/surgeon_review) are further gated by RolePermissions.
            opts.AddPolicy("OrthoSurgicalAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.OralSurgeon)));

            // Finance access: Admin + Reception + Accountant
            opts.AddPolicy("FinanceAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Reception), nameof(UserRole.Accountant)));

            // Reports access: Admin + Accountant
            opts.AddPolicy("ReportsAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            // Finance write access: Admin + Accountant (used for POST/DELETE/PATCH in FinanceV3)
            opts.AddPolicy("FinanceWrite", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            // Admin access: Admin only (used by OperationalExpenses approve/reject, SupplierBills cancel)
            opts.AddPolicy("AdminAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin)));

            // Cashier access: Admin + Reception + Accountant (used by FinanceV3 cashier session endpoints)
            opts.AddPolicy("CashierAccess", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Reception), nameof(UserRole.Accountant)));

            // Doctors (any medical role) + Admin
            opts.AddPolicy("DoctorAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon)));

            // Appointment management: all doctors + reception + admin
            opts.AddPolicy("AppointmentAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon),
                    nameof(UserRole.Reception)));

            // AI access: all doctors + admin
            opts.AddPolicy("AIAccess", policy =>
                policy.RequireRole(
                    nameof(UserRole.Admin),
                    nameof(UserRole.Orthodontist),
                    nameof(UserRole.GeneralDentist),
                    nameof(UserRole.OralSurgeon)));

            // Patient portal credentials management: Admin + Reception only
            opts.AddPolicy("AdminOrReception", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Reception)));

            // ── Doctor Commission policies ───────────────────────────────────────────
            opts.AddPolicy("CommissionView", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("CommissionEdit", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("CommissionApprove", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            opts.AddPolicy("CommissionPay", policy =>
                policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

            // Patient portal access - for patient-facing mobile app
            opts.AddPolicy("PatientAccess", policy =>
                policy.RequireRole("Patient"));

            // Staff-only policy: excludes Patient portal users from staff endpoints.
            // Any authenticated user without the Patient role is considered staff.
            // Applied to controllers that previously used bare [Authorize] which
            // allowed Patient JWTs to access staff endpoints (TD-009 security fix).
            opts.AddPolicy("StaffOnly", policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => !ctx.User.IsInRole(nameof(UserRole.Patient))));
        });
    }
}
