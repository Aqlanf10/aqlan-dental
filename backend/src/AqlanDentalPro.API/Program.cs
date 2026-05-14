using AqlanDentalPro.API.Middleware;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Seed;
using AqlanDentalPro.Infrastructure.Repositories;
using AqlanDentalPro.Infrastructure.Services;
using MessagingService = AqlanDentalPro.Infrastructure.Services.MessagingService;
using PatientPortalService = AqlanDentalPro.Infrastructure.Services.PatientPortalService;
using WhatsAppService = AqlanDentalPro.Infrastructure.Services.WhatsAppService;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Net;
using System.Threading.RateLimiting;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Database (PostgreSQL) ─────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("AqlanDentalPro.Infrastructure")));

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"]
        ?? "localhost:6379"));

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey is required");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// ── Role-Based Authorization Policies ─────────────────────────────────────────
builder.Services.AddAuthorization(opts =>
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

    // Finance access: Admin + Reception + Accountant
    opts.AddPolicy("FinanceAccess", policy =>
        policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Reception), nameof(UserRole.Accountant)));

    // Reports access: Admin + Accountant
    opts.AddPolicy("ReportsAccess", policy =>
        policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));

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

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
    ?? ["http://localhost:3000", "http://localhost:3001"];
// Always include Vercel deployment origins so the frontend can call the API directly
allowedOrigins = [..allowedOrigins,
    "https://aqlan-dental-pro.vercel.app",
    "https://aqlan-dental.vercel.app"];
builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", policy =>
{
    policy.SetIsOriginAllowed(origin =>
        {
            // Allow configured origins
            if (allowedOrigins.Contains(origin)) return true;
            // C-01 FIX: Removed wildcard *.vercel.app — only explicitly listed origins are allowed
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}));

// ── Rate Limiting (H1 FIX: prevent brute-force on auth endpoints) ────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AuthPolicy", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            }));

    // P4 FIX: Strict rate limiting for public booking to prevent spam
    options.AddPolicy("BookingPolicy", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 1
            }));

    // P7 FIX: Portal auth rate limiting (prevents abuse of forgot-password WhatsApp messages)
    options.AddPolicy("PortalAuthPolicy", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 1
            }));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, IPAddress>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress ?? IPAddress.Loopback,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
        }
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "طلبات كثيرة جداً. حاول مرة أخرى بعد قليل.",
            retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var r) ? r.TotalSeconds : 60
        }, cancellationToken);
    };
});

// ── DI — Repositories ────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();

// ── DI — Services ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<PatientService>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<OrthoService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<GeneralService>();
builder.Services.AddScoped<MessagingService>();
builder.Services.AddScoped<CephService>();
builder.Services.AddScoped<IPatientPortalService, PatientPortalService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<INotificationService, AqlanDentalPro.Infrastructure.Services.NotificationService>();
builder.Services.AddHostedService<AqlanDentalPro.Infrastructure.Services.OverdueNotificationJob>();
builder.Services.AddScoped<IBookingRequestService, AqlanDentalPro.Infrastructure.Services.BookingRequestService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddHttpClient("WhatsApp");

builder.Services.AddHttpContextAccessor();

// ── FluentValidation ──────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(); // Application validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();               // API-level validators

// ── Static Files (uploads) ────────────────────────────────────────────────────
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
});
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

// ── Controllers + Swagger ─────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Aqlan Dental Pro API",
        Version = "v1",
        Description = "نظام إدارة مركز د. عقلان الكامل لطب وتقويم الأسنان"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── One-time Admin Password Reset ─────────────────────────────────
