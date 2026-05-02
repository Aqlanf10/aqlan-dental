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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

    // Patient portal access - for patient-facing mobile app
    opts.AddPolicy("PatientAccess", policy =>
        policy.RequireRole("Patient"));
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
            // Allow any Vercel preview deployment URL (*.vercel.app)
            if (origin.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}));

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

// ── Migrate + Seed ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Pre-migration: Add new columns that EF Core expects but may not exist yet
    try
    {
        // Add DeletedAt/DeletedBy to all tables that inherit BaseEntity
        var baseEntityTables = new[] {
            "Patients", "Users", "Doctors", "Branches", "Appointments",
            "Conversations", "ConversationParticipants", "Messages", "MessageReads",
            "Visits", "Payments", "Contracts", "OrthoCases", "OrthoVisits",
            "TreatmentStages", "RetentionRecords", "SurgeryCases", "Prescriptions",
            "Notifications", "AuditLogs", "Settings", "Inventory", "LabOrders",
            "InternalReferrals", "ClinicalPhotos", "Radiographs", "Documents",
            "DentalCharts", "ToothConditions", "GeneralTreatments",
            "WhatsAppMessages", "WhatsAppTemplates", "PatientAccounts",
            "CephAnalyses", "PerioRecords", "GeneralTreatmentPlanItems",
            "MedicalHistories", "DentalHistories", "Receipts"
        };
        foreach (var table in baseEntityTables)
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync($"""
                    DO $$ BEGIN
                        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{table}') THEN
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = 'DeletedAt') THEN
                                ALTER TABLE "{table}" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                            END IF;
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = 'DeletedBy') THEN
                                ALTER TABLE "{table}" ADD COLUMN "DeletedBy" uuid NULL;
                            END IF;
                        END IF;
                    END $$;
                """);
            }
            catch (Exception tableEx)
            {
                logger.LogWarning(tableEx, "Skipping soft-delete columns for table {Table}", table);
            }
        }

        // Add NormalizedPhone/NormalizedWhatsApp to Patients
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone') THEN
                    ALTER TABLE "Patients" ADD COLUMN "NormalizedPhone" character varying(20) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedWhatsApp') THEN
                    ALTER TABLE "Patients" ADD COLUMN "NormalizedWhatsApp" character varying(20) NULL;
                END IF;
            END $$;
        """);

        // Backfill NormalizedPhone/NormalizedWhatsApp
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Patients" SET "NormalizedPhone" = 
                CASE 
                    WHEN "Phone" IS NULL OR "Phone" = '' THEN NULL
                    ELSE LTRIM(RTRIM(
                        CASE 
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '')) = 9 AND REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                                '967' || REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '')
                            ELSE REPLACE(REPLACE(REPLACE(REPLACE("Phone", ' ', ''), '-', ''), '(', ''), ')', '')
                        END
                    ))
                END
            WHERE "NormalizedPhone" IS NULL AND "Phone" IS NOT NULL AND "Phone" != '';
        """);

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE "Patients" SET "NormalizedWhatsApp" = 
                CASE 
                    WHEN "WhatsApp" IS NULL OR "WhatsApp" = '' THEN NULL
                    ELSE LTRIM(RTRIM(
                        CASE 
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                            WHEN REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                                '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                            WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')) = 9 AND REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                                '967' || REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')
                            ELSE REPLACE(REPLACE(REPLACE(REPLACE("WhatsApp", ' ', ''), '-', ''), '(', ''), ')', '')
                        END
                    ))
                END
            WHERE "NormalizedWhatsApp" IS NULL AND "WhatsApp" IS NOT NULL AND "WhatsApp" != '';
        """);

        // Create unique indexes for NormalizedPhone/NormalizedWhatsApp (with deduplication)
        // First: NULL out duplicates, keeping only the first (oldest) record
        await db.Database.ExecuteSqlRawAsync("""
            WITH duplicates AS (
                SELECT "Id", "NormalizedPhone", 
                       ROW_NUMBER() OVER (PARTITION BY "NormalizedPhone" ORDER BY "CreatedAt" ASC) as rn
                FROM "Patients" 
                WHERE "NormalizedPhone" IS NOT NULL AND "NormalizedPhone" != ''
            )
            UPDATE "Patients" SET "NormalizedPhone" = NULL
            FROM duplicates
            WHERE "Patients"."Id" = duplicates."Id" AND duplicates.rn > 1;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            WITH duplicates AS (
                SELECT "Id", "NormalizedWhatsApp", 
                       ROW_NUMBER() OVER (PARTITION BY "NormalizedWhatsApp" ORDER BY "CreatedAt" ASC) as rn
                FROM "Patients" 
                WHERE "NormalizedWhatsApp" IS NOT NULL AND "NormalizedWhatsApp" != ''
            )
            UPDATE "Patients" SET "NormalizedWhatsApp" = NULL
            FROM duplicates
            WHERE "Patients"."Id" = duplicates."Id" AND duplicates.rn > 1;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedPhone" 
                ON "Patients" ("NormalizedPhone") 
                WHERE "NormalizedPhone" IS NOT NULL AND "NormalizedPhone" != '';
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedWhatsApp" 
                ON "Patients" ("NormalizedWhatsApp") 
                WHERE "NormalizedWhatsApp" IS NOT NULL AND "NormalizedWhatsApp" != '';
        """);

        // Add ConversationType/PatientId/BranchId to Conversations
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                END IF;
            END $$;
        """);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId" ON "Conversations" ("PatientId");
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType" ON "Conversations" ("ConversationType");
        """);

        // Add FK for Conversations.PatientId -> Patients.Id
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
                    ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Patients_PatientId" 
                        FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
        """);

        // Record migrations in history
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260501000000_AddNormalizedPhoneFields', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields'
            );
        """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260501010000_AddPatientConversationSupport', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport'
            );
        """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260501020000_AddSoftDeleteToMessagingTables', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables'
            );
        """);

        logger.LogInformation("Pre-migration schema updates applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply pre-migration schema updates");
    }

    // Ensure PatientAccounts table exists — separate try/catch so failures
    // in the main pre-migration block don't prevent this from running.
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "PatientAccounts" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "PatientId" uuid NOT NULL,
                "PhoneNumber" character varying(20) NOT NULL,
                "VerificationCode" character varying(10) NULL,
                "VerificationCodeExpiry" timestamp with time zone NULL,
                "IsVerified" boolean NOT NULL DEFAULT FALSE,
                "LastLogin" timestamp with time zone NULL,
                "DeviceToken" character varying(500) NULL,
                "RefreshToken" character varying(256) NULL,
                "RefreshTokenExpiry" timestamp with time zone NULL,
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "DeletedAt" timestamp with time zone NULL,
                "DeletedBy" uuid NULL
            );
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PatientAccounts_Patients_PatientId') THEN
                    ALTER TABLE "PatientAccounts" ADD CONSTRAINT "FK_PatientAccounts_Patients_PatientId"
                        FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE CASCADE;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PatientAccounts_PatientId"
                ON "PatientAccounts" ("PatientId");
        """);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PatientAccounts_PhoneNumber"
                ON "PatientAccounts" ("PhoneNumber");
        """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260430120000_AddPatientPortal', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430120000_AddPatientPortal'
            );
        """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260430140000_AddWhatsAppIntegration', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430140000_AddWhatsAppIntegration'
            );
        """);
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260430160000_AddGeneralDentistryEnhancements', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430160000_AddGeneralDentistryEnhancements'
            );
        """);
        logger.LogInformation("PatientAccounts table ensured and migration history updated");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure PatientAccounts table exists");
    }

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

    // Ensure Visits/Documents new columns exist — separate try/catch for Sprint 4
    try
    {
        // Add Diagnosis column to Visits
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'Diagnosis') THEN
                        ALTER TABLE "Visits" ADD COLUMN "Diagnosis" text NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'NextVisitPlan') THEN
                        ALTER TABLE "Visits" ADD COLUMN "NextVisitPlan" text NULL;
                    END IF;
                    CREATE INDEX IF NOT EXISTS "IX_Visits_PatientId" ON "Visits" ("PatientId");
                    CREATE INDEX IF NOT EXISTS "IX_Visits_VisitDate" ON "Visits" ("VisitDate");
                END IF;
            END $$;
        """);

        // Add new columns to Documents
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Documents') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Documents' AND column_name = 'FileName') THEN
                        ALTER TABLE "Documents" ADD COLUMN "FileName" text NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Documents' AND column_name = 'FileSize') THEN
                        ALTER TABLE "Documents" ADD COLUMN "FileSize" bigint NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Documents' AND column_name = 'MimeType') THEN
                        ALTER TABLE "Documents" ADD COLUMN "MimeType" text NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Documents' AND column_name = 'Notes') THEN
                        ALTER TABLE "Documents" ADD COLUMN "Notes" text NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Documents' AND column_name = 'UploadedBy') THEN
                        ALTER TABLE "Documents" ADD COLUMN "UploadedBy" uuid NULL;
                    END IF;
                    CREATE INDEX IF NOT EXISTS "IX_Documents_PatientId" ON "Documents" ("PatientId");
                    CREATE INDEX IF NOT EXISTS "IX_Documents_DocumentType" ON "Documents" ("DocumentType");
                END IF;
            END $$;
        """);

        // Record Sprint 4 migration in history
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260502000000_AddVisitsDocumentsFields', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields'
            );
        """);

        logger.LogInformation("Sprint 4 Visits/Documents columns ensured successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure Sprint 4 Visits/Documents columns");
    }

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

        // Record Sprint 4.5 migration in history
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260502100000_AddQueueFieldsToAppointments', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502100000_AddQueueFieldsToAppointments'
            );
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

        // Record Sprint 5 migration in history
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            SELECT '20260502120000_AddDoctorSchedules', '8.0.8'
            WHERE NOT EXISTS (
                SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502120000_AddDoctorSchedules'
            );
        """);

        logger.LogInformation("Sprint 5 DoctorSchedules table ensured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure Sprint 5 DoctorSchedules table");
    }

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

            // Record the migration in history if not already recorded
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260430000000_AddMessagingSystem', '8.0.8'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMessagingSystem'
                );
            """);

            logger.LogInformation("Messaging tables created manually as fallback");
        }
        catch (Exception innerEx)
        {
            logger.LogError(innerEx, "Failed to create messaging tables manually");
        }
    }

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
}

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Serve uploaded files
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
try { Directory.CreateDirectory(uploadsPath); } catch { /* directory creation may fail in restricted environments */ }
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aqlan Dental Pro v1"));

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseMiddleware<AuditLogMiddleware>();
app.MapControllers();

app.Run();
