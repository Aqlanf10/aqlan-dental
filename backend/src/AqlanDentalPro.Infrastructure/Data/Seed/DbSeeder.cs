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
        try
        {
            // Ensure PasswordSalt column exists BEFORE MigrateAsync()
            // because the EF model expects this column and queries will fail without it.
            await context.Database.ExecuteSqlRawAsync(
                @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""PasswordSalt"" text NOT NULL DEFAULT ''");

            // Deduplicate phone numbers before creating unique index
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE ""Patients"" SET ""Phone"" = '' 
                WHERE ""Id""::text NOT IN (
                    SELECT MIN(p.""Id""::text) FROM ""Patients"" p 
                    WHERE p.""Phone"" IS NOT NULL AND p.""Phone"" != ''
                    GROUP BY p.""Phone""
                ) AND ""Phone"" IS NOT NULL AND ""Phone"" != '';
            ");

            // Add unique index on Phone if not exists
            var addPhoneIndexSql = @"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Patients_Phone') THEN
                        CREATE UNIQUE INDEX ""IX_Patients_Phone"" ON ""Patients"" (""Phone"") WHERE ""Phone"" IS NOT NULL AND ""Phone"" != '';
                    END IF;
                END $$;";
            await context.Database.ExecuteSqlRawAsync(addPhoneIndexSql);

            // Add unique index on WhatsApp if not exists
            var addWhatsAppIndexSql = @"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Patients_WhatsApp') THEN
                        CREATE UNIQUE INDEX ""IX_Patients_WhatsApp"" ON ""Patients"" (""WhatsApp"") WHERE ""WhatsApp"" IS NOT NULL AND ""WhatsApp"" != '';
                    END IF;
                END $$;";
            await context.Database.ExecuteSqlRawAsync(addWhatsAppIndexSql);

            await context.Database.MigrateAsync();

            if (!await context.Branches.AnyAsync())
                await SeedBranchAsync(context);

            if (!await context.Users.AnyAsync())
                await SeedUsersAndDoctorsAsync(context);
            else
                await MigrateUserPasswordsAsync(context);

            // Always run: reset admin password if ADMIN_RESET_PASSWORD env var is set.
            await EnsureAdminPasswordResetAsync(context, logger);

            if (!await context.RolePermissions.AnyAsync())
                await SeedPermissionsAsync(context);

            if (!await context.Settings.AnyAsync())
                await SeedSettingsAsync(context);

            await context.SaveChangesAsync();

            // Test data (patients, appointments, ortho, finance)
            if (!await context.Patients.AnyAsync())
                await SeedTestDataAsync(context);

            logger.LogInformation("Database seeding completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database seeding failed, but app will continue running.");
            // Don't rethrow - let the app start even if seeding partially fails
        }
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

        var defaultPassword = "AqlanDental2024!";

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

        var defaultPassword = "AqlanDental2024!";

        foreach (var user in users)
        {
            var salt = GenerateSalt();
            var hash = HashPassword(defaultPassword, salt);
            user.PasswordSalt = salt;
            user.PasswordHash = hash;
        }

        await context.SaveChangesAsync();
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
            new() { Id = new Guid("b0000000-0000-0000-0000-000000000001"), CaseNumber = "OR-2026-001", PatientId = patients[0].Id, DoctorId = drAqlan.Id, ApplianceType = "MBT 0.022", StartDate = today.AddDays(-60), ExpectedDurationMonths = 18, TotalFee = 800000, Status = "active", BranchId = branchId },
            new() { Id = new Guid("b0000000-0000-0000-0000-000000000002"), CaseNumber = "OR-2026-002", PatientId = patients[2].Id, DoctorId = drAqlan.Id, ApplianceType = "Damon Q",  StartDate = today.AddDays(-30), ExpectedDurationMonths = 24, TotalFee = 1000000, Status = "active", BranchId = branchId },
            new() { Id = new Guid("b0000000-0000-0000-0000-000000000003"), CaseNumber = "OR-2026-003", PatientId = patients[5].Id, DoctorId = drAqlan.Id, ApplianceType = "MBT 0.018", StartDate = today.AddDays(-90), ExpectedDurationMonths = 12, TotalFee = 700000, Status = "active", BranchId = branchId },
        };
        await context.OrthoCases.AddRangeAsync(orthoCases);
        await context.SaveChangesAsync();

        // ─── Contracts + Payments ───────────────────────────────────────
        var receptionId  = new Guid("20000000-0000-0000-0000-000000000007");
        var accountantId = new Guid("20000000-0000-0000-0000-000000000008");

        var contracts = new List<Contract>
        {
            new() { Id = new Guid("c0000000-0000-0000-0000-000000000001"), PatientId = patients[0].Id, RelatedCaseId = orthoCases[0].Id, Specialty = "ortho",   TotalAmount = 800000,  DiscountAmount = 0,     InstallmentsCount = 4, InstallmentAmount = 200000, StartDate = today.AddDays(-60), Status = "active" },
            new() { Id = new Guid("c0000000-0000-0000-0000-000000000002"), PatientId = patients[2].Id, RelatedCaseId = orthoCases[1].Id, Specialty = "ortho",   TotalAmount = 1000000, DiscountAmount = 50000, InstallmentsCount = 6, InstallmentAmount = 158333, StartDate = today.AddDays(-30), Status = "active" },
            new() { Id = new Guid("c0000000-0000-0000-0000-000000000003"), PatientId = patients[1].Id, Specialty = "general", TotalAmount = 150000,  DiscountAmount = 0,     InstallmentsCount = 1, InstallmentAmount = 150000, StartDate = today.AddDays(-14), Status = "completed" },
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