// Resets the admin password to "AqlanDental2026!" if the reset flag is not yet set.
// This runs ONCE and then sets a flag so it never runs again.
try
{
    using var resetScope = app.Services.CreateScope();
    var resetDb     = resetScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var resetLogger = resetScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Check if reset has already been done
    var alreadyReset = await resetDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Settings') THEN
                CREATE TABLE "Settings" (
                    "Id" uuid NOT NULL PRIMARY KEY DEFAULT gen_random_uuid(),
                    "Key" character varying(200) NOT NULL,
                    "Value" text NULL,
                    "Category" character varying(50) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "IsActive" boolean NOT NULL DEFAULT true
                );
            END IF;
        END $$;
    """);

    // Check if the reset flag exists
    var flagExists = false;
    using (var cmd = resetDb.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = """
            SELECT COUNT(*) FROM "Settings" WHERE "Key" = 'admin.password.reset.2026'
        """;
        await resetDb.Database.OpenConnectionAsync();
        var result = await cmd.ExecuteScalarAsync();
        await resetDb.Database.CloseConnectionAsync();
        flagExists = result != null && Convert.ToInt64(result) > 0;
    }

    if (!flagExists)
    {
        // C-02 FIX: Use environment variable instead of hardcoded password
        var newPassword = Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD") ?? "ChangeMeImmediately2026!";
        var salt = AqlanDentalPro.Application.Services.AuthService.GenerateSalt();
        var hash = AqlanDentalPro.Application.Services.AuthService.HashPassword(newPassword, salt);

        await resetDb.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" SET "PasswordHash" = {0}, "PasswordSalt" = {1}, "IsActive" = true, "UpdatedAt" = NOW()
            WHERE "Username" = 'admin'
        """, hash, salt);

        // Set the flag so this never runs again
        await resetDb.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Settings" ("Id", "Key", "Value", "Category", "UpdatedAt")
            VALUES (gen_random_uuid(), 'admin.password.reset.2026', 'done', 'system', NOW())
        """);

        resetLogger.LogWarning("Admin password has been reset to default value. Username: admin. CHANGE PASSWORD IMMEDIATELY!");
    }
    else
    {
        resetLogger.LogInformation("Admin password reset already applied, skipping");
    }
}
catch (Exception ex)
{
    var resetLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    resetLogger2.LogWarning(ex, "Admin password reset hotfix failed (non-fatal)");
}

// ── Website Settings Seed (unconditional, idempotent) ────────────────────────
try
{
    using var wsScope = app.Services.CreateScope();
    var wsDb     = wsScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var wsLogger = wsScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var websiteDefaults = new Dictionary<string, string>
    {
        ["website.clinicName"]           = "مركز الدكتور عقلان الكامل لتقويم وزراعة وتجميل الأسنان",
        ["website.heroTitle"]            = "ابتسامة تجمع بين دقة العلم ولمسة الفن",
        ["website.heroSubtitle"]         = "مركز الدكتور عقلان الكامل يقدم رعاية متكاملة في تقويم وزراعة وتجميل الأسنان، مع تشخيص دقيق وخطط علاج واضحة ومتابعة مستمرة لكل حالة.",
        ["website.marketingSlogan"]      = "قيادة طبية… وابتسامة بثقة",
        ["website.aboutText"]            = "يقدم مركز الدكتور عقلان الكامل خدمات تخصصية شاملة في تقويم وزراعة وتجميل الأسنان، معتمدين على تشخيص دقيق، وخطط علاج واضحة، ومتابعة مستمرة للحالات للمساعدة في الوصول إلى نتائج علاجية دقيقة ومناسبة لكل حالة.",
        ["website.phone"]                = "04-253028",
        ["website.whatsapp"]             = "967770245745",
        ["website.address"]              = "تعز، اليمن — شارع التحرير الأعلى",
        ["website.workingHours"]         = "السبت – الخميس: 8 ص – 8 م",
        ["website.facebook"]             = "",
        ["website.instagram"]            = "",
        ["website.logoUrl"]              = "",
        ["website.heroImageUrl"]         = "",
        ["website.servicesSectionTitle"] = "حلول طبية متكاملة لابتسامة صحية وواثقة",
        ["website.bookingButtonText"]    = "احجز موعدك الآن",
        ["website.whatsappButtonText"]   = "تواصل عبر الواتساب",
    };

    var existingKeys = await wsDb.Settings
        .Where(s => s.Category == "website")
        .Select(s => s.Key)
        .ToListAsync();

    foreach (var (key, value) in websiteDefaults)
    {
        if (!existingKeys.Contains(key))
        {
            wsDb.Settings.Add(new AqlanDentalPro.Domain.Entities.Setting
            {
                Key = key,
                Value = value,
                Category = "website",
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    if (wsDb.ChangeTracker.HasChanges())
    {
        await wsDb.SaveChangesAsync();
        wsLogger.LogInformation("Website settings seeded ({Count} new keys)", websiteDefaults.Count - existingKeys.Count);
    }
    else
    {
        wsLogger.LogInformation("Website settings already exist, no seeding needed");
    }
}
catch (Exception ex)
{
    var wsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    wsLogger2.LogWarning(ex, "Website settings seed hotfix failed (non-fatal)");
}

// ── DB Maintenance (gated by ENABLE_STARTUP_DB_MAINTENANCE + advisory lock) ────
// TD-020: remaining raw SQL blocks — see docs/technical-debt/TD-020-raw-sql-inventory.md
var enableStartupDbMaintenance =
    builder.Configuration.GetValue<bool>("ENABLE_STARTUP_DB_MAINTENANCE");

if (enableStartupDbMaintenance)
{
    using (var scope = app.Services.CreateScope())
    {
        var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // ── Acquire advisory lock ───────────────────────────────────────────────
    var lockKey = builder.Configuration.GetValue<int>("DB_MAINTENANCE_LOCK_KEY", 918273645);
    var acquiredLock = false;
    try
    {
        await db.Database.OpenConnectionAsync();
        using (var lockCmd = db.Database.GetDbConnection().CreateCommand())
        {
            lockCmd.CommandText = $"SELECT pg_try_advisory_lock({lockKey})";
            var lockResult = await lockCmd.ExecuteScalarAsync();
            acquiredLock = lockResult is bool b && b;
        }
    }
    catch (Exception lockEx)
    {
        logger.LogWarning(lockEx, "Failed to acquire advisory lock for DB maintenance, proceeding without lock");
        acquiredLock = true; // Proceed without lock if advisory locks aren't supported
    }

    if (!acquiredLock)
    {
        logger.LogInformation("DB maintenance advisory lock not acquired — another instance is running maintenance. Skipping.");
    }
    else
    {
        logger.LogInformation("DB maintenance advisory lock acquired — proceeding with schema maintenance");

    // Pre-migration: Add new columns that EF Core expects but may not exist yet
    try
    {
        // B2 (soft-delete columns) removed in TD-020 Phase C1-a --
        // now handled by EF migration 20260522000000_AddSoftDeleteColumnsToLegacyTables

        // B3/B8/B9 normalized phone schema removed in TD-020 Phase C1-b;
        // now handled by EF migration 20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes.

        // NormalizedPhone/NormalizedWhatsApp backfill + dedup removed in TD-020 Phase C1-e;
        // now handled by EF migrations 20260501000000 and 20260523000000.

        // B10-B13 conversation schema removed in TD-020 Phase C1-d;
        // now handled by EF migration 20260524000000_AddConversationPatientBranchFieldsAndIndexes.

        logger.LogInformation("Pre-migration schema updates applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply pre-migration schema updates");
    }

    // PatientAccounts table creation — now handled by EF migration 20260430120000_AddPatientPortal
    // (removed in TD-020 Phase C1-e)

    // Add Username/PasswordHash/PasswordSalt/InitialPassword columns to PatientAccounts
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'Username') THEN
                    ALTER TABLE "PatientAccounts" ADD COLUMN "Username" character varying(50) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash') THEN
                    ALTER TABLE "PatientAccounts" ADD COLUMN "PasswordHash" character varying(256) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordSalt') THEN
                    ALTER TABLE "PatientAccounts" ADD COLUMN "PasswordSalt" character varying(128) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'InitialPassword') THEN
                    ALTER TABLE "PatientAccounts" ADD COLUMN "InitialPassword" character varying(20) NULL;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PatientAccounts_Username" ON "PatientAccounts" ("Username") WHERE "Username" IS NOT NULL;
        """);
        logger.LogInformation("PatientAccounts username/password columns ensured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to add username/password columns to PatientAccounts");
    }

    // Visits/Documents columns — now handled by EF migration 20260502000000_AddVisitsDocumentsFields
    // (removed in TD-020 Phase C1-e)

    // Ensure Sprint 4.5 queue columns exist on Appointments table
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Appointments') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'RoomName') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "RoomName" character varying(50) NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ArrivedAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "ArrivedAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'CalledAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "CalledAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'InRoomAt') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "InRoomAt" timestamp with time zone NULL;
                    END IF;
                    CREATE INDEX IF NOT EXISTS "IX_Appointments_AppointmentDate" ON "Appointments" ("AppointmentDate");
                END IF;
            END $$;
        """);

        logger.LogInformation("Sprint 4.5 queue columns ensured on Appointments table");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure Sprint 4.5 queue columns");
    }

    // Ensure Sprint 5 DoctorSchedules table exists
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DoctorSchedules" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "DoctorId" uuid NOT NULL,
                "DayOfWeek" integer NOT NULL,
                "StartTime" time without time zone NOT NULL,
                "EndTime" time without time zone NOT NULL,
                "IsWorking" boolean NOT NULL DEFAULT TRUE,
                "BreakStart" time without time zone NULL,
                "BreakEnd" time without time zone NULL,
                "SlotDurationMinutes" integer NOT NULL DEFAULT 30,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "DeletedAt" timestamp with time zone NULL,
                "DeletedBy" uuid NULL
            );
        """);

        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_DoctorSchedules_Doctors_DoctorId') THEN
                    ALTER TABLE "DoctorSchedules" ADD CONSTRAINT "FK_DoctorSchedules_Doctors_DoctorId"
                        FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id") ON DELETE CASCADE;
                END IF;
            END $$;
        """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_DoctorSchedules_DoctorId_DayOfWeek"
                ON "DoctorSchedules" ("DoctorId", "DayOfWeek")
                WHERE "IsActive" = TRUE;
        """);

        logger.LogInformation("Sprint 5 DoctorSchedules table ensured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure Sprint 5 DoctorSchedules table");
    }

    // NOTE: Conversation RecipientType/RecipientUserId columns, BookingRequests table,
    // Message IsEdited/EditedAt fields, BookingRequests DoctorId, and Sprint 6 Doctor
    // compensation columns were previously in unconditional hotfix blocks.
    // They have been consolidated into this gated maintenance block with advisory lock.
    // The remaining pre-migration blocks above already ensure all required columns.

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed, attempting to ensure messaging tables exist manually");

        // Manually create messaging tables if they don't exist
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Conversations" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "Title" character varying(200) NOT NULL,
                    "IsGroup" boolean NOT NULL,
                    "CreatedBy" uuid NULL,
                    "LastMessageAt" timestamp with time zone NULL,
                    "LastMessagePreview" character varying(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Conversations_LastMessageAt" ON "Conversations" ("LastMessageAt");
                
                ALTER TABLE "Conversations" DROP CONSTRAINT IF EXISTS "FK_Conversations_Users_CreatedBy";
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Users_CreatedBy') THEN
                        ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Users_CreatedBy" 
                            FOREIGN KEY ("CreatedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
                
                -- Add Phase 1-4 columns to Conversations
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                END IF;
                -- RecipientType/RecipientUserId columns are ensured by the unconditional hotfix block above
                CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId" ON "Conversations" ("PatientId");
                CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType" ON "Conversations" ("ConversationType");
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ConversationParticipants" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "ConversationId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "IsAdmin" boolean NOT NULL,
                    "LastReadAt" timestamp with time zone NULL,
                    "IsMuted" boolean NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConversationParticipants_ConversationId_UserId" 
                    ON "ConversationParticipants" ("ConversationId", "UserId");
                
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Conversations_ConversationId') THEN
                        ALTER TABLE "ConversationParticipants" ADD CONSTRAINT "FK_ConversationParticipants_Conversations_ConversationId" 
                            FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Users_UserId') THEN
                        ALTER TABLE "ConversationParticipants" ADD CONSTRAINT "FK_ConversationParticipants_Users_UserId" 
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Messages" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "ConversationId" uuid NOT NULL,
                    "SenderId" uuid NOT NULL,
                    "Content" text NOT NULL,
                    "AttachmentUrl" character varying(1000) NULL,
                    "AttachmentName" character varying(255) NULL,
                    "AttachmentType" character varying(50) NULL,
                    "ReplyToId" uuid NULL,
                    "IsSystemMessage" boolean NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_Messages_ConversationId" ON "Messages" ("ConversationId");
                CREATE INDEX IF NOT EXISTS "IX_Messages_CreatedAt" ON "Messages" ("CreatedAt");
                
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Conversations_ConversationId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Conversations_ConversationId" 
                            FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Users_SenderId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Users_SenderId" 
                            FOREIGN KEY ("SenderId") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Messages_ReplyToId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Messages_ReplyToId" 
                            FOREIGN KEY ("ReplyToId") REFERENCES "Messages"("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "MessageReads" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "MessageId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "ReadAt" timestamp with time zone NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MessageReads_MessageId_UserId" 
                    ON "MessageReads" ("MessageId", "UserId");
                
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Messages_MessageId') THEN
                        ALTER TABLE "MessageReads" ADD CONSTRAINT "FK_MessageReads_Messages_MessageId" 
                            FOREIGN KEY ("MessageId") REFERENCES "Messages"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Users_UserId') THEN
                        ALTER TABLE "MessageReads" ADD CONSTRAINT "FK_MessageReads_Users_UserId" 
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                END $$;
            """);

            logger.LogInformation("Messaging tables created manually as fallback");
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Failed to create messaging tables manually");
        }
    }

    // ClinicQueueItems table + tracking fields — now handled by EF migrations
    // 20260514000000_AddClinicQueueItems and 20260520000000_AddClinicQueueTrackingFields
    // (TD-010 / TD-010 safety nets removed in TD-020 Phase C1-e)

    await DbSeeder.SeedAsync(db, logger);

    // Seed PatientAccounts for existing patients
    try
    {
        using var seedScope = app.Services.CreateScope();
        var portalSvc = seedScope.ServiceProvider.GetRequiredService<IPatientPortalService>();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var patientsWithoutAccount = await seedDb.Patients
            .Where(p => p.IsActive && !seedDb.PatientAccounts.Any(a => a.PatientId == p.Id))
            .Take(100)
            .ToListAsync();
        foreach (var p in patientsWithoutAccount)
        {
            await portalSvc.EnsurePatientAccountAsync(p.Id, p.PatientNumber, p.Phone);
        }
        if (patientsWithoutAccount.Count > 0)
            logger.LogInformation("Seeded PatientAccounts for {Count} existing patients", patientsWithoutAccount.Count);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to seed PatientAccounts for existing patients");
    }
    } // end else (acquiredLock)

    // ── Release advisory lock ───────────────────────────────────────────────
    if (acquiredLock)
    {
        try
        {
            using var releaseCmd = db.Database.GetDbConnection().CreateCommand();
            releaseCmd.CommandText = $"SELECT pg_advisory_unlock({lockKey})";
            await releaseCmd.ExecuteNonQueryAsync();
            logger.LogInformation("DB maintenance advisory lock released");
        }
        catch (Exception releaseEx)
        {
            logger.LogWarning(releaseEx, "Failed to release advisory lock (will auto-release on connection close)");
        }
    }

    try { await db.Database.CloseConnectionAsync(); } catch { /* ignore */ }

    } // end using scope
} // end if (enableStartupDbMaintenance)
else
{
    app.Logger.LogInformation("Startup DB maintenance is disabled (ENABLE_STARTUP_DB_MAINTENANCE=false). Skipping migrations and seed.");
}

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseSecurityHeaders();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Serve uploaded files — resolve writable uploads directory
// Priority: 1) UPLOADS_PATH env var (Railway persistent volume), 2) wwwroot/uploads, 3) /tmp fallback
var uploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH");
if (!string.IsNullOrWhiteSpace(uploadsPath))
{
    // UPLOADS_PATH is set — use the persistent volume path directly
    Directory.CreateDirectory(uploadsPath);
}
else
{
    uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    try
    {
        Directory.CreateDirectory(uploadsPath);
        var testFile = Path.Combine(uploadsPath, $".write-test-{Guid.NewGuid()}");
        File.WriteAllText(testFile, "test");
        File.Delete(testFile);
    }
    catch
    {
        // wwwroot not writable (e.g., container running as non-root without pre-created dir)
        uploadsPath = Path.Combine(Path.GetTempPath(), "aqlan-uploads");
        Directory.CreateDirectory(uploadsPath);
    }
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

// C-01 FIX: Swagger only enabled in non-production environments
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aqlan Dental Pro v1"));
}

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseMiddleware<AuditLogMiddleware>();
app.MapControllers();

app.Run();
