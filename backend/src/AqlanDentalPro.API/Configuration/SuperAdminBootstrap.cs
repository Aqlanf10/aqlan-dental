using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// Establishes the single-owner security invariant without requiring a schema migration.
/// UserRole is persisted as a string, so promoting the configured owner from Admin to
/// SuperAdmin is backwards-compatible with the existing Users table.
/// </summary>
public static class SuperAdminBootstrap
{
    public static async Task EnsureSingleSuperAdminAsync(
        this WebApplication app,
        IConfiguration configuration)
    {
        var configuredUsername = configuration["Security:SuperAdminUsername"]
            ?? Environment.GetEnvironmentVariable("SUPER_ADMIN_USERNAME")
            ?? "admin";

        configuredUsername = configuredUsername.Trim();
        if (string.IsNullOrWhiteSpace(configuredUsername))
        {
            app.Logger.LogError("SEC: SuperAdmin username is empty. Owner bootstrap was skipped.");
            return;
        }

        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var owner = await db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == configuredUsername);

            if (owner is null)
            {
                app.Logger.LogError(
                    "SEC: configured SuperAdmin user '{Username}' does not exist. " +
                    "No account was auto-created; create/restore the owner account through the controlled deployment process.",
                    configuredUsername);
                return;
            }

            var changed = false;

            if (owner.Role != UserRole.SuperAdmin)
            {
                owner.Role = UserRole.SuperAdmin;
                changed = true;
            }

            // The owner account is intentionally non-disableable. If legacy data left it
            // soft-deleted or inactive, restore it during the controlled bootstrap pass.
            if (!owner.IsActive || owner.DeletedAt is not null)
            {
                owner.IsActive = true;
                owner.DeletedAt = null;
                owner.DeletedBy = null;
                changed = true;
            }

            // Enforce exactly one SuperAdmin. Any legacy/accidental additional owner role
            // is downgraded to Admin rather than deleted or disabled.
            var additionalSuperAdmins = await db.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id != owner.Id && u.Role == UserRole.SuperAdmin)
                .ToListAsync();

            foreach (var extra in additionalSuperAdmins)
            {
                extra.Role = UserRole.Admin;
                changed = true;
                app.Logger.LogWarning(
                    "SEC: user '{Username}' was downgraded from SuperAdmin to Admin to preserve the single-owner invariant.",
                    extra.Username);
            }

            if (changed)
            {
                owner.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            app.Logger.LogInformation(
                "SEC: SuperAdmin owner invariant verified for '{Username}' ({UserId}).",
                owner.Username,
                owner.Id);
        }
        catch (Exception ex)
        {
            // Do not make a transient database outage prevent the process from starting;
            // authorization still denies SuperAdmin-only operations until a valid token exists.
            app.Logger.LogError(ex, "SEC: failed to verify the SuperAdmin owner invariant at startup.");
        }
    }
}
