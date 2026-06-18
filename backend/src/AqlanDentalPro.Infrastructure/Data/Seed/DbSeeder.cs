using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace AqlanDentalPro.Infrastructure.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        // ── Critical admin operations: always run first, independently of migration status ──
        // These must succeed even if MigrateAsync fails, otherwise the admin can be locked out
        // with no email and no way to recover.
        try { await EnsureAdminPasswordResetAsync(context, logger); }
        catch (Exception ex) { logger.LogWarning(ex, "EnsureAdminPasswordResetAsync failed, continuing."); }

        try { await EnsureAdminEmailAsync(context, logger); }
        catch (Exception ex) { logger.LogWarning(ex, "EnsureAdminEmailAsync failed, continuing."); }

        try { await ForceAdminPasswordResetOnceAsync(context, logger); }
        catch (Exception ex) { logger.LogWarning(ex, "ForceAdminPasswordResetOnceAsync failed, continuing."); }

        try
        {
            // TD-020 Phase B2: The following 4 pre-MigrateAsync ExecuteSqlRaw calls
            // have been replaced by EF migration 20260521000000_AddPasswordSaltAndPatientPhoneIndexes:
            //   S1: ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "PasswordSalt"
            //   S2: Deduplicate Phone values before unique index
            //   S3: CREATE UNIQUE INDEX "IX_Patients_Phone"
            //   S4: CREATE UNIQUE INDEX "IX_Patients_WhatsApp"
            // context.Database.MigrateAsync() below will apply the migration automatically.

            await context.Database.MigrateAsync();

            if (!await context.Branches.AnyAsync())
                await SeedBranchAsync(context);

            if (!await context.Users.AnyAsync())
                await SeedUsersAndDoctorsAsync(context);
            else
                await MigrateUserPasswordsAsync(context);

            // Additive seeding: only add missing (Role, Resource) combinations
            await SeedPermissionsAsync(context);

            if (!await context.Settings.AnyAsync())
                await SeedSettingsAsync(context);

            // Always run ClinicServices upsert — syncs commission defaults for existing records,
            // inserts new services if table is empty. Safe to run every startup.
            await SeedClinicServicesAsync(context);

            if (!await context.ClinicRooms.AnyAsync())
                await SeedClinicRoomsAsync(context);

            // Sprint 2: Always upsert payment methods — safe to run every startup
            await SeedPaymentMethodSettingsAsync(context);

            // Lab Sprint 2: Always upsert lab work types — safe to run every startup
            await SeedLabWorkTypesAsync(context);

            await context.SaveChangesAsync();

            // Test data (patients, appointments, ortho, finance)
            if (!await context.Patients.AnyAsync())
                await SeedTestDataAsync(context);

            // ── Post-seeding admin rescue pass ──
            // On a fresh database the admin user does not exist before MigrateAsync,
            // so the pre-migration pass of EnsureAdminEmailAsync / EnsureAdminPasswordResetAsync
            // is a no-op. Run them again now that seeding has created the admin user.
            // Both methods are idempotent: they return early when the value is already applied.
            try { await EnsureAdminPasswordResetAsync(context, logger); }
            catch (Exception ex) { logger.LogWarning(ex, "Post-seeding EnsureAdminPasswordResetAsync failed, continuing."); }

            try { await EnsureAdminEmailAsync(context, logger); }
            catch (Exception ex) { logger.LogWarning(ex, "Post-seeding EnsureAdminEmailAsync failed, continuing."); }

            logger.LogInformation("Database seeding completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed, but app will continue running.");
            // Don't rethrow - let the app start even if seeding partially fails
        }
    }

    /// <summary>
    /// HOTFIX: One-time admin password force-reset to recover from lockout.
    /// Uses a Setting row as a sentinel to ensure this only runs once.
    /// Resets admin password to the default seed value and activates the account.
    /// </summary>
    private static async Task ForceAdminPasswordResetOnceAsync(AppDbContext context, ILogger logger)
    {
        const string sentinelKey = "system.admin_force_reset_v1";

        try
        {
            var alreadyDone = await context.Settings.AnyAsync(s => s.Key == sentinelKey);
            if (alreadyDone) return;

            var admin = await context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username == "admin");

            if (admin is null)
            {
                logger.LogWarning("HOTFIX: Admin user not found, skipping force reset.");
                return;
            }

            // SEC-01 FIX: Use ADMIN_RESET_PASSWORD / ADMIN_DEFAULT_PASSWORD env var. In Production,
            // refuse to reset to a known default. Dev fallback keeps the legacy default for convenience.
            var rescuePassword = Environment.GetEnvironmentVariable("ADMIN_RESET_PASSWORD")
                              ?? Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");
            if (string.IsNullOrWhiteSpace(rescuePassword))
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
                if (env == "Production")
                {
                    logger.LogError(
                        "HOTFIX: ADMIN_RESET_PASSWORD / ADMIN_DEFAULT_PASSWORD not set in Production. " +
                        "Refusing to reset admin password to a known default. Set one of these env vars to perform the rescue reset.");
                    return;
                }
                rescuePassword = "AqlanDental2024!"; // Dev fallback only
            }
            var salt = GenerateSalt();
            var hash = HashPassword(rescuePassword, salt);

            admin.PasswordHash = hash;
            admin.PasswordSalt = salt;
            admin.IsActive = true;
            // SEC-01 FIX: Force password change on next login after rescue reset.
            admin.MustChangePassword = true;
            admin.UpdatedAt = DateTime.UtcNow;

            // Write sentinel so this never runs again
            context.Settings.Add(new Setting
            {
                Key = sentinelKey,
                Value = DateTime.UtcNow.ToString("O"),
                Category = "system"
            });

            await context.SaveChangesAsync();

            logger.LogWarning(
                "HOTFIX: Admin password was force-reset to default value. " +
                "Account activated. Sentinel key={Key} written to prevent re-run.",
                sentinelKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "HOTFIX: Failed to force-reset admin password. Will retry on next startup.");
            // Don't write sentinel on failure — this allows retry on next startup
        }
    }

    /// <summary>
    /// If the ADMIN_EMAIL environment variable is set and non-empty, updates the
    /// admin user's email address. This is essential for the forgot-password flow
    /// to work — without an email, the system cannot send a reset link and instead
    /// creates a PasswordResetRequest that requires another admin to approve (which
    /// is impossible if the only admin is locked out).
    /// The email is only updated if it differs from the current value.
    /// </summary>
    private static async Task EnsureAdminEmailAsync(AppDbContext context, ILogger logger)
    {
        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
        if (string.IsNullOrWhiteSpace(adminEmail))
            return;

        var admin = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == "admin");

        if (admin is null)
        {
            logger.LogWarning("ADMIN_EMAIL is set but no user with username 'admin' was found.");
            return;
        }

        // Only update if the email is actually different (avoid unnecessary DB writes)
        if (string.Equals(admin.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
            return;

        // Validate basic email format
        if (!adminEmail.Contains('@') || !adminEmail.Contains('.'))
        {
            logger.LogWarning("ADMIN_EMAIL value does not appear to be a valid email address. Skipping.");
            return;
        }

        admin.Email = adminEmail.Trim();
        admin.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogWarning(
            "Admin account '{Username}' (Id: {Id}) email was updated via ADMIN_EMAIL env var. " +
            "This enables the forgot-password email flow for the admin account.",
            admin.Username, admin.Id);
    }

    /// <summary>
    /// If the ADMIN_RESET_PASSWORD environment variable is set and non-empty,
    /// resets the admin user's password to that value using a fresh Argon2id hash
    /// and ensures the account is active. The password value is never logged.
    /// Remove or clear the variable after confirming admin login works.
    /// </summary>
    private static async Task EnsureAdminPasswordResetAsync(AppDbContext context, ILogger logger)
    {
        var resetPassword = Environment.GetEnvironmentVariable("ADMIN_RESET_PASSWORD");
        if (string.IsNullOrWhiteSpace(resetPassword))
            return;

        var admin = await context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Username == "admin");

        if (admin is null)
        {
            logger.LogWarning("ADMIN_RESET_PASSWORD is set but no user with username 'admin' was found.");
            return;
        }

        var salt = GenerateSalt();
        var hash = HashPassword(resetPassword, salt);

        admin.PasswordHash = hash;
        admin.PasswordSalt = salt;
        admin.IsActive = true;
        admin.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Log action without exposing the password value
        logger.LogWarning(
            "Admin account '{Username}' (Id: {Id}) password was reset via ADMIN_RESET_PASSWORD. " +
            "Remove this environment variable after verifying login succeeds.",
            admin.Username, admin.Id);
    }

    private static async Task SeedBranchAsync(AppDbContext context)
    {
        var branch = new Branch
        {
            Id = new Guid("10000000-0000-0000-0000-000000000001"),
            Name = "مركز د. عقلان الكامل لطب وتقويم الأسنان",
            Address = "تعز، اليمن — شارع التحرير الأعلى",
            Phone = "04-253028",
            IsMain = true
        };
        await context.Branches.AddAsync(branch);
    }

    private static async Task SeedUsersAndDoctorsAsync(AppDbContext context)
    {
        var branchId = new Guid("10000000-0000-0000-0000-000000000001");

        var usersData = new[]
        {
            (id: new Guid("20000000-0000-0000-0000-000000000001"), username: "admin",       role: UserRole.Admin,          name: "المدير العام",         specialty: (string?)null,             color: "#374151", initials: "مع"),
            (id: new Guid("20000000-0000-0000-0000-000000000002"), username: "dr_aqlan",    role: UserRole.Orthodontist,   name: "د. عقلان الكامل",      specialty: "أخصائي تقويم الأسنان",   color: "#0E7490", initials: "عق"),
            (id: new Guid("20000000-0000-0000-0000-000000000003"), username: "dr_aisha",    role: UserRole.GeneralDentist, name: "د. عائشة غازي",        specialty: "طب أسنان عام",           color: "#7C3AED", initials: "عغ"),
            (id: new Guid("20000000-0000-0000-0000-000000000004"), username: "dr_iman",     role: UserRole.GeneralDentist, name: "د. إيمان الكامل",      specialty: "طب أسنان عام",           color: "#059669", initials: "إك"),
            (id: new Guid("20000000-0000-0000-0000-000000000005"), username: "dr_hisham",   role: UserRole.GeneralDentist, name: "د. هشام القدسي",       specialty: "طب أسنان عام",           color: "#D97706", initials: "هق"),
            (id: new Guid("20000000-0000-0000-0000-000000000006"), username: "dr_khaldoon", role: UserRole.OralSurgeon,    name: "د. خلدون البريهي",     specialty: "أخصائي جراحة وجه وفكين", color: "#DC2626", initials: "خب"),
            (id: new Guid("20000000-0000-0000-0000-000000000007"), username: "reception1",  role: UserRole.Reception,      name: "موظف الاستقبال",       specialty: (string?)null,             color: "#6B7280", initials: "مس"),
            (id: new Guid("20000000-0000-0000-0000-000000000008"), username: "accountant1", role: UserRole.Accountant,     name: "المحاسب",              specialty: (string?)null,             color: "#6B7280", initials: "مح"),
        };

        // SEC-01 FIX: Seed password comes from ADMIN_DEFAULT_PASSWORD env var. In Production,
        // throw if unset so fresh deployments don't boot with a publicly-known default. In Development,
        // fall back to the legacy default for convenience.
        var seedPassword = Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");
        if (string.IsNullOrWhiteSpace(seedPassword))
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env == "Production")
            {
                throw new InvalidOperationException(
                    "SEC: ADMIN_DEFAULT_PASSWORD not set in Production. Refusing to seed users with a publicly-known default password. Set ADMIN_DEFAULT_PASSWORD env var before first deployment.");
            }
            seedPassword = "AqlanDental2024!"; // Dev fallback only
        }
        var defaultPassword = seedPassword;

        foreach (var (id, username, role, name, specialty, color, initials) in usersData)
        {
            // Generate a unique salt for each user
            var salt = GenerateSalt();
            var hash = HashPassword(defaultPassword, salt);

            var user = new User
            {
                Id = id,
                Username = username,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = role,
                BranchId = branchId,
                // SEC-01 FIX: Force password change on first login for ALL seeded users.
                MustChangePassword = true
            };
            await context.Users.AddAsync(user);

            if (specialty != null)
            {
                var doctor = new Doctor
                {
                    UserId = id,
                    Name = name,
                    Specialty = specialty,
                    BranchId = branchId,
                    Color = color,
                    AvatarInitials = initials
                };
                await context.Doctors.AddAsync(doctor);
            }
        }
    }

    /// <summary>
    /// Migrates existing users from unsalted password hashes to per-user salted hashes.
    /// Users with empty PasswordSalt get re-hashed with the default password.
    /// </summary>
    private static async Task MigrateUserPasswordsAsync(AppDbContext context)
    {
        var users = await context.Users
            .Where(u => u.PasswordSalt == null || u.PasswordSalt == "")
            .ToListAsync();

        if (users.Count == 0) return;

        // SEC-01 FIX: Use ADMIN_DEFAULT_PASSWORD env var (or dev fallback). These users will be
        // forced to change password on next login via MustChangePassword=true below.
        var migratePassword = Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");
        if (string.IsNullOrWhiteSpace(migratePassword))
        {
            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (env == "Production")
            {
                logger.LogWarning(
                    "MigrateUserPasswordsAsync: ADMIN_DEFAULT_PASSWORD not set in Production. " +
                    "Skipping re-hash of unsalted users — they must use the forgot-password flow.");
                return;
            }
            migratePassword = "AqlanDental2024!"; // Dev fallback only
        }
        var defaultPassword = migratePassword;

        foreach (var user in users)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(defaultPassword, salt);
            user.PasswordSalt = salt;
            user.PasswordHash = hash;
            // SEC-01 FIX: Force password change on next login for migrated users.
            user.MustChangePassword = true;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedPermissionsAsync(AppDbContext context)
    {
        // Role keys MUST match the UserRole enum names exactly (PascalCase)
        // so that Enum.TryParse<UserRole>(role) succeeds in API endpoints.
        var matrix = new Dictionary<string, Dictionary<string, (bool view, bool create, bool edit, bool delete, bool export, bool approve)>>
        {
            ["patients"] = new()
            {
                ["Admin"]          = (true,  true,  true,  true,  true,  false),
                ["Orthodontist"]   = (true,  true,  true,  false, false, false),
                ["GeneralDentist"] = (true,  true,  true,  false, false, false),
                ["OralSurgeon"]    = (true,  true,  true,  false, false, false),
                ["Reception"]      = (true,  true,  false, false, false, false),
                ["Accountant"]     = (true,  false, false, false, true,  false),
                ["Assistant"]      = (true,  false, false, false, false, false),
                ["BranchManager"]  = (true,  false, false, false, false, false),
            },
            ["ortho"] = new()
            {
                ["Admin"]        = (true, true, true, true, true, true),
                ["Orthodontist"] = (true, true, true, false, false, true),
            },
            ["general_dentistry"] = new()
            {
                ["Admin"]          = (true, true, true, true, true, false),
                ["GeneralDentist"] = (true, true, true, false, false, false),
            },
            ["surgery"] = new()
            {
                ["Admin"]       = (true, true, true, true, true, true),
                ["OralSurgeon"] = (true, true, true, false, false, true),
            },
            ["appointments"] = new()
            {
                ["Admin"]          = (true, true, true, true, false, false),
                ["Orthodontist"]   = (true, true, true, false, false, false),
                ["GeneralDentist"] = (true, true, true, false, false, false),
                ["OralSurgeon"]    = (true, true, true, false, false, false),
                ["Reception"]      = (true, true, true, false, false, false),
                ["Assistant"]      = (true, false, false, false, false, false),
                ["Accountant"]     = (true, false, false, false, false, false),
            },
            ["finance"] = new()
            {
                ["Admin"]     = (true, true, true, true, true, true),
                ["Reception"] = (true, true, false, false, false, false),
                ["Accountant"]= (true, true, true, false, true,  false),
            },
            ["reports"] = new()
            {
                ["Admin"]     = (true, false, false, false, true, false),
                ["Accountant"]= (true, false, false, false, true, false),
            },
            ["users"] = new()
            {
                ["Admin"] = (true, true, true, true, false, false),
            },
            ["settings"] = new()
            {
                ["Admin"]         = (true, true, true, true, true, true),
                ["BranchManager"] = (true, false, false, false, false, true),
            },
            ["ai"] = new()
            {
                ["Admin"]          = (true, true, false, false, false, true),
                ["Orthodontist"]   = (true, true, false, false, false, true),
                ["GeneralDentist"] = (true, true, false, false, false, false),
                ["OralSurgeon"]    = (true, true, false, false, false, false),
            },
            ["user_management"] = new()
            {
                ["Admin"] = (true, true, true, true, false, true),
            },
            ["password_reset_requests"] = new()
            {
                ["Admin"] = (true, false, true, false, false, true),
            },
            ["impersonation"] = new()
            {
                ["Admin"] = (true, true, false, false, false, false),
            },
            ["daily_operations"] = new()
            {
                ["Admin"]          = (true, true, true, true, true, true),
                ["Reception"]      = (true, true, true, false, false, false),
                ["Orthodontist"]   = (true, false, false, false, false, false),
                ["GeneralDentist"] = (true, false, false, false, false, false),
                ["OralSurgeon"]    = (true, false, false, false, false, false),
                ["Accountant"]     = (true, false, false, false, false, false),
                ["Assistant"]      = (true, false, false, false, false, false),
                ["BranchManager"]  = (true, false, false, false, false, true),
            },
            ["booking_requests"] = new()
            {
                ["Admin"]     = (true, true, true, false, false, false),
                ["Reception"] = (true, true, true, false, false, false),
            },
            ["clinic_queue"] = new()
            {
                ["Admin"]          = (true, true, true, true, false, true),
                ["Reception"]      = (true, true, true, true, false, true),
                ["Orthodontist"]   = (true, false, false, false, false, false),
                ["GeneralDentist"] = (true, false, false, false, false, false),
                ["OralSurgeon"]    = (true, false, false, false, false, false),
                ["Assistant"]      = (true, false, false, false, false, false),
            },
            ["clinic_display"] = new()
            {
                ["Admin"]          = (true, false, false, false, false, false),
                ["Reception"]      = (true, false, false, false, false, false),
                ["Orthodontist"]   = (true, false, false, false, false, false),
                ["GeneralDentist"] = (true, false, false, false, false, false),
                ["OralSurgeon"]    = (true, false, false, false, false, false),
                ["Assistant"]      = (true, false, false, false, false, false),
            },
            ["patient_journey"] = new()
            {
                ["Admin"]          = (true, true, true, false, false, false),
                ["Reception"]      = (true, true, true, false, false, false),
                ["Orthodontist"]   = (true, true, false, false, false, false),
                ["GeneralDentist"] = (true, true, false, false, false, false),
                ["OralSurgeon"]    = (true, true, false, false, false, false),
            },
            ["visits"] = new()
            {
                ["Admin"]          = (true, true, true, false, false, false),
                ["Reception"]      = (true, true, true, false, false, false),
                ["Orthodontist"]   = (true, true, false, false, false, false),
                ["GeneralDentist"] = (true, true, false, false, false, false),
                ["OralSurgeon"]    = (true, true, false, false, false, false),
            },
            ["checkout"] = new()
            {
                ["Admin"]     = (true, false, false, false, false, false),
                ["Reception"] = (true, false, false, false, false, false),
                ["Accountant"]= (true, false, false, false, false, false),
            },
            ["invoices"] = new()
            {
                ["Admin"]     = (true, true, true, false, true, false),
                ["Reception"] = (true, true, false, false, false, false),
                ["Accountant"]= (true, true, true, false, true, false),
            },
            ["rooms"] = new()
            {
                ["Admin"]          = (true, true, true, false, false, false),
                ["Reception"]      = (true, true, true, false, false, false),
                ["Orthodontist"]   = (true, true, true, false, false, false),
                ["GeneralDentist"] = (true, true, true, false, false, false),
                ["OralSurgeon"]    = (true, true, true, false, false, false),
            },
            ["lab_orders"] = new()
            {
                ["Admin"]          = (true, true, true, true, true, false),
                ["Reception"]      = (true, false, false, false, false, false),
                ["Orthodontist"]   = (true, true, true, false, false, false),
                ["GeneralDentist"] = (true, true, false, false, false, false),
                ["OralSurgeon"]    = (true, true, false, false, false, false),
                ["Assistant"]      = (true, false, false, false, false, false),
                ["BranchManager"]  = (true, true, true, false, false, false),
            },
            ["labs"] = new()
            {
                ["Admin"]         = (true, true, true, true, false, false),
                ["BranchManager"] = (true, true, true, false, false, false),
                ["Orthodontist"]  = (true, false, false, false, false, false),
                ["Reception"]     = (true, false, false, false, false, false),
            },
            ["lab_work_types"] = new()
            {
                ["Admin"]         = (true, true, true, true, false, false),
                ["BranchManager"] = (true, true, true, false, false, false),
                ["Orthodontist"]  = (true, false, false, false, false, false),
            },
            ["lab_work_prices"] = new()
            {
                ["Admin"]         = (true, true, true, true, false, true),
                ["BranchManager"] = (true, true, true, false, false, false),
                ["Orthodontist"]  = (true, false, false, false, false, false),
            },
            ["lab_payables"] = new()
            {
                ["Admin"]         = (true, false, true, false, true, false),
                ["Accountant"]    = (true, false, true, false, true, false),
                ["BranchManager"] = (true, false, true, false, false, false),
            },
            ["lab_reports"] = new()
            {
                ["Admin"]         = (true, false, false, false, true, false),
                ["Accountant"]    = (true, false, false, false, true, false),
                ["BranchManager"] = (true, false, false, false, true, false),
            },
        };

        var existingPermissions = await context.RolePermissions
            .ToDictionaryAsync(rp => (rp.Role, rp.Resource));

        foreach (var (resource, roles) in matrix)
        {
            foreach (var (role, (view, create, edit, delete, export, approve)) in roles)
            {
                if (existingPermissions.TryGetValue((role, resource), out var existing))
                {
                    existing.CanView = view;
                    existing.CanCreate = create;
                    existing.CanEdit = edit;
                    existing.CanDelete = delete;
                    existing.CanExport = export;
                    existing.CanApprove = approve;
                }
                else
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        Role = role,
                        Resource = resource,
                        CanView = view,
                        CanCreate = create,
                        CanEdit = edit,
                        CanDelete = delete,
                        CanExport = export,
                        CanApprove = approve
                    });
                }
            }
        }
    }

    private static async Task SeedSettingsAsync(AppDbContext context)
    {
        var settings = new[]
        {
            // Official clinic name per the owner — appears on all reports/PDFs.
            new Setting { Key = "clinic.name",     Value = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان", Category = "clinic" },
            new Setting { Key = "clinic.location",  Value = "تعز، اليمن — شارع التحرير الأعلى",         Category = "clinic" },
            new Setting { Key = "clinic.phones",    Value = "04-253028 · 770-245745 · 711-752823",       Category = "clinic" },
            new Setting { Key = "clinic.currency",  Value = "YER",                                       Category = "clinic" },
            // Lead doctor identity for report headers/signatures (owner decision).
            new Setting { Key = "clinic.lead_doctor",             Value = "د. عقلان الكامل",                          Category = "clinic" },
            new Setting { Key = "clinic.lead_doctor_title",       Value = "أخصائي تقويم الأسنان",                     Category = "clinic" },
            new Setting { Key = "clinic.lead_doctor_credentials", Value = "جامعة مانيلا المركزية — الفلبين",          Category = "clinic" },
            new Setting { Key = "patient.number_prefix",          Value = "GM",  Category = "patients" },
            new Setting { Key = "appointment.default_duration",   Value = "30",  Category = "appointments" },
            new Setting { Key = "appointment.reminder_hours",     Value = "24,2", Category = "appointments" },
            // Website / Homepage settings
            new Setting { Key = "website.clinicName",           Value = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان", Category = "website" },
            new Setting { Key = "website.heroTitle",            Value = "ابتسامة تجمع بين دقة العلم ولمسة الفن", Category = "website" },
            new Setting { Key = "website.heroSubtitle",         Value = "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.", Category = "website" },
            new Setting { Key = "website.marketingSlogan",      Value = "قيادة طبية… وابتسامة بثقة", Category = "website" },
            new Setting { Key = "website.aboutText",            Value = "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.", Category = "website" },
            new Setting { Key = "website.phone",                Value = "04-253028", Category = "website" },
            new Setting { Key = "website.whatsapp",             Value = "967770245745", Category = "website" },
            new Setting { Key = "website.address",              Value = "تعز، اليمن — شارع التحرير الأعلى", Category = "website" },
            new Setting { Key = "website.workingHours",         Value = "السبت – الخميس: 8 ص – 8 م", Category = "website" },
            new Setting { Key = "website.facebook",             Value = "", Category = "website" },
            new Setting { Key = "website.instagram",            Value = "", Category = "website" },
            new Setting { Key = "website.logoUrl",              Value = "", Category = "website" },
            new Setting { Key = "website.heroImageUrl",         Value = "", Category = "website" },
            new Setting { Key = "website.servicesSectionTitle", Value = "حلول طبية متكاملة لابتسامة صحية وواثقة", Category = "website" },
            new Setting { Key = "website.bookingButtonText",    Value = "احجز موعدك الآن", Category = "website" },
            new Setting { Key = "website.whatsappButtonText",   Value = "تواصل عبر الواتساب", Category = "website" },
        };
        await context.Settings.AddRangeAsync(settings);
    }

    private static async Task SeedClinicServicesAsync(AppDbContext context)
    {
        // Canonical service definitions — used for both insert (new DB) and upsert (existing DB).
        // Commission fields are always synced; DefaultPrice is synced only for new records.
        var canonical = new[]
        {
            new ClinicService { ArabicName = "معاينة", EnglishName = "Consultation", Code = "CONS", Category = ServiceCategory.Consultation, DefaultDurationMinutes = 30, DefaultPrice = 5000, RequiresDoctor = true, RequiresConsultationFee = true, DefaultDoctorCommissionPercentage = 50, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 1 },
            new ClinicService { ArabicName = "متابعة", EnglishName = "Follow-up", Code = "FOLL", Category = ServiceCategory.Consultation, DefaultDurationMinutes = 15, DefaultPrice = 3000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 50, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 2 },
            new ClinicService { ArabicName = "حالة إسعافية", EnglishName = "Emergency", Code = "EMER", Category = ServiceCategory.Consultation, DefaultDurationMinutes = 30, DefaultPrice = 7000, RequiresDoctor = true, RequiresConsultationFee = true, DefaultDoctorCommissionPercentage = 50, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 3 },
            new ClinicService { ArabicName = "حشوة", EnglishName = "Filling", Code = "FILL", Category = ServiceCategory.Restorative, DefaultDurationMinutes = 45, DefaultPrice = 15000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 40, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 4 },
            new ClinicService { ArabicName = "علاج عصب", EnglishName = "Root Canal", Code = "RC", Category = ServiceCategory.Endodontics, DefaultDurationMinutes = 60, DefaultPrice = 40000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 35, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 5 },
            new ClinicService { ArabicName = "تنظيف جير", EnglishName = "Scaling", Code = "SCAL", Category = ServiceCategory.Preventive, DefaultDurationMinutes = 30, DefaultPrice = 10000, RequiresDoctor = false, DefaultDoctorCommissionPercentage = 30, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 6 },
            new ClinicService { ArabicName = "خلع بسيط", EnglishName = "Simple Extraction", Code = "EXT-S", Category = ServiceCategory.Surgery, DefaultDurationMinutes = 30, DefaultPrice = 10000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 40, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 7 },
            new ClinicService { ArabicName = "خلع جراحي", EnglishName = "Surgical Extraction", Code = "EXT-G", Category = ServiceCategory.Surgery, DefaultDurationMinutes = 60, DefaultPrice = 30000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 35, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 8 },
            new ClinicService { ArabicName = "كشف تقويم", EnglishName = "Ortho Consultation", Code = "ORTH-C", Category = ServiceCategory.Orthodontics, DefaultDurationMinutes = 30, DefaultPrice = 5000, RequiresDoctor = true, RequiresConsultationFee = true, DefaultDoctorCommissionPercentage = 50, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 9 },
            new ClinicService { ArabicName = "شد تقويم", EnglishName = "Ortho Adjustment", Code = "ORTH-A", Category = ServiceCategory.Orthodontics, DefaultDurationMinutes = 20, DefaultPrice = 5000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 50, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 10 },
            new ClinicService { ArabicName = "زراعة", EnglishName = "Implant", Code = "IMPL", Category = ServiceCategory.Surgery, DefaultDurationMinutes = 90, DefaultPrice = 200000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 25, DefaultLabCost = 40000, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 11 },
            new ClinicService { ArabicName = "تركيبات زيركون", EnglishName = "Zirconia Crown", Code = "CROWN-Z", Category = ServiceCategory.Prosthodontics, DefaultDurationMinutes = 45, DefaultPrice = 60000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 30, DefaultLabCost = 15000, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 12 },
            new ClinicService { ArabicName = "تركيبات إيماكس", EnglishName = "E-Max Veneer/Crown", Code = "EMAX", Category = ServiceCategory.Prosthodontics, DefaultDurationMinutes = 45, DefaultPrice = 50000, RequiresDoctor = true, DefaultDoctorCommissionPercentage = 30, DefaultLabCost = 12000, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 13 },
            new ClinicService { ArabicName = "فينير", EnglishName = "Veneer", Code = "VENEER", Category = ServiceCategory.Cosmetic, DefaultDurationMinutes = 45, DefaultPrice = 50000, RequiresDoctor = true, ShowInBooking = false, DefaultDoctorCommissionPercentage = 30, DefaultLabCost = 12000, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 14 },
            new ClinicService { ArabicName = "تبييض", EnglishName = "Whitening", Code = "WHITE", Category = ServiceCategory.Cosmetic, DefaultDurationMinutes = 60, DefaultPrice = 30000, RequiresDoctor = false, DefaultDoctorCommissionPercentage = 25, DefaultMaterialCost = 5000, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 15 },
            new ClinicService { ArabicName = "أشعة", EnglishName = "X-Ray", Code = "XRAY", Category = ServiceCategory.Radiology, DefaultDurationMinutes = 15, DefaultPrice = 5000, RequiresDoctor = false, ShowInBooking = false, ShowInTreatmentPlan = false, DefaultDoctorCommissionPercentage = 0, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 16 },
            new ClinicService { ArabicName = "أخرى", EnglishName = "Other", Code = "OTHER", Category = ServiceCategory.Other, DefaultDurationMinutes = 30, DefaultPrice = 0, RequiresDoctor = true, ShowInBooking = false, DefaultDoctorCommissionPercentage = 40, CommissionBaseRule = CommissionBaseRule.AfterDiscountAndCosts, CommissionRecognitionMode = CommissionRecognitionMode.OnPaymentCollection, SortOrder = 99 },
        };

        // Upsert-by-Code: update existing services with canonical commission values,
        // insert new services if they don't exist yet.
        var existingServices = await context.ClinicServices
            .IgnoreQueryFilters()
            .ToDictionaryAsync(s => s.Code);

        foreach (var c in canonical)
        {
            if (existingServices.TryGetValue(c.Code, out var existing))
            {
                // Sync commission defaults — these are system-managed, not user-customized
                existing.DefaultDoctorCommissionPercentage = c.DefaultDoctorCommissionPercentage;
                existing.DefaultMaterialCost             = c.DefaultMaterialCost;
                existing.DefaultMaterialCostType          = c.DefaultMaterialCostType;
                existing.DefaultLabCost                   = c.DefaultLabCost;
                existing.CommissionBaseRule               = c.CommissionBaseRule;
                existing.CommissionRecognitionMode         = c.CommissionRecognitionMode;
                // Sync descriptive fields
                existing.ArabicName            = c.ArabicName;
                existing.EnglishName           = c.EnglishName;
                existing.Category              = c.Category;
                existing.DefaultDurationMinutes = c.DefaultDurationMinutes;
                existing.RequiresDoctor        = c.RequiresDoctor;
                existing.RequiresConsultationFee = c.RequiresConsultationFee;
                existing.ShowInBooking         = c.ShowInBooking;
                existing.ShowInReception       = c.ShowInReception;
                existing.ShowInTreatmentPlan   = c.ShowInTreatmentPlan;
                existing.SortOrder             = c.SortOrder;
                // Note: DefaultPrice is NOT overwritten for existing services
                // — admin may have customized pricing through the UI.
            }
            else
            {
                // New service — insert with full data including price
                var newService = new ClinicService
                {
                    ArabicName                    = c.ArabicName,
                    EnglishName                   = c.EnglishName,
                    Code                          = c.Code,
                    Category                      = c.Category,
                    DefaultDurationMinutes        = c.DefaultDurationMinutes,
                    DefaultPrice                  = c.DefaultPrice,
                    RequiresDoctor                = c.RequiresDoctor,
                    RequiresConsultationFee       = c.RequiresConsultationFee,
                    ShowInBooking                 = c.ShowInBooking,
                    ShowInReception               = c.ShowInReception,
                    ShowInTreatmentPlan           = c.ShowInTreatmentPlan,
                    SortOrder                     = c.SortOrder,
                    DefaultMaterialCost           = c.DefaultMaterialCost,
                    DefaultMaterialCostType       = c.DefaultMaterialCostType,
                    DefaultLabCost                = c.DefaultLabCost,
                    DefaultDoctorCommissionPercentage = c.DefaultDoctorCommissionPercentage,
                    CommissionBaseRule            = c.CommissionBaseRule,
                    CommissionRecognitionMode     = c.CommissionRecognitionMode,
                };
                await context.ClinicServices.AddAsync(newService);
            }
        }
    }

    private static async Task SeedClinicRoomsAsync(AppDbContext context)
    {
        var rooms = new[]
        {
            new ClinicRoom { ArabicName = "غرفة 1", EnglishName = "Room 1", Code = "ROOM-1", RoomType = RoomType.Treatment, SortOrder = 1 },
            new ClinicRoom { ArabicName = "غرفة 2", EnglishName = "Room 2", Code = "ROOM-2", RoomType = RoomType.Treatment, SortOrder = 2 },
            new ClinicRoom { ArabicName = "غرفة 3", EnglishName = "Room 3", Code = "ROOM-3", RoomType = RoomType.Treatment, SortOrder = 3 },
            new ClinicRoom { ArabicName = "غرفة الجراحة", EnglishName = "Surgery Room", Code = "SURG-1", RoomType = RoomType.Surgery, SortOrder = 4 },
            new ClinicRoom { ArabicName = "غرفة الأشعة", EnglishName = "X-Ray Room", Code = "XRAY-1", RoomType = RoomType.Radiology, SortOrder = 5 },
            new ClinicRoom { ArabicName = "الاستقبال", EnglishName = "Reception", Code = "RECP-1", RoomType = RoomType.Reception, SortOrder = 6 },
        };
        await context.ClinicRooms.AddRangeAsync(rooms);
    }

    /// <summary>
    /// Sprint 2: Seed default payment method settings.
    /// Upsert-by-Code: inserts missing methods, updates existing ones.
    /// Safe to run every startup.
    /// </summary>
    private static async Task SeedPaymentMethodSettingsAsync(AppDbContext context)
    {
        var canonical = new[]
        {
            new PaymentMethodSetting { Name = "نقداً", Code = "cash", RequiresReferenceNumber = false, SortOrder = 1 },
            new PaymentMethodSetting { Name = "بطاقة/شبكة", Code = "card", RequiresReferenceNumber = true, SortOrder = 2 },
            new PaymentMethodSetting { Name = "تحويل بنكي", Code = "bank_transfer", RequiresReferenceNumber = true, SortOrder = 3 },
            new PaymentMethodSetting { Name = "كريمي", Code = "karimey", RequiresReferenceNumber = true, SortOrder = 4 },
            new PaymentMethodSetting { Name = "جوالي", Code = "jawaly", RequiresReferenceNumber = true, SortOrder = 5 },
            new PaymentMethodSetting { Name = "حوالة", Code = "transfer", RequiresReferenceNumber = false, SortOrder = 6 },
            new PaymentMethodSetting { Name = "أخرى", Code = "other", RequiresReferenceNumber = false, SortOrder = 99 },
        };

        var existing = await context.PaymentMethodSettings
            .IgnoreQueryFilters()
            .ToDictionaryAsync(p => p.Code);

        foreach (var c in canonical)
        {
            if (existing.TryGetValue(c.Code, out var existingMethod))
            {
                // Sync display name and sort order
                existingMethod.Name = c.Name;
                existingMethod.RequiresReferenceNumber = c.RequiresReferenceNumber;
                existingMethod.SortOrder = c.SortOrder;
            }
            else
            {
                var newMethod = new PaymentMethodSetting
                {
                    Name = c.Name,
                    Code = c.Code,
                    IsActive = true,
                    RequiresReferenceNumber = c.RequiresReferenceNumber,
                    SortOrder = c.SortOrder,
                };
                await context.PaymentMethodSettings.AddAsync(newMethod);
            }
        }
    }

    private static async Task SeedTestDataAsync(AppDbContext context)
    {
        var branchId = new Guid("10000000-0000-0000-0000-000000000001");
        var today = DateOnly.FromDateTime(DateTime.Today);

        // Doctors (fetch existing)
        var doctors = await context.Doctors.ToListAsync();
        var drAqlan    = doctors.First(d => d.Name.Contains("عقلان"));
        var drAisha    = doctors.FirstOrDefault(d => d.Name.Contains("عائشة")) ?? doctors[0];
        var drIman     = doctors.FirstOrDefault(d => d.Name.Contains("إيمان")) ?? doctors[0];
        var drKhaldoon = doctors.FirstOrDefault(d => d.Name.Contains("خلدون")) ?? doctors[0];

        // ─── 10 Test Patients ───────────────────────────────────────────
        var patients = new List<Patient>
        {
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000001"), PatientNumber = "GM-2026-001", FirstName = "أحمد",    LastName = "محمد",    DateOfBirth = new DateOnly(1990, 3, 15), Gender = Gender.Male,   Phone = "770111001", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000002"), PatientNumber = "GM-2026-002", FirstName = "فاطمة",   LastName = "علي",     DateOfBirth = new DateOnly(1985, 7, 22), Gender = Gender.Female, Phone = "770111002", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000003"), PatientNumber = "GM-2026-003", FirstName = "محمد",    LastName = "الحسن",   DateOfBirth = new DateOnly(2005, 1, 10), Gender = Gender.Male,   Phone = "770111003", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000004"), PatientNumber = "GM-2026-004", FirstName = "خديجة",   LastName = "سالم",    DateOfBirth = new DateOnly(1978, 11, 5), Gender = Gender.Female, Phone = "770111004", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000005"), PatientNumber = "GM-2026-005", FirstName = "عمر",     LastName = "القحطاني",DateOfBirth = new DateOnly(1995, 6, 20), Gender = Gender.Male,   Phone = "770111005", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000006"), PatientNumber = "GM-2026-006", FirstName = "مريم",    LastName = "ناصر",    DateOfBirth = new DateOnly(2008, 4, 3),  Gender = Gender.Female, Phone = "770111006", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000007"), PatientNumber = "GM-2026-007", FirstName = "سعيد",    LastName = "عبدالله", DateOfBirth = new DateOnly(1972, 9, 18), Gender = Gender.Male,   Phone = "770111007", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000008"), PatientNumber = "GM-2026-008", FirstName = "هند",     LastName = "الزهراني",DateOfBirth = new DateOnly(2000, 2, 14), Gender = Gender.Female, Phone = "770111008", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000009"), PatientNumber = "GM-2026-009", FirstName = "يوسف",    LastName = "المطري",  DateOfBirth = new DateOnly(2010, 8, 25), Gender = Gender.Male,   Phone = "770111009", BranchId = branchId },
            new() { Id = new Guid("a0000000-0000-0000-0000-000000000010"), PatientNumber = "GM-2026-010", FirstName = "نور",     LastName = "الحربي",  DateOfBirth = new DateOnly(1988, 12, 7), Gender = Gender.Female, Phone = "770111010", BranchId = branchId },
        };
        await context.Patients.AddRangeAsync(patients);
        await context.SaveChangesAsync();

        // ─── Appointments (today + next few days) ────────────────────────
        var appts = new List<Appointment>
        {
            new() { PatientId=patients[0].Id, DoctorId=drAqlan.Id,    AppointmentDate=today,             StartTime=new TimeOnly(9,0),  DurationMinutes=45, AppointmentType="كشف تقويمي",     Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[1].Id, DoctorId=drAisha.Id,    AppointmentDate=today,             StartTime=new TimeOnly(9,30), DurationMinutes=30, AppointmentType="حشو",             Status=AppointmentStatus.Completed, BranchId=branchId },
            new() { PatientId=patients[2].Id, DoctorId=drAqlan.Id,    AppointmentDate=today,             StartTime=new TimeOnly(10,0), DurationMinutes=60, AppointmentType="تركيب جهاز تقويم",Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[3].Id, DoctorId=drIman.Id,     AppointmentDate=today,             StartTime=new TimeOnly(10,30),DurationMinutes=30, AppointmentType="كشف",             Status=AppointmentStatus.InProgress, BranchId=branchId },
            new() { PatientId=patients[4].Id, DoctorId=drKhaldoon.Id, AppointmentDate=today,             StartTime=new TimeOnly(11,0), DurationMinutes=90, AppointmentType="خلع جراحي",       Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[5].Id, DoctorId=drAisha.Id,    AppointmentDate=today,             StartTime=new TimeOnly(11,30),DurationMinutes=30, AppointmentType="تنظيف وتلميع",   Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[6].Id, DoctorId=drAqlan.Id,    AppointmentDate=today.AddDays(1),  StartTime=new TimeOnly(9,0),  DurationMinutes=30, AppointmentType="متابعة تقويم",   Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[7].Id, DoctorId=drIman.Id,     AppointmentDate=today.AddDays(1),  StartTime=new TimeOnly(10,0), DurationMinutes=45, AppointmentType="تاج",             Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[8].Id, DoctorId=drAqlan.Id,    AppointmentDate=today.AddDays(2),  StartTime=new TimeOnly(9,0),  DurationMinutes=60, AppointmentType="بدء تقويم",       Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[9].Id, DoctorId=drKhaldoon.Id, AppointmentDate=today.AddDays(2),  StartTime=new TimeOnly(11,0), DurationMinutes=60, AppointmentType="استشارة جراحية", Status=AppointmentStatus.Scheduled, BranchId=branchId },
            new() { PatientId=patients[0].Id, DoctorId=drAqlan.Id,    AppointmentDate=today.AddDays(-7), StartTime=new TimeOnly(9,0),  DurationMinutes=30, AppointmentType="متابعة تقويم",   Status=AppointmentStatus.Completed, BranchId=branchId },
            new() { PatientId=patients[1].Id, DoctorId=drAisha.Id,    AppointmentDate=today.AddDays(-7), StartTime=new TimeOnly(10,0), DurationMinutes=30, AppointmentType="كشف",             Status=AppointmentStatus.Completed, BranchId=branchId },
        };
        await context.Appointments.AddRangeAsync(appts);
        await context.SaveChangesAsync();

        // ─── Ortho Cases ────────────────────────────────────────────────
        var orthoCases = new List<OrthoCase>
        {
            new() { Id = new Guid("b0000000-0000-0000-0000-000000000001"), CaseNumber = "OR-2026-001", PatientId = patients[0].Id, DoctorId = drAqlan.Id, ApplianceType = "MBT 0.022", StartDate = today.AddDays(-60), ExpectedDurationMonths = 18, TotalFee = 800000, Status = OrthoCaseStatus.Active, BranchId = branchId },
            new() { Id = new Guid("b0000000-0000-0000-0000-000000000002"), CaseNumber = "OR-2026-002", PatientId = patients[2].Id, DoctorId = drAqlan.Id, ApplianceType = "Damon Q",  StartDate = today.AddDays(-30), ExpectedDurationMonths = 24, TotalFee = 1000000, Status = OrthoCaseStatus.Active, BranchId = branchId },
            new() { Id = new Guid("b0000000-0000-0000-0000-000000000003"), CaseNumber = "OR-2026-003", PatientId = patients[5].Id, DoctorId = drAqlan.Id, ApplianceType = "MBT 0.018", StartDate = today.AddDays(-90), ExpectedDurationMonths = 12, TotalFee = 700000, Status = OrthoCaseStatus.Active, BranchId = branchId },
        };
        await context.OrthoCases.AddRangeAsync(orthoCases);
        await context.SaveChangesAsync();

        // ─── Contracts + Payments ───────────────────────────────────────
        var receptionId  = new Guid("20000000-0000-0000-0000-000000000007");
        var accountantId = new Guid("20000000-0000-0000-0000-000000000008");

        var contracts = new List<Contract>
        {
            new() { Id = new Guid("c0000000-0000-0000-0000-000000000001"), PatientId = patients[0].Id, RelatedCaseId = orthoCases[0].Id, Specialty = "ortho",   TotalAmount = 800000,  DiscountAmount = 0,     InstallmentsCount = 4, InstallmentAmount = 200000, StartDate = today.AddDays(-60), Status = ContractStatus.Active },
            new() { Id = new Guid("c0000000-0000-0000-0000-000000000002"), PatientId = patients[2].Id, RelatedCaseId = orthoCases[1].Id, Specialty = "ortho",   TotalAmount = 1000000, DiscountAmount = 50000, InstallmentsCount = 6, InstallmentAmount = 158333, StartDate = today.AddDays(-30), Status = ContractStatus.Active },
            new() { Id = new Guid("c0000000-0000-0000-0000-000000000003"), PatientId = patients[1].Id, Specialty = "general", TotalAmount = 150000,  DiscountAmount = 0,     InstallmentsCount = 1, InstallmentAmount = 150000, StartDate = today.AddDays(-14), Status = ContractStatus.Completed },
        };
        await context.Contracts.AddRangeAsync(contracts);
        await context.SaveChangesAsync();

        // Payments linked to contracts
        var payments = new List<Payment>
        {
            new() { ContractId = contracts[0].Id, PatientId = patients[0].Id, DoctorId = drAqlan.Id, BranchId = branchId, Amount = 200000, PaymentDate = today.AddDays(-60), PaymentMethod = "cash",     Specialty = "ortho",   ReceivedBy = receptionId,  Notes = "دفعة أولى" },
            new() { ContractId = contracts[0].Id, PatientId = patients[0].Id, DoctorId = drAqlan.Id, BranchId = branchId, Amount = 200000, PaymentDate = today.AddDays(-30), PaymentMethod = "transfer",  Specialty = "ortho",   ReceivedBy = receptionId,  Notes = "دفعة ثانية" },
            new() { ContractId = contracts[1].Id, PatientId = patients[2].Id, DoctorId = drAqlan.Id, BranchId = branchId, Amount = 300000, PaymentDate = today.AddDays(-30), PaymentMethod = "cash",      Specialty = "ortho",   ReceivedBy = receptionId,  Notes = "دفعة أولى" },
            new() { ContractId = contracts[2].Id, PatientId = patients[1].Id, DoctorId = drAisha.Id, BranchId = branchId, Amount = 150000, PaymentDate = today.AddDays(-14), PaymentMethod = "cash",      Specialty = "general", ReceivedBy = accountantId, Notes = "سداد كامل" },
        };
        await context.Payments.AddRangeAsync(payments);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Generates a cryptographically random 16-byte salt encoded as Base64.
    /// Each user gets a unique salt for maximum security.
    /// </summary>
    /// <summary>
    /// Lab Sprint 2: Seed default lab work types.
    /// Upsert-by-Name: inserts missing types, updates existing ones.
    /// Safe to run every startup.
    /// </summary>
    private static async Task SeedLabWorkTypesAsync(AppDbContext context)
    {
        var canonical = new[]
        {
            new { Name = "Crown",          NameAr = "تاج",          Category = "Crown",    SortOrder = 1 },
            new { Name = "Bridge",         NameAr = "جسر",          Category = "Crown",    SortOrder = 2 },
            new { Name = "Veneer",         NameAr = "قشرة",         Category = "Crown",    SortOrder = 3 },
            new { Name = "Denture",        NameAr = "طقم",          Category = "Denture",  SortOrder = 4 },
            new { Name = "Implant Crown",  NameAr = "تاج زراعة",    Category = "Crown",    SortOrder = 5 },
            new { Name = "Retainer",       NameAr = "مثبت",         Category = "Ortho",    SortOrder = 6 },
            new { Name = "Night Guard",    NameAr = "واقي ليلي",    Category = "Ortho",    SortOrder = 7 },
            new { Name = "Ortho Appliance",NameAr = "جهاز تقويم",   Category = "Ortho",    SortOrder = 8 },
            new { Name = "Surgical Guide", NameAr = "دليل جراحي",   Category = "Surgical", SortOrder = 9 },
            new { Name = "Inlay/Onlay",    NameAr = "حشوة داخلية",  Category = "Crown",    SortOrder = 10 },
        };

        var existing = await context.LabWorkTypes
            .IgnoreQueryFilters()
            .ToDictionaryAsync(w => w.Name);

        foreach (var c in canonical)
        {
            if (existing.TryGetValue(c.Name, out var existingType))
            {
                existingType.NameAr = c.NameAr;
                existingType.Category = c.Category;
                existingType.SortOrder = c.SortOrder;
            }
            else
            {
                await context.LabWorkTypes.AddAsync(new LabWorkType
                {
                    Name = c.Name,
                    NameAr = c.NameAr,
                    Category = c.Category,
                    SortOrder = c.SortOrder,
                });
            }
        }
    }

    private static string GenerateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Hashes a password with the given salt using Argon2id.
    /// </summary>
    private static string HashPassword(string password, string salt)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = Convert.FromBase64String(salt),
            DegreeOfParallelism = 2,
            MemorySize = 65536,
            Iterations = 3
        };
        return Convert.ToBase64String(argon2.GetBytes(32));
    }
}
