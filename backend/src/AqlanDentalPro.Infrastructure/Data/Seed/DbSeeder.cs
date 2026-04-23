using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Konscious.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;

namespace AqlanDentalPro.Infrastructure.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger)
    {
        try
        {
            await context.Database.MigrateAsync();

            if (!await context.Branches.AnyAsync())
                await SeedBranchAsync(context);

            if (!await context.Users.AnyAsync())
                await SeedUsersAndDoctorsAsync(context);

            if (!await context.RolePermissions.AnyAsync())
                await SeedPermissionsAsync(context);

            if (!await context.Settings.AnyAsync())
                await SeedSettingsAsync(context);

            await context.SaveChangesAsync();
            logger.LogInformation("Database seeding completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed.");
            throw;
        }
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

        var defaultPassword = "AqlanDental2024!";

        foreach (var (id, username, role, name, specialty, color, initials) in usersData)
        {
            var user = new User
            {
                Id = id,
                Username = username,
                PasswordHash = HashPassword(defaultPassword),
                Role = role,
                BranchId = branchId
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

    private static async Task SeedPermissionsAsync(AppDbContext context)
    {
        var permissions = new List<RolePermission>();

        var matrix = new Dictionary<string, Dictionary<string, (bool view, bool create, bool edit, bool delete, bool export, bool approve)>>
        {
            ["patients"] = new()
            {
                ["admin"]          = (true,  true,  true,  true,  true,  false),
                ["orthodontist"]   = (true,  true,  true,  false, false, false),
                ["general_dentist"]= (true,  true,  true,  false, false, false),
                ["oral_surgeon"]   = (true,  true,  true,  false, false, false),
                ["reception"]      = (true,  true,  false, false, false, false),
                ["accountant"]     = (true,  false, false, false, true,  false),
            },
            ["ortho"] = new()
            {
                ["admin"]        = (true, true, true, true, true, true),
                ["orthodontist"] = (true, true, true, false, false, true),
            },
            ["general_dentistry"] = new()
            {
                ["admin"]          = (true, true, true, true, true, false),
                ["general_dentist"]= (true, true, true, false, false, false),
            },
            ["surgery"] = new()
            {
                ["admin"]       = (true, true, true, true, true, true),
                ["oral_surgeon"]= (true, true, true, false, false, true),
            },
            ["appointments"] = new()
            {
                ["admin"]          = (true, true, true, true, false, false),
                ["orthodontist"]   = (true, true, true, false, false, false),
                ["general_dentist"]= (true, true, true, false, false, false),
                ["oral_surgeon"]   = (true, true, true, false, false, false),
                ["reception"]      = (true, true, true, false, false, false),
            },
            ["finance"] = new()
            {
                ["admin"]     = (true, true, true, true, true, false),
                ["reception"] = (true, true, false, false, false, false),
                ["accountant"]= (true, true, true, false, true,  false),
            },
            ["reports"] = new()
            {
                ["admin"]     = (true, false, false, false, true, false),
                ["accountant"]= (true, false, false, false, true, false),
            },
            ["users"] = new()
            {
                ["admin"] = (true, true, true, true, false, false),
            },
            ["settings"] = new()
            {
                ["admin"] = (true, false, true, false, false, false),
            },
            ["ai"] = new()
            {
                ["admin"]          = (true, true, false, false, false, true),
                ["orthodontist"]   = (true, true, false, false, false, true),
                ["general_dentist"]= (true, true, false, false, false, false),
                ["oral_surgeon"]   = (true, true, false, false, false, false),
            },
        };

        foreach (var (resource, roles) in matrix)
        {
            foreach (var (role, (view, create, edit, delete, export, approve)) in roles)
            {
                permissions.Add(new RolePermission
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

        await context.RolePermissions.AddRangeAsync(permissions);
    }

    private static async Task SeedSettingsAsync(AppDbContext context)
    {
        var settings = new[]
        {
            new Setting { Key = "clinic.name",     Value = "مركز د. عقلان الكامل لطب وتقويم الأسنان", Category = "clinic" },
            new Setting { Key = "clinic.location",  Value = "تعز، اليمن — شارع التحرير الأعلى",         Category = "clinic" },
            new Setting { Key = "clinic.phones",    Value = "04-253028 · 770-245745 · 711-752823",       Category = "clinic" },
            new Setting { Key = "clinic.currency",  Value = "YER",                                       Category = "clinic" },
            new Setting { Key = "patient.number_prefix",          Value = "GM",  Category = "patients" },
            new Setting { Key = "appointment.default_duration",   Value = "30",  Category = "appointments" },
            new Setting { Key = "appointment.reminder_hours",     Value = "24,2", Category = "appointments" },
        };
        await context.Settings.AddRangeAsync(settings);
    }

    private static string HashPassword(string password)
    {
        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = Encoding.UTF8.GetBytes("AqlanDentalSalt!"), // In prod, use random salt stored with hash
            DegreeOfParallelism = 1,
            MemorySize = 65536,
            Iterations = 3
        };
        var hash = argon2.GetBytes(32);
        return Convert.ToBase64String(hash);
    }
}
