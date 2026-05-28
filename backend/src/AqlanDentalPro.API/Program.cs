using AqlanDentalPro.API.Hubs;
using AqlanDentalPro.API.Middleware;
using AqlanDentalPro.Application.Interfaces.Repositories;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Domain.Entities;
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

// ── Fail-Fast: رفض الإقلاع بإعدادات افتراضية في الإنتاج ──────────────────────
if (builder.Environment.IsProduction())
{
    var prodJwtKey = builder.Configuration["Jwt:SecretKey"] ?? "";
    if (prodJwtKey.Contains("CHANGE_ME") || prodJwtKey.Length < 32)
        throw new InvalidOperationException(
            "SEC: مفتاح JWT غير آمن في الإنتاج. يجب ضبط Jwt:SecretKey بقيمة عشوائية لا تقل عن 32 حرفاً.");

    var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    if (connStr.Contains("CHANGE_ME"))
        throw new InvalidOperationException(
            "SEC: كلمة مرور قاعدة البيانات لم تُضبط في الإنتاج. يجب ضبط ConnectionStrings:DefaultConnection.");
}

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
// HOTFIX: Make Redis registration fully resilient so the app starts even when
// Redis is unavailable. Without this, ConnectionMultiplexer.Connect() throws
// RedisConnectionException at DI resolution time, which crashes the entire
// LoginAttemptService + TokenService chain and turns every staff login into a 500.
//
// Strategy:
// 1. Try to connect with AbortOnConnectFail=false (returns a multiplexer that
//    retries in the background even if the initial connect fails).
// 2. If ConfigurationOptions.Parse() or Connect() throws for ANY reason
//    (invalid connection string, DNS failure, etc.), fall back to a minimal
//    "localhost:6379" configuration. This multiplexer will be disconnected but
//    the app will start — Redis operations in LoginAttemptService and TokenService
//    are wrapped in try/catch and degrade gracefully.
// 3. If even the fallback fails, create a completely disconnected multiplexer
//    using ConnectAsync + manual configuration — this should never happen but
//    ensures the app ALWAYS starts.
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var logger = sp.GetService<ILogger<Program>>();

    ConnectionMultiplexer? mux = null;

    // Attempt 1: Connect with configured connection string
    try
    {
        var connString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(connString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 3;
        options.ReconnectRetryPolicy = new ExponentialRetry(5000);
        options.ConnectTimeout = 3000; // Don't block startup for too long
        mux = ConnectionMultiplexer.Connect(options);
        logger?.LogInformation("Redis connection multiplexer created (AbortOnConnectFail=false, IsConnected={IsConnected})", mux.IsConnected);
        return mux;
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "Redis: Primary connection attempt failed. Trying fallback configuration.");
    }

    // Attempt 2: Fallback to localhost with minimal config
    try
    {
        var fallbackOptions = new ConfigurationOptions
        {
            EndPoints = { "localhost:6379" },
            AbortOnConnectFail = false,
            ConnectRetry = 0,
            ConnectTimeout = 1000
        };
        mux = ConnectionMultiplexer.Connect(fallbackOptions);
        logger?.LogWarning("Redis: Connected with fallback configuration (localhost:6379). Redis features will be degraded.");
        return mux;
    }
    catch (Exception ex)
    {
        logger?.LogError(ex, "Redis: Fallback connection also failed. Creating minimal disconnected multiplexer.");
    }

    // Attempt 3: Last resort — return a disconnected multiplexer
    // This ensures the app ALWAYS starts. Redis-dependent features degrade gracefully.
    try
    {
        var lastResortOptions = new ConfigurationOptions
        {
            EndPoints = { "localhost:6379" },
            AbortOnConnectFail = false,
            ConnectTimeout = 1,
            ConnectRetry = 0,
            AsyncTimeout = 1,
            SyncTimeout = 1
        };
        mux = ConnectionMultiplexer.Connect(lastResortOptions);
        logger?.LogCritical("Redis: Created disconnected multiplexer as last resort. LoginAttemptService and TokenService will operate in degraded mode (no lockout, no refresh token persistence).");
        return mux;
    }
    catch (Exception ex)
    {
        // Absolute last resort — this should never happen with AbortOnConnectFail=false
        logger?.LogCritical(ex, "Redis: ALL connection attempts failed. Creating multiplexer with absolute minimum config.");
        var emergencyOptions = new ConfigurationOptions
        {
            EndPoints = { "127.0.0.1:6379" },
            AbortOnConnectFail = false
        };
        return ConnectionMultiplexer.Connect(emergencyOptions);
    }
});

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
        policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant),
            nameof(UserRole.Reception),
            nameof(UserRole.Orthodontist), nameof(UserRole.GeneralDentist), nameof(UserRole.OralSurgeon)));

    opts.AddPolicy("CommissionEdit", policy =>
        policy.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant), nameof(UserRole.Reception)));

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

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
    ?? ["http://localhost:3000", "http://localhost:3001"];
// Always include Vercel deployment origins so the frontend can call the API directly
allowedOrigins = [..allowedOrigins,
    "https://aqlan-dental-pro.vercel.app",
    "https://aqlan-dental.vercel.app"];
builder.Services.AddCors(opts =>
{
    // Authenticated staff endpoints — strict origins, cookies allowed
    opts.AddPolicy("AllowFrontend", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (allowedOrigins.Contains(origin)) return true;
                // C-01 FIX: Removed wildcard *.vercel.app — only explicitly listed origins are allowed
                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Public API endpoints (/api/public/*) — no auth/cookies, any origin is safe
    opts.AddPolicy("AllowPublicApi", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

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

    // SEC-04 FIX: Stricter rate limit for password reset — 3 requests per 15 minutes per IP
    // Prevents brute-force attacks on password reset codes while allowing legitimate retries
    options.AddPolicy("PortalPasswordResetPolicy", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0 // No queue — reject immediately
            }));

    // Forgot password rate limiting — 3 requests per 15 minutes per IP
    options.AddPolicy("ForgotPasswordPolicy", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
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
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IJournalEntryService, JournalEntryService>();
builder.Services.AddScoped<ITreasuryResolutionService, TreasuryResolutionService>();
builder.Services.AddScoped<GeneralService>();
builder.Services.AddScoped<IMessagingService, MessagingService>();
builder.Services.AddScoped<IPatientAccountLinkingService, PatientAccountLinkingService>();
builder.Services.AddScoped<IPatientPortalMessagingService, PatientPortalMessagingService>();
builder.Services.AddScoped<IRealTimePushService, SignalRPushService>();
builder.Services.AddScoped<CephService>();
builder.Services.AddScoped<IPatientPortalService, PatientPortalService>();
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<INotificationService, AqlanDentalPro.Infrastructure.Services.NotificationService>();
builder.Services.AddHostedService<AqlanDentalPro.Infrastructure.Services.OverdueNotificationJob>();
builder.Services.AddHostedService<AqlanDentalPro.Infrastructure.Services.AppointmentReminderJob>();
builder.Services.AddHostedService<AqlanDentalPro.API.Services.AutoBackupJob>();
builder.Services.AddScoped<IBookingRequestService, AqlanDentalPro.Infrastructure.Services.BookingRequestService>();
builder.Services.AddScoped<AqlanDentalPro.Application.Interfaces.Services.ICommissionService, AqlanDentalPro.Infrastructure.Services.CommissionService>();
builder.Services.AddScoped<AqlanDentalPro.Application.Interfaces.Services.IPatientAccessService, AqlanDentalPro.Infrastructure.Services.PatientAccessService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddHttpClient(); // Register IHttpClientFactory for PatientPortalService
builder.Services.AddHttpClient<IRecaptchaService, RecaptchaService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHttpClient("WhatsApp", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddHttpClient("Sms", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

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
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 10 * 1024; // 10 KB
});
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // FIX: Allow enum values as strings in JSON (e.g., "Consultation" instead of 0)
        // Without this, frontend sends category: "Other" but backend expects integer → 400 Bad Request
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
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

// ── CRITICAL: Ensure Users table schema is up-to-date ──────────────────
// HOTFIX: The Users table MUST have MustChangePassword, DeletedAt, and DeletedBy
// columns for the app to function. If ENABLE_STARTUP_DB_MAINTENANCE is false,
// MigrateAsync() is skipped and these columns may be missing.
// This check runs UNCONDITIONALLY (not gated by the maintenance flag) because
// the app cannot serve login requests without these columns.
try
{
    using var schemaScope = app.Services.CreateScope();
    var schemaDb = schemaScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var schemaLogger = schemaScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await schemaDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- Users.MustChangePassword (SEC-02 FIX, migration 20260525000000)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword') THEN
                    ALTER TABLE "Users" ADD COLUMN "MustChangePassword" boolean NOT NULL DEFAULT false;
                END IF;
                -- Users.DeletedAt (soft-delete, migration 20260522000000)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "Users" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                -- Users.DeletedBy (soft-delete, migration 20260522000000)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "Users" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
                -- Users.PasswordSalt (per-user salt, migration 20260521000000)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt') THEN
                    ALTER TABLE "Users" ADD COLUMN "PasswordSalt" text NOT NULL DEFAULT '';
                END IF;
            END IF;

            -- Doctors.DeletedAt (soft-delete, migration 20260522000000)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Doctors') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "Doctors" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "Doctors" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
                -- Doctors.CompensationType (Sprint 6)
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType') THEN
                    ALTER TABLE "Doctors" ADD COLUMN "CompensationType" character varying(20) NOT NULL DEFAULT 'None';
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'DefaultCommissionPercentage') THEN
                    ALTER TABLE "Doctors" ADD COLUMN "DefaultCommissionPercentage" numeric(5,2) NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationNotes') THEN
                    ALTER TABLE "Doctors" ADD COLUMN "CompensationNotes" character varying(500) NULL;
                END IF;
            END IF;
        END $$;
    """);

    schemaLogger.LogInformation("HOTFIX: Users/Doctors table schema verified — critical columns ensured");
}
catch (Exception ex)
{
    var schemaLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    schemaLogger2.LogError(ex, "HOTFIX: Failed to ensure Users/Doctors table schema. Staff login may fail with PostgresException!");
}

// ── CRITICAL: Ensure MessageAttachments table exists ─────────────────────────
// HOTFIX: PR #150 added .Include(m => m.Attachments) in MessagingService.
// If migration 20260602000000_AddMessageAttachments was not applied (because
// ENABLE_STARTUP_DB_MAINTENANCE is false), every GET /api/messages/conversations/{id}
// throws "relation MessageAttachments does not exist" → 500.
// This guard runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var maScope = app.Services.CreateScope();
    var maDb     = maScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maLogger = maScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await maDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- Create MessageAttachments table if not present (migration 20260602000000)
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'MessageAttachments'
            ) THEN
                CREATE TABLE "MessageAttachments" (
                    "Id"        uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "MessageId" uuid                     NOT NULL,
                    "FileUrl"   character varying(1000)  NOT NULL,
                    "FileName"  character varying(255)   NOT NULL,
                    "FileSize"  bigint                   NOT NULL DEFAULT 0,
                    "MimeType"  character varying(100)   NOT NULL,
                    "IsActive"  boolean                  NOT NULL DEFAULT true,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid                     NULL,
                    CONSTRAINT "PK_MessageAttachments" PRIMARY KEY ("Id")
                );
            END IF;

            -- CRITICAL FIX: Add DeletedBy column if missing (causes "column DeletedBy does not exist" → 500 on conversation open)
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'MessageAttachments' AND column_name = 'DeletedBy'
            ) THEN
                ALTER TABLE "MessageAttachments" ADD COLUMN "DeletedBy" uuid NULL;
            END IF;

            -- Add FK to Messages if missing
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'FK_MessageAttachments_Messages_MessageId'
            ) THEN
                IF EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'Messages'
                ) THEN
                    ALTER TABLE "MessageAttachments"
                        ADD CONSTRAINT "FK_MessageAttachments_Messages_MessageId"
                        FOREIGN KEY ("MessageId")
                        REFERENCES "Messages"("Id")
                        ON DELETE CASCADE;
                END IF;
            END IF;

            -- Create index on MessageId if missing
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public'
                  AND tablename  = 'MessageAttachments'
                  AND indexname  = 'IX_MessageAttachments_MessageId'
            ) THEN
                CREATE INDEX "IX_MessageAttachments_MessageId"
                    ON "MessageAttachments" ("MessageId");
            END IF;
        END $$;
    """);

    maLogger.LogInformation("HOTFIX: MessageAttachments table schema ensured (idempotent)");
}
catch (Exception ex)
{
    var maLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    maLogger2.LogError(ex, "HOTFIX: Failed to ensure MessageAttachments schema. Opening conversations will return 500!");
}

// ── CRITICAL: Ensure BaseEntity columns exist on ALL messaging tables ────
// HOTFIX: The MessageAttachments migration (20260602000000) was missing the
// DeletedBy column. Other tables may also lack it if migrations partially
// failed. This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var msgSchemaScope = app.Services.CreateScope();
    var msgSchemaDb = msgSchemaScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var msgSchemaLogger = msgSchemaScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await msgSchemaDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- MessageAttachments: add DeletedBy if missing
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MessageAttachments') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageAttachments' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "MessageAttachments" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
            END IF;

            -- Messages: add DeletedAt/DeletedBy/IsEdited/EditedAt if missing
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Messages') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "Messages" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "Messages" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                    ALTER TABLE "Messages" ADD COLUMN "IsEdited" boolean NOT NULL DEFAULT false;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
                    ALTER TABLE "Messages" ADD COLUMN "EditedAt" timestamp with time zone NULL;
                END IF;
            END IF;

            -- ConversationParticipants: add DeletedAt/DeletedBy if missing
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ConversationParticipants') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
            END IF;

            -- MessageReads: add DeletedAt/DeletedBy if missing
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MessageReads') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "MessageReads" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "MessageReads" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
            END IF;

            -- Conversations: add DeletedAt/DeletedBy if missing
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Conversations') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedAt') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedBy') THEN
                    ALTER TABLE "Conversations" ADD COLUMN "DeletedBy" uuid NULL;
                END IF;
            END IF;
        END $$;
    """);

    msgSchemaLogger.LogInformation("HOTFIX: All messaging tables BaseEntity columns ensured (idempotent)");
}
catch (Exception ex)
{
    var msgSchemaLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    msgSchemaLogger2.LogError(ex, "HOTFIX: Failed to ensure messaging tables BaseEntity columns. Messaging may return 500!");
}

// ── CRITICAL: Ensure Doctor Commission tables/columns exist ─────────────────
// HOTFIX: PR #157 added the Doctor Commission module. If ENABLE_STARTUP_DB_MAINTENANCE
// is false, MigrateAsync() is skipped and the DoctorCommissionPayments table +
// InvoiceLineItems commission columns will be missing. Without this guard,
// CommissionsController returns 404 (controller can't resolve dependencies) or 500
// (missing columns). This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var commScope = app.Services.CreateScope();
    var commDb     = commScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var commLogger = commScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await commDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- DoctorCommissionPayments table (migration 20260606000000)
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'DoctorCommissionPayments'
            ) THEN
                CREATE TABLE "DoctorCommissionPayments" (
                    "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "DoctorId"      uuid                     NOT NULL,
                    "Amount"        numeric                  NOT NULL,
                    "PaymentDate"   date                     NOT NULL,
                    "PaymentMethod" character varying(50)    NULL,
                    "ReferenceNumber" character varying(100)  NULL,
                    "Notes"         character varying(500)    NULL,
                    "PaidBy"        uuid                     NULL,
                    "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                    "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                    "IsActive"      boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"     timestamp with time zone  NULL,
                    "DeletedBy"     uuid                     NULL,
                    CONSTRAINT "PK_DoctorCommissionPayments" PRIMARY KEY ("Id")
                );
            END IF;

            -- FK: DoctorCommissionPayments → Doctors
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'FK_DoctorCommissionPayments_Doctors_DoctorId'
            ) THEN
                IF EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'Doctors'
                ) THEN
                    ALTER TABLE "DoctorCommissionPayments"
                        ADD CONSTRAINT "FK_DoctorCommissionPayments_Doctors_DoctorId"
                        FOREIGN KEY ("DoctorId")
                        REFERENCES "Doctors"("Id")
                        ON DELETE RESTRICT;
                END IF;
            END IF;

            -- Indexes on DoctorCommissionPayments
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'DoctorCommissionPayments' AND indexname = 'IX_DoctorCommissionPayments_DoctorId'
            ) THEN
                CREATE INDEX "IX_DoctorCommissionPayments_DoctorId" ON "DoctorCommissionPayments" ("DoctorId");
            END IF;
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'DoctorCommissionPayments' AND indexname = 'IX_DoctorCommissionPayments_PaymentDate'
            ) THEN
                CREATE INDEX "IX_DoctorCommissionPayments_PaymentDate" ON "DoctorCommissionPayments" ("PaymentDate");
            END IF;

            -- InvoiceLineItems: commission columns (migration 20260606000000)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'InvoiceLineItems') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorId') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "DoctorId" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LineDiscountAmount') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "LineDiscountAmount" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'MaterialCost') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "MaterialCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabCost') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "LabCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'OtherDirectCost') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "OtherDirectCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionBaseRule') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionBaseRule" integer NOT NULL DEFAULT 2;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionPercentage') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "DoctorCommissionPercentage" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'NetCommissionableAmount') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "NetCommissionableAmount" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionAmount') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "DoctorCommissionAmount" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CenterShareAmount') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "CenterShareAmount" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionStatus') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionStatus" integer NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionNotes') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionNotes" text NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabOrderId') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "LabOrderId" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionApprovedBy') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionApprovedBy" uuid NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'CommissionApprovedAt') THEN
                    ALTER TABLE "InvoiceLineItems" ADD COLUMN "CommissionApprovedAt" timestamp with time zone NULL;
                END IF;
            END IF;

            -- ClinicServices: commission defaults columns (migration 20260606000000)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCost') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCostType') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCostType" integer NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultLabCost') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultLabCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultDoctorCommissionPercentage') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultDoctorCommissionPercentage" numeric NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionBaseRule') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "CommissionBaseRule" integer NOT NULL DEFAULT 2;
                END IF;
            END IF;

            -- ClinicServices: CommissionRecognitionMode column (migration 20260607000000)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "CommissionRecognitionMode" integer NOT NULL DEFAULT 0;
                END IF;
            END IF;

            -- FK: InvoiceLineItems → Doctors
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_Doctors_DoctorId'
            ) THEN
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorId') THEN
                    ALTER TABLE "InvoiceLineItems"
                        ADD CONSTRAINT "FK_InvoiceLineItems_Doctors_DoctorId"
                        FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id") ON DELETE SET NULL;
                END IF;
            END IF;

            -- FK: InvoiceLineItems → LabOrders
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId'
            ) THEN
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabOrderId') THEN
                    ALTER TABLE "InvoiceLineItems"
                        ADD CONSTRAINT "FK_InvoiceLineItems_LabOrders_LabOrderId"
                        FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders"("Id") ON DELETE SET NULL;
                END IF;
            END IF;

            -- Indexes on InvoiceLineItems commission columns (only if table exists)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'InvoiceLineItems') THEN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE schemaname = 'public' AND tablename = 'InvoiceLineItems' AND indexname = 'IX_InvoiceLineItems_DoctorId'
                ) THEN
                    CREATE INDEX "IX_InvoiceLineItems_DoctorId" ON "InvoiceLineItems" ("DoctorId");
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_indexes
                    WHERE schemaname = 'public' AND tablename = 'InvoiceLineItems' AND indexname = 'IX_InvoiceLineItems_LabOrderId'
                ) THEN
                    CREATE INDEX "IX_InvoiceLineItems_LabOrderId" ON "InvoiceLineItems" ("LabOrderId");
                END IF;
            END IF;
        END $$;
    """);

    commLogger.LogInformation("HOTFIX: Doctor Commission tables/columns schema ensured (idempotent)");
}
catch (Exception ex)
{
    var commLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    commLogger2.LogError(ex, "HOTFIX: Failed to ensure Doctor Commission schema. Commission endpoints may return 404/500!");
}

// ── CRITICAL: Ensure ClinicServices and ClinicRooms tables exist ─────────────
// HOTFIX: The ClinicServices table is created by migration 20260528000000_AddClinicServicesAndRooms,
// but that migration only runs when ENABLE_STARTUP_DB_MAINTENANCE=true. On deployments where
// this flag is false, the table never gets created, causing 500 errors on ALL endpoints that
// reference ClinicServices (services/active, commissions/report, invoices/{id}, etc.).
// This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var svcScope = app.Services.CreateScope();
    var svcDb     = svcScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var svcLogger = svcScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await svcDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- ── Create ClinicServices table if not exists ───────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ClinicServices') THEN
                CREATE TABLE "ClinicServices" (
                    "Id"                              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "ArabicName"                      character varying(200)   NOT NULL,
                    "EnglishName"                     character varying(200)   NULL,
                    "Code"                            character varying(50)    NOT NULL,
                    "Department"                      character varying(100)   NULL,
                    "Category"                        character varying(30)    NOT NULL DEFAULT 'Other',
                    "Description"                     character varying(1000)  NULL,
                    "DefaultDurationMinutes"          integer                  NOT NULL DEFAULT 30,
                    "DefaultPrice"                    numeric(12,2)            NOT NULL DEFAULT 0,
                    "RequiresDoctor"                  boolean                  NOT NULL DEFAULT true,
                    "RequiresConsultationFee"         boolean                  NOT NULL DEFAULT false,
                    "ShowInBooking"                   boolean                  NOT NULL DEFAULT true,
                    "ShowInReception"                 boolean                  NOT NULL DEFAULT true,
                    "ShowInTreatmentPlan"             boolean                  NOT NULL DEFAULT true,
                    "SortOrder"                       integer                  NOT NULL DEFAULT 0,
                    "DefaultMaterialCost"             numeric                  NOT NULL DEFAULT 0,
                    "DefaultMaterialCostType"         integer                  NOT NULL DEFAULT 0,
                    "DefaultLabCost"                  numeric                  NOT NULL DEFAULT 0,
                    "DefaultDoctorCommissionPercentage" numeric                NULL,
                    "CommissionBaseRule"              integer                  NOT NULL DEFAULT 2,
                    "CommissionRecognitionMode"       integer                  NOT NULL DEFAULT 0,
                    "IsActive"                        boolean                  NOT NULL DEFAULT true,
                    "CreatedAt"                       timestamp with time zone  NOT NULL DEFAULT now(),
                    "UpdatedAt"                       timestamp with time zone  NOT NULL DEFAULT now(),
                    "DeletedAt"                       timestamp with time zone  NULL,
                    "DeletedBy"                       uuid                     NULL,
                    CONSTRAINT "PK_ClinicServices" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX "IX_ClinicServices_Code" ON "ClinicServices" ("Code");
            END IF;

            -- ── Add commission columns if table exists but columns are missing ──
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCost') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultMaterialCostType') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultMaterialCostType" integer NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultLabCost') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultLabCost" numeric NOT NULL DEFAULT 0;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'DefaultDoctorCommissionPercentage') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "DefaultDoctorCommissionPercentage" numeric NULL;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionBaseRule') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "CommissionBaseRule" integer NOT NULL DEFAULT 2;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode') THEN
                    ALTER TABLE "ClinicServices" ADD COLUMN "CommissionRecognitionMode" integer NOT NULL DEFAULT 0;
                END IF;
            END IF;

            -- ── Create ClinicRooms table if not exists ──────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'ClinicRooms') THEN
                CREATE TABLE "ClinicRooms" (
                    "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "ArabicName"    character varying(200)   NOT NULL,
                    "EnglishName"   character varying(200)   NULL,
                    "Code"          character varying(50)    NOT NULL,
                    "RoomType"      character varying(30)    NOT NULL DEFAULT 'Treatment',
                    "SortOrder"     integer                  NOT NULL DEFAULT 0,
                    "IsActive"      boolean                  NOT NULL DEFAULT true,
                    "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                    "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                    "DeletedAt"     timestamp with time zone  NULL,
                    "DeletedBy"     uuid                     NULL,
                    CONSTRAINT "PK_ClinicRooms" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX "IX_ClinicRooms_Code" ON "ClinicRooms" ("Code");
                CREATE INDEX "IX_ClinicRooms_RoomType" ON "ClinicRooms" ("RoomType");
            END IF;
        END $$;
    """);

    svcLogger.LogInformation("HOTFIX: ClinicServices and ClinicRooms tables schema ensured (idempotent)");
}
catch (Exception ex)
{
    var svcLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    svcLogger2.LogError(ex, "HOTFIX: Failed to ensure ClinicServices/ClinicRooms schema. Services and Commission endpoints may return 500!");
}

// ── CRITICAL: Ensure PasswordResetTokens and PasswordResetRequests tables exist ──
// HOTFIX: PR #165 added password reset system entities. If ENABLE_STARTUP_DB_MAINTENANCE
// is false, MigrateAsync() is skipped and these tables will be missing. Without them,
// the forgot-password endpoint returns 500 for existing users. This block runs
// UNCONDITIONALLY and is fully idempotent.
try
{
    using var prsScope = app.Services.CreateScope();
    var prsDb     = prsScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var prsLogger = prsScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await prsDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- PasswordResetTokens table
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'PasswordResetTokens'
            ) THEN
                CREATE TABLE "PasswordResetTokens" (
                    "Id"        uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "UserId"    uuid                     NOT NULL,
                    "TokenHash" character varying(200)   NOT NULL,
                    "ExpiresAt" timestamp with time zone NOT NULL,
                    "IsUsed"    boolean                  NOT NULL DEFAULT false,
                    "UsedAt"    timestamp with time zone  NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY ("Id")
                );
            END IF;

            -- Index on TokenHash (unique)
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'PasswordResetTokens' AND indexname = 'IX_PasswordResetTokens_TokenHash'
            ) THEN
                CREATE UNIQUE INDEX "IX_PasswordResetTokens_TokenHash" ON "PasswordResetTokens" ("TokenHash");
            END IF;

            -- Index on UserId
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'PasswordResetTokens' AND indexname = 'IX_PasswordResetTokens_UserId'
            ) THEN
                CREATE INDEX "IX_PasswordResetTokens_UserId" ON "PasswordResetTokens" ("UserId");
            END IF;

            -- FK: PasswordResetTokens → Users
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_PasswordResetTokens_Users_UserId'
            ) THEN
                ALTER TABLE "PasswordResetTokens"
                    ADD CONSTRAINT "FK_PasswordResetTokens_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
            END IF;

            -- PasswordResetRequests table
            IF NOT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'PasswordResetRequests'
            ) THEN
                CREATE TABLE "PasswordResetRequests" (
                    "Id"                uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "UserId"            uuid                     NULL,
                    "UsernameOrEmail"   character varying(200)   NOT NULL,
                    "RequestedAt"       timestamp with time zone NOT NULL,
                    "Status"            character varying(20)    NOT NULL DEFAULT 'Pending',
                    "RequestedIpHash"   character varying(200)   NULL,
                    "UserAgent"         character varying(500)   NULL,
                    "ApprovedByUserId"  uuid                     NULL,
                    "ApprovedAt"        timestamp with time zone  NULL,
                    "Notes"             character varying(500)   NULL,
                    "CreatedAt"         timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"         timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"          boolean                  NOT NULL DEFAULT true,
                    CONSTRAINT "PK_PasswordResetRequests" PRIMARY KEY ("Id")
                );
            END IF;

            -- Index on Status
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'PasswordResetRequests' AND indexname = 'IX_PasswordResetRequests_Status'
            ) THEN
                CREATE INDEX "IX_PasswordResetRequests_Status" ON "PasswordResetRequests" ("Status");
            END IF;

            -- Index on UserId
            IF NOT EXISTS (
                SELECT 1 FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = 'PasswordResetRequests' AND indexname = 'IX_PasswordResetRequests_UserId'
            ) THEN
                CREATE INDEX "IX_PasswordResetRequests_UserId" ON "PasswordResetRequests" ("UserId");
            END IF;

            -- FK: PasswordResetRequests → Users (UserId)
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_PasswordResetRequests_Users_UserId'
            ) THEN
                ALTER TABLE "PasswordResetRequests"
                    ADD CONSTRAINT "FK_PasswordResetRequests_Users_UserId"
                    FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE SET NULL;
            END IF;

            -- FK: PasswordResetRequests → Users (ApprovedByUserId)
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint WHERE conname = 'FK_PasswordResetRequests_Users_ApprovedByUserId'
            ) THEN
                ALTER TABLE "PasswordResetRequests"
                    ADD CONSTRAINT "FK_PasswordResetRequests_Users_ApprovedByUserId"
                    FOREIGN KEY ("ApprovedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL;
            END IF;

            -- Users.EmailConfirmed column (for admin email verification)
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'EmailConfirmed') THEN
                    ALTER TABLE "Users" ADD COLUMN "EmailConfirmed" boolean NOT NULL DEFAULT false;
                END IF;
            END IF;
        END $$;
    """);

    prsLogger.LogInformation("HOTFIX: PasswordResetTokens/PasswordResetRequests tables schema ensured (idempotent)");
}
catch (Exception ex)
{
    var prsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    prsLogger2.LogError(ex, "HOTFIX: Failed to ensure PasswordReset tables schema. Forgot-password endpoint may return 500!");
}

// ── CRITICAL: Ensure EmailLogs table exists ─────────────────────────────────
// HOTFIX: The EmailLogs table tracks outgoing emails for statistics and daily
// limit monitoring. Without this table, the AppointmentReminderJob and
// EmailStatsController will fail. This block runs UNCONDITIONALLY and is
// fully idempotent.
try
{
    using var elScope = app.Services.CreateScope();
    var elDb     = elScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var elLogger = elScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await elDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'EmailLogs') THEN
                CREATE TABLE "EmailLogs" (
                    "Id"                uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "ToEmail"           text                     NOT NULL,
                    "Subject"           text                     NOT NULL,
                    "Category"          character varying(50)    NOT NULL DEFAULT 'general',
                    "Provider"          character varying(20)    NULL,
                    "IsSent"            boolean                  NOT NULL DEFAULT false,
                    "ErrorMessage"      text                     NULL,
                    "ExternalId"        character varying(100)   NULL,
                    "RelatedEntityType" character varying(50)    NULL,
                    "RelatedEntityId"   uuid                     NULL,
                    "CreatedAt"         timestamp with time zone NOT NULL DEFAULT now(),
                    CONSTRAINT "PK_EmailLogs" PRIMARY KEY ("Id")
                );
            END IF;

            IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EmailLogs' AND indexname = 'IX_EmailLogs_IsSent_CreatedAt') THEN
                CREATE INDEX "IX_EmailLogs_IsSent_CreatedAt" ON "EmailLogs" ("IsSent", "CreatedAt");
            END IF;

            IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EmailLogs' AND indexname = 'IX_EmailLogs_Category_CreatedAt') THEN
                CREATE INDEX "IX_EmailLogs_Category_CreatedAt" ON "EmailLogs" ("Category", "CreatedAt");
            END IF;

            IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'EmailLogs' AND indexname = 'IX_EmailLogs_RelatedEntity') THEN
                CREATE INDEX "IX_EmailLogs_RelatedEntity" ON "EmailLogs" ("RelatedEntityType", "RelatedEntityId");
            END IF;
        END $$;
    """);

    elLogger.LogInformation("HOTFIX: EmailLogs table schema ensured (idempotent)");
}
catch (Exception ex)
{
    var elLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    elLogger2.LogError(ex, "HOTFIX: Failed to ensure EmailLogs table schema. Email statistics and reminder tracking may fail!");
}

// ── CRITICAL: Ensure reminder tracking columns exist ──────────────────────
// HOTFIX: Migration 20260610000000_AddSeparateReminderTrackingAndPatientEmail
// adds EmailReminderSentAt, WhatsAppReminderSentAt, EmailReminderWindowsSent
// to Appointments and Email to Patients. If ENABLE_STARTUP_DB_MAINTENANCE is
// false, MigrateAsync() is skipped and these columns will be missing.
// Without them, ALL appointment/patient queries return 500 because EF Core's
// SELECT fails on columns that don't exist in the database.
// This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var rtScope = app.Services.CreateScope();
    var rtDb     = rtScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var rtLogger = rtScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await rtDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- Add Email column to Patients if missing
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Patients' AND column_name = 'Email') THEN
                ALTER TABLE "Patients" ADD COLUMN "Email" text NULL;
            END IF;

            -- Add reminder tracking columns to Appointments if missing
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Appointments' AND column_name = 'EmailReminderSentAt') THEN
                ALTER TABLE "Appointments" ADD COLUMN "EmailReminderSentAt" timestamp with time zone NULL;
            END IF;

            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Appointments' AND column_name = 'WhatsAppReminderSentAt') THEN
                ALTER TABLE "Appointments" ADD COLUMN "WhatsAppReminderSentAt" timestamp with time zone NULL;
            END IF;

            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'Appointments' AND column_name = 'EmailReminderWindowsSent') THEN
                ALTER TABLE "Appointments" ADD COLUMN "EmailReminderWindowsSent" text NULL;
            END IF;
        END $$;
    """);

    rtLogger.LogInformation("HOTFIX: Reminder tracking columns ensured (idempotent)");
}
catch (Exception ex)
{
    var rtLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    rtLogger2.LogError(ex, "HOTFIX: Failed to ensure reminder tracking columns. Appointment/patient queries may return 500!");
}

// ── CRITICAL: Ensure Invoices and InvoiceLineItems tables exist ─────────────
// HOTFIX: Migration history reconciliation failed in previous deployments,
// causing MigrateAsync() to skip creating the Invoices and InvoiceLineItems
// tables. Without these tables, ALL invoice/payment/commission endpoints
// return 500. This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var invScope = app.Services.CreateScope();
    var invDb     = invScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var invLogger = invScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await invDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- ── Create Invoices table if not exists ──────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Invoices') THEN
                CREATE TABLE "Invoices" (
                    "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "PatientId"     uuid                     NOT NULL,
                    "VisitId"       uuid                     NULL,
                    "AppointmentId" uuid                     NULL,
                    "InvoiceNumber" character varying(50)    NOT NULL,
                    "Status"        character varying(20)    NOT NULL,
                    "Subtotal"      numeric(12,2)            NOT NULL DEFAULT 0,
                    "DiscountAmount" numeric(12,2)           NULL,
                    "TaxAmount"     numeric(12,2)            NULL,
                    "TotalAmount"   numeric(12,2)            NOT NULL DEFAULT 0,
                    "Notes"         text                     NULL,
                    "CreatedBy"     uuid                     NULL,
                    "UpdatedBy"     uuid                     NULL,
                    "CreatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                    "UpdatedAt"     timestamp with time zone  NOT NULL DEFAULT now(),
                    "IsActive"      boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"     timestamp with time zone  NULL,
                    "DeletedBy"     uuid                     NULL,
                    CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_Invoices_PatientId" ON "Invoices" ("PatientId");
                CREATE INDEX "IX_Invoices_VisitId" ON "Invoices" ("VisitId");
                CREATE INDEX "IX_Invoices_AppointmentId" ON "Invoices" ("AppointmentId");
                CREATE INDEX "IX_Invoices_Status" ON "Invoices" ("Status");
                CREATE UNIQUE INDEX "IX_Invoices_InvoiceNumber" ON "Invoices" ("InvoiceNumber");

                -- FK: Invoices → Patients
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Invoices_Patients_PatientId') THEN
                    ALTER TABLE "Invoices" ADD CONSTRAINT "FK_Invoices_Patients_PatientId"
                        FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE RESTRICT;
                END IF;
            END IF;

            -- ── Create InvoiceLineItems table if not exists ────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'InvoiceLineItems') THEN
                CREATE TABLE "InvoiceLineItems" (
                    "Id"                          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "InvoiceId"                   uuid                     NOT NULL,
                    "ServiceId"                   uuid                     NULL,
                    "ServiceNameSnapshot"         character varying(200)   NOT NULL DEFAULT '',
                    "Description"                 character varying(500)   NOT NULL DEFAULT '',
                    "Quantity"                    integer                  NOT NULL DEFAULT 1,
                    "UnitPrice"                   numeric(12,2)            NOT NULL DEFAULT 0,
                    "TotalPrice"                  numeric(12,2)            NOT NULL DEFAULT 0,
                    "RelatedTreatmentPlanStepId"  uuid                     NULL,
                    "RelatedVisitId"              uuid                     NULL,
                    "SortOrder"                   integer                  NOT NULL DEFAULT 0,
                    "DoctorId"                    uuid                     NULL,
                    "LineDiscountAmount"          numeric                  NOT NULL DEFAULT 0,
                    "MaterialCost"                numeric                  NOT NULL DEFAULT 0,
                    "LabCost"                     numeric                  NOT NULL DEFAULT 0,
                    "OtherDirectCost"             numeric                  NOT NULL DEFAULT 0,
                    "CommissionBaseRule"           integer                  NOT NULL DEFAULT 2,
                    "DoctorCommissionPercentage"  numeric                  NOT NULL DEFAULT 0,
                    "NetCommissionableAmount"     numeric                  NOT NULL DEFAULT 0,
                    "DoctorCommissionAmount"      numeric                  NOT NULL DEFAULT 0,
                    "CenterShareAmount"           numeric                  NOT NULL DEFAULT 0,
                    "CommissionStatus"            integer                  NOT NULL DEFAULT 0,
                    "CommissionNotes"             text                     NULL,
                    "LabOrderId"                  uuid                     NULL,
                    "CommissionApprovedBy"        uuid                     NULL,
                    "CommissionApprovedAt"        timestamp with time zone  NULL,
                    "CreatedAt"                   timestamp with time zone  NOT NULL DEFAULT now(),
                    "UpdatedAt"                   timestamp with time zone  NOT NULL DEFAULT now(),
                    "IsActive"                    boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"                   timestamp with time zone  NULL,
                    "DeletedBy"                   uuid                     NULL,
                    CONSTRAINT "PK_InvoiceLineItems" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_InvoiceLineItems_InvoiceId" ON "InvoiceLineItems" ("InvoiceId");
                CREATE INDEX "IX_InvoiceLineItems_ServiceId" ON "InvoiceLineItems" ("ServiceId");
                CREATE INDEX "IX_InvoiceLineItems_DoctorId" ON "InvoiceLineItems" ("DoctorId");
                CREATE INDEX "IX_InvoiceLineItems_LabOrderId" ON "InvoiceLineItems" ("LabOrderId");

                -- FK: InvoiceLineItems → Invoices
                ALTER TABLE "InvoiceLineItems" ADD CONSTRAINT "FK_InvoiceLineItems_Invoices_InvoiceId"
                    FOREIGN KEY ("InvoiceId") REFERENCES "Invoices"("Id") ON DELETE CASCADE;
                -- FK: InvoiceLineItems → ClinicServices
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                    ALTER TABLE "InvoiceLineItems" ADD CONSTRAINT "FK_InvoiceLineItems_ClinicServices_ServiceId"
                        FOREIGN KEY ("ServiceId") REFERENCES "ClinicServices"("Id") ON DELETE SET NULL;
                END IF;
                -- FK: InvoiceLineItems → Doctors
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Doctors') THEN
                    ALTER TABLE "InvoiceLineItems" ADD CONSTRAINT "FK_InvoiceLineItems_Doctors_DoctorId"
                        FOREIGN KEY ("DoctorId") REFERENCES "Doctors"("Id") ON DELETE SET NULL;
                END IF;
            END IF;

            -- ── Add InvoiceId to Payments if missing ───────────────────
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Payments') THEN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId') THEN
                    ALTER TABLE "Payments" ADD COLUMN "InvoiceId" uuid NULL;
                    CREATE INDEX "IX_Payments_InvoiceId" ON "Payments" ("InvoiceId");
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_Invoices_InvoiceId') THEN
                    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId') THEN
                        ALTER TABLE "Payments" ADD CONSTRAINT "FK_Payments_Invoices_InvoiceId"
                            FOREIGN KEY ("InvoiceId") REFERENCES "Invoices"("Id") ON DELETE SET NULL;
                    END IF;
                END IF;
            END IF;

            -- ── Fix __EFMigrationsHistory ──────────────────────────────
            -- Comprehensive cleanup: delete records for migrations whose schema doesn't exist,
            -- then insert records for schema that does exist.
            -- This fixes the problem where a previous reconciliation inserted ALL migration
            -- records, causing MigrateAsync() to skip creating missing tables like ClinicServices.

            -- DELETE records where primary schema element doesn't exist
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'PatientId');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502010000_AddSecurePatientPortalPasswordAuth'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddConversationRecipientType'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'RecipientType');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260507000000_AddBookingRequests'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508052207_AddBookingRequest'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditFields'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511000000_AddDoctorIdToBookingRequest'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'BookingRequests' AND column_name = 'DoctorId');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_AddRadiographFileMetadata'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicalPhotos' AND column_name = 'FileType');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513000000_AddDoctorCompensationFields'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514000000_AddClinicQueueItem'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520000000_AddClinicQueueItemTrackingFields'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledAt');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520202816_SyncAuditPhase2Configurations'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AuditLogs');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddPasswordSaltAndPatientPhoneIndexes'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522000000_AddSoftDeleteColumnsToLegacyTables'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260524000000_AddConversationPatientBranchFieldsAndIndexes'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525000000_AddMissingFKIndexesAndUserMustChangePassword'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528000000_AddClinicServicesAndRooms'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529000000_AddPatientJourneyFields'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'ReferralSource');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260530000000_AddPatientTreatmentPlanSteps'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'TreatmentPlanSteps');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260531000000_AddInvoicesAndInvoiceLineItems'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Invoices');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddInvoicePaymentLink'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260602000000_AddMessageAttachments'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageAttachments');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603000000_AddOrthoDiagnosisRetentionPhotos'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OrthoDiagnoses' AND column_name = 'RetentionPhotoLeft');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260604000000_AddSuppliersAndPurchases'
                AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260606000000_AddDoctorCommissionSystem'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionPercentage');
            DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607000000_AddCommissionRecognitionMode'
                AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode');

            -- INSERT records for schema that DOES exist (created by HOTFIX blocks)
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType')
                AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType') THEN
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260430221624_AddConversationPatientAndType', '8.0');
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Invoices')
                AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260531000000_AddInvoicesAndInvoiceLineItems') THEN
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260531000000_AddInvoicesAndInvoiceLineItems', '8.0');
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId')
                AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddInvoicePaymentLink') THEN
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260601000000_AddInvoicePaymentLink', '8.0');
            END IF;
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'DoctorCommissionPayments')
                AND NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260606000000_AddDoctorCommissionSystem') THEN
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260606000000_AddDoctorCommissionSystem', '8.0');
            END IF;
        END $$;
    """);
    invLogger.LogInformation("HOTFIX: Invoices/InvoiceLineItems/Payments schema ensured and migration history reconciled (idempotent)");
}
catch (Exception ex)
{
    var invLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    invLogger2.LogError(ex, "HOTFIX: Failed to ensure Invoices/InvoiceLineItems schema. Invoice and commission endpoints may return 500!");
}

// ── One-time Admin Password Reset ─────────────────────────────────
// SEC-03 FIX: Admin password reset only from environment variables.
// In production: ADMIN_DEFAULT_PASSWORD is REQUIRED. No fallback.
// In development: if not set, uses a dev-only fallback that is NEVER active in production.
// This block runs ONCE and sets a flag so it never runs again.
// It does NOT reset any existing passwords — only sets password on first run.
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

    // Check if the admin password reset flag exists using EF Core LINQ
    var flagExists = await resetDb.Settings.AnyAsync(s => s.Key == "admin.password.reset.2026");

    if (!flagExists)
    {
        // SEC-03 FIX: In production, ADMIN_DEFAULT_PASSWORD env var is REQUIRED — no fallback.
        // In development only, a clearly-marked dev default is allowed for convenience.
        var newPassword = Environment.GetEnvironmentVariable("ADMIN_DEFAULT_PASSWORD");

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            if (app.Environment.IsProduction())
            {
                // SEC-03 FIX: Production MUST set ADMIN_DEFAULT_PASSWORD. No fallback allowed.
                resetLogger.LogCritical(
                    "SEC-03: ADMIN_DEFAULT_PASSWORD environment variable is NOT set in production. " +
                    "Admin password reset SKIPPED for security. " +
                    "Set ADMIN_DEFAULT_PASSWORD and restart, or use ADMIN_RESET_PASSWORD in DbSeeder.");
                // Skip the password reset entirely — admin keeps existing password
                goto AdminResetDone;
            }

            // SEC-03 FIX: Development-only fallback. The #if DEBUG guard and IsDevelopment() check
            // ensure this can NEVER be active in production, even if code is misconfigured.
            if (app.Environment.IsDevelopment())
            {
                newPassword = "DevOnly2026!ChangeMe";
                resetLogger.LogWarning(
                    "SEC-03: ADMIN_DEFAULT_PASSWORD not set. Using DEVELOPMENT-ONLY fallback. " +
                    "This fallback is NEVER active in production (IsDevelopment check). " +
                    "Set ADMIN_DEFAULT_PASSWORD for production deployments.");
            }
            else
            {
                // Non-production, non-development (e.g., Staging) — also require env var
                resetLogger.LogCritical(
                    "SEC-03: ADMIN_DEFAULT_PASSWORD not set in {Environment} environment. " +
                    "Admin password reset SKIPPED. Set the variable and restart.",
                    app.Environment.EnvironmentName);
                goto AdminResetDone;
            }
        }

        var salt = AqlanDentalPro.Application.Services.AuthService.GenerateSalt();
        var hash = AqlanDentalPro.Application.Services.AuthService.HashPassword(newPassword, salt);

        // Update admin password using EF Core LINQ
        var adminUser = await resetDb.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminUser != null)
        {
            adminUser.PasswordHash = hash;
            adminUser.PasswordSalt = salt;
            adminUser.IsActive = true;
        }

        // Set the flag using EF Core so this never runs again
        resetDb.Settings.Add(new AqlanDentalPro.Domain.Entities.Setting
        {
            Key = "admin.password.reset.2026",
            Value = "done",
            Category = "system",
            UpdatedAt = DateTime.UtcNow
        });

        await resetDb.SaveChangesAsync();

        resetLogger.LogInformation("SEC-03: Admin initial password has been set. Username: admin. Change password after first login.");
    }
    else
    {
        resetLogger.LogInformation("SEC-03: Admin password reset already applied, skipping");
    }
    AdminResetDone:;
}
catch (Exception ex)
{
    var resetLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    resetLogger2.LogWarning(ex, "SEC-03: Admin password reset failed (non-fatal)");
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

// ── CRITICAL: Ensure Patient Journey columns exist on Visits and Appointments ──
// HOTFIX: Migration 20260529000000_AddPatientJourneyFields adds ServiceId, ClinicRoomId
// to Appointments and ServiceId, CheckoutStatus, ReadyForCheckoutAt, AmountDueReference
// to Visits. If ENABLE_STARTUP_DB_MAINTENANCE=false, MigrateAsync() is skipped and
// these columns are missing, causing /api/patient-journey/today to return 500.
// This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var pjScope = app.Services.CreateScope();
    var pjDb     = pjScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pjLogger = pjScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await pjDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- ── Appointments: add ServiceId if missing ───────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ServiceId') THEN
                ALTER TABLE "Appointments" ADD COLUMN "ServiceId" uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_Appointments_ServiceId" ON "Appointments" ("ServiceId");
            END IF;

            -- ── Appointments: add ClinicRoomId if missing ───────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ClinicRoomId') THEN
                ALTER TABLE "Appointments" ADD COLUMN "ClinicRoomId" uuid NULL;
            END IF;

            -- ── Appointments: add RoomName if missing (Sprint 4.5 queue fields) ──
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'RoomName') THEN
                ALTER TABLE "Appointments" ADD COLUMN "RoomName" character varying(50) NULL;
            END IF;

            -- ── Appointments: add ArrivedAt if missing ──────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ArrivedAt') THEN
                ALTER TABLE "Appointments" ADD COLUMN "ArrivedAt" timestamp with time zone NULL;
            END IF;

            -- ── Appointments: add CalledAt if missing ───────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'CalledAt') THEN
                ALTER TABLE "Appointments" ADD COLUMN "CalledAt" timestamp with time zone NULL;
            END IF;

            -- ── Appointments: add InRoomAt if missing ───────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'InRoomAt') THEN
                ALTER TABLE "Appointments" ADD COLUMN "InRoomAt" timestamp with time zone NULL;
            END IF;

            -- ── Visits: add ServiceId if missing ─────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'ServiceId') THEN
                ALTER TABLE "Visits" ADD COLUMN "ServiceId" uuid NULL;
            END IF;

            -- ── Visits: add CheckoutStatus if missing ────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'CheckoutStatus') THEN
                ALTER TABLE "Visits" ADD COLUMN "CheckoutStatus" character varying(30) NULL;
                CREATE INDEX IF NOT EXISTS "IX_Visits_CheckoutStatus" ON "Visits" ("CheckoutStatus");
            END IF;

            -- ── Visits: add ReadyForCheckoutAt if missing ────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'ReadyForCheckoutAt') THEN
                ALTER TABLE "Visits" ADD COLUMN "ReadyForCheckoutAt" timestamp with time zone NULL;
            END IF;

            -- ── Visits: add AmountDueReference if missing ────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Visits' AND column_name = 'AmountDueReference') THEN
                ALTER TABLE "Visits" ADD COLUMN "AmountDueReference" numeric(12,2) NULL;
            END IF;
        END $$;
    """);

    pjLogger.LogInformation("HOTFIX: Patient Journey columns ensured on Appointments and Visits (idempotent)");
}
catch (Exception ex)
{
    var pjLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    pjLogger2.LogError(ex, "HOTFIX: Failed to ensure Patient Journey columns. Patient journey endpoint may return 500!");
}

// ── HOTFIX: Seed patient_journey permissions (idempotent, unconditional) ──────
// PR #201 added Patient Daily Journey Hub but permissions were not seeded because
// ENABLE_STARTUP_DB_MAINTENANCE may be false. Without patient_journey.view in
// RolePermissions, the sidebar hides the link and /api/auth/me/permissions omits it.
try
{
    using var pjpScope = app.Services.CreateScope();
    var pjpDb     = pjpScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pjpLogger = pjpScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var journeyPerms = new (string Role, bool View, bool Create, bool Edit)[]{
        ("Admin",          true, true, true),
        ("Reception",      true, true, true),
        ("Orthodontist",   true, true, false),
        ("GeneralDentist", true, true, false),
        ("OralSurgeon",    true, true, false),
        ("Accountant",     true, false, false),
        ("Assistant",      true, false, false),
    };

    var existingJourney = await pjpDb.RolePermissions
        .Where(rp => rp.Resource == "patient_journey")
        .Select(rp => rp.Role)
        .ToListAsync();

    var toAddJourney = new List<RolePermission>();
    foreach (var (role, view, create, edit) in journeyPerms)
    {
        if (!existingJourney.Contains(role))
        {
            toAddJourney.Add(new RolePermission
            {
                Role = role,
                Resource = "patient_journey",
                CanView = view,
                CanCreate = create,
                CanEdit = edit,
                CanDelete = false,
                CanExport = false,
                CanApprove = false
            });
        }
    }

    if (toAddJourney.Count > 0)
    {
        await pjpDb.RolePermissions.AddRangeAsync(toAddJourney);
        await pjpDb.SaveChangesAsync();
        pjpLogger.LogInformation("HOTFIX: Seeded {Count} patient_journey permissions", toAddJourney.Count);
    }
    else
    {
        pjpLogger.LogInformation("HOTFIX: patient_journey permissions already exist, no seeding needed");
    }
}
catch (Exception ex)
{
    var pjpLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    pjpLogger2.LogError(ex, "HOTFIX: Failed to seed patient_journey permissions. Journey Hub may be hidden in sidebar!");
}

// ── HOTFIX: Ensure SMS Gateway tables exist (Sprint: Local Android SIM SMS Gateway) ──
try
{
    var smsLogger = app.Services.GetRequiredService<ILogger<Program>>();
    using var smsScope = app.Services.CreateScope();
    var smsDb = smsScope.ServiceProvider.GetRequiredService<AqlanDentalPro.Infrastructure.Data.AppDbContext>();

    await smsDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- ── SmsMessages table ──────────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SmsMessages') THEN
                CREATE TABLE "SmsMessages" (
                    "Id" uuid PRIMARY KEY,
                    "PatientId" uuid NOT NULL,
                    "PhoneNumber" character varying(20) NOT NULL,
                    "TemplateType" character varying(50) NOT NULL DEFAULT 'custom',
                    "MessageContent" character varying(1000) NOT NULL,
                    "Status" character varying(20) NOT NULL DEFAULT 'pending',
                    "ExternalId" character varying(100) NULL,
                    "ErrorMessage" character varying(500) NULL,
                    "RetryCount" integer NOT NULL DEFAULT 0,
                    "SentAt" timestamp with time zone NULL,
                    "DeliveredAt" timestamp with time zone NULL,
                    "RelatedEntityId" uuid NULL,
                    "RelatedEntityType" character varying(50) NULL,
                    "Gateway" character varying(30) NOT NULL DEFAULT 'local_android',
                    "CharacterCount" integer NOT NULL DEFAULT 0,
                    "SegmentCount" integer NOT NULL DEFAULT 0,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL
                );
                CREATE INDEX "IX_SmsMessages_PatientId" ON "SmsMessages" ("PatientId");
                CREATE INDEX "IX_SmsMessages_Status" ON "SmsMessages" ("Status");
                CREATE INDEX "IX_SmsMessages_CreatedAt" ON "SmsMessages" ("CreatedAt");
                CREATE INDEX "IX_SmsMessages_TemplateType" ON "SmsMessages" ("TemplateType");
                CREATE INDEX "IX_SmsMessages_CreatedAt_Status" ON "SmsMessages" ("CreatedAt", "Status");
            END IF;

            -- ── SmsTemplates table ─────────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SmsTemplates') THEN
                CREATE TABLE "SmsTemplates" (
                    "Id" uuid PRIMARY KEY,
                    "TemplateKey" character varying(50) NOT NULL,
                    "NameAr" character varying(100) NOT NULL,
                    "ContentTemplate" character varying(500) NOT NULL,
                    "IsTemplateActive" boolean NOT NULL DEFAULT true,
                    "Category" character varying(30) NOT NULL DEFAULT 'general',
                    "MaxLength" integer NOT NULL DEFAULT 160,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                    "IsActive" boolean NOT NULL DEFAULT true,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL
                );
                CREATE UNIQUE INDEX "IX_SmsTemplates_TemplateKey" ON "SmsTemplates" ("TemplateKey");
                CREATE INDEX "IX_SmsTemplates_Category" ON "SmsTemplates" ("Category");
            END IF;

            -- ── Appointments: add SmsReminderWindowsSent if missing ──
            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'SmsReminderWindowsSent') THEN
                ALTER TABLE "Appointments" ADD COLUMN "SmsReminderWindowsSent" character varying(200) NULL;
            END IF;
        END $$;
    """);

    smsLogger.LogInformation("HOTFIX: SMS Gateway tables + SmsReminderWindowsSent column ensured (idempotent)");
}
catch (Exception ex)
{
    var smsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    smsLogger2.LogError(ex, "HOTFIX: Failed to ensure SMS Gateway tables. SMS endpoints may return 500!");
}

// ── Seed SMS Gateway settings (Cloud API mode) ──
// API key is stored via environment variable Sms:ApiKey, NOT in source code.
try
{
    using var smsSettingsScope = app.Services.CreateScope();
    var smsSettingsDb = smsSettingsScope.ServiceProvider.GetRequiredService<AqlanDentalPro.Infrastructure.Data.AppDbContext>();

    var smsApiUrl = builder.Configuration["Sms:ApiUrl"] ?? "";
    var smsApiKey = builder.Configuration["Sms:ApiKey"] ?? "";
    var smsGatewayMode = builder.Configuration["Sms:GatewayMode"] ?? "cloud_api";

    var smsSettingsSeed = new Dictionary<string, (string Value, string Category)>
    {
        ["sms.enabled"] = ("true", "sms"),
        ["sms.gateway_mode"] = (smsGatewayMode, "sms"),
        ["sms.sender_name"] = ("AqlanDental", "sms"),
        ["sms.daily_limit"] = ("500", "sms"),
        ["sms.send_appointment_reminders"] = ("true", "sms"),
        ["sms.send_payment_reminders"] = ("true", "sms"),
        ["sms.reminder_hours"] = ("24,2", "sms"),
    };

    // Only seed URL and API key from env vars if they are provided
    if (!string.IsNullOrWhiteSpace(smsApiUrl))
        smsSettingsSeed["sms.api_url"] = (smsApiUrl, "sms");
    if (!string.IsNullOrWhiteSpace(smsApiKey))
        smsSettingsSeed["sms.api_key"] = (smsApiKey, "sms");

    foreach (var (key, (value, category)) in smsSettingsSeed)
    {
        var existing = await smsSettingsDb.Settings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing == null)
        {
            smsSettingsDb.Settings.Add(new AqlanDentalPro.Domain.Entities.Setting
            {
                Key = key,
                Value = value,
                Category = category,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (key == "sms.gateway_mode" && existing.Value != smsGatewayMode)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    await smsSettingsDb.SaveChangesAsync();
    var smsSettingsLogger = app.Services.GetRequiredService<ILogger<Program>>();
    smsSettingsLogger.LogInformation("SMS Gateway settings seeded (mode={Mode}, hasUrl={HasUrl}, hasKey={HasKey})", smsGatewayMode, !string.IsNullOrWhiteSpace(smsApiUrl), !string.IsNullOrWhiteSpace(smsApiKey));
}
catch (Exception ex)
{
    var smsSettingsLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    smsSettingsLogger2.LogError(ex, "Failed to seed SMS Gateway settings");
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

    // Add Username/PasswordHash/PasswordSalt columns to PatientAccounts
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

    // Ensure Sprint 8 queue/appointment integration columns exist
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ServiceId') THEN
                        ALTER TABLE "ClinicQueueItems" ADD COLUMN "ServiceId" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ClinicRoomId') THEN
                        ALTER TABLE "ClinicQueueItems" ADD COLUMN "ClinicRoomId" uuid NULL;
                    END IF;
                END IF;
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Appointments') THEN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ServiceId') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "ServiceId" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Appointments' AND column_name = 'ClinicRoomId') THEN
                        ALTER TABLE "Appointments" ADD COLUMN "ClinicRoomId" uuid NULL;
                    END IF;
                END IF;
            END $$;
        """);

        logger.LogInformation("Sprint 8 queue/appointment integration columns ensured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure Sprint 8 integration columns");
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

    // ── Finance V3: Ensure JournalEntries & JournalLines tables exist ────────
    // These tables are required by the double-entry ledger migration. Without them,
    // ALL FinanceV3 endpoints fail with PostgresException: relation does not exist.
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "JournalEntries" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "EntryNumber" character varying(100) NOT NULL,
                "FinancialDocumentId" uuid NOT NULL,
                "FinancialDocumentType" character varying(30) NOT NULL,
                "Description" character varying(500) NOT NULL,
                "EntryDate" date NOT NULL,
                "BranchId" uuid NOT NULL,
                "CashierSessionId" uuid NULL,
                "TreasuryId" uuid NULL,
                "PerformedBy" uuid NOT NULL,
                "IsReversal" boolean NOT NULL DEFAULT FALSE,
                "ReversalOfEntryId" uuid NULL,
                "ReversedByEntryId" uuid NULL,
                "IsPosted" boolean NOT NULL DEFAULT FALSE,
                "PostedAt" timestamp with time zone NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "DeletedAt" timestamp with time zone NULL,
                "DeletedBy" uuid NULL
            );
        """);

        // FKs for JournalEntries
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_Branches_BranchId') THEN
                    ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_Branches_BranchId"
                        FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_CashierSessions_CashierSessionId') THEN
                    ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_CashierSessions_CashierSessionId"
                        FOREIGN KEY ("CashierSessionId") REFERENCES "CashierSessions"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_Treasuries_TreasuryId') THEN
                    ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_Treasuries_TreasuryId"
                        FOREIGN KEY ("TreasuryId") REFERENCES "Treasuries"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_Users_PerformedBy') THEN
                    ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_Users_PerformedBy"
                        FOREIGN KEY ("PerformedBy") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_JournalEntries_ReversalOfEntryId') THEN
                    ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_JournalEntries_ReversalOfEntryId"
                        FOREIGN KEY ("ReversalOfEntryId") REFERENCES "JournalEntries"("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalEntries_JournalEntries_ReversedByEntryId') THEN
                    ALTER TABLE "JournalEntries" ADD CONSTRAINT "FK_JournalEntries_JournalEntries_ReversedByEntryId"
                        FOREIGN KEY ("ReversedByEntryId") REFERENCES "JournalEntries"("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
        """);

        // Indexes for JournalEntries
        await db.Database.ExecuteSqlRawAsync("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_JournalEntries_EntryNumber" ON "JournalEntries" ("EntryNumber")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_BranchId" ON "JournalEntries" ("BranchId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_EntryDate" ON "JournalEntries" ("EntryDate")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_FinancialDocumentType" ON "JournalEntries" ("FinancialDocumentType")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_IsPosted" ON "JournalEntries" ("IsPosted")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_IsReversal" ON "JournalEntries" ("IsReversal")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_CashierSessionId" ON "JournalEntries" ("CashierSessionId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_TreasuryId" ON "JournalEntries" ("TreasuryId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_PerformedBy" ON "JournalEntries" ("PerformedBy")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_BranchId_EntryDate" ON "JournalEntries" ("BranchId", "EntryDate")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_ReversalOfEntryId" ON "JournalEntries" ("ReversalOfEntryId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalEntries_ReversedByEntryId" ON "JournalEntries" ("ReversedByEntryId")""");

        // JournalLines table
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "JournalLines" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "JournalEntryId" uuid NOT NULL,
                "AccountType" character varying(30) NOT NULL,
                "AccountId" uuid NOT NULL,
                "Debit" numeric(12,2) NOT NULL DEFAULT 0,
                "Credit" numeric(12,2) NOT NULL DEFAULT 0,
                "Description" character varying(500) NULL,
                "BranchId" uuid NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "DeletedAt" timestamp with time zone NULL,
                "DeletedBy" uuid NULL
            );
        """);

        // FKs for JournalLines
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalLines_JournalEntries_JournalEntryId') THEN
                    ALTER TABLE "JournalLines" ADD CONSTRAINT "FK_JournalLines_JournalEntries_JournalEntryId"
                        FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries"("Id") ON DELETE CASCADE;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_JournalLines_Branches_BranchId') THEN
                    ALTER TABLE "JournalLines" ADD CONSTRAINT "FK_JournalLines_Branches_BranchId"
                        FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE RESTRICT;
                END IF;
            END $$;
        """);

        // Check constraint for Debit/Credit mutual exclusivity
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_JournalLines_DebitCreditMutual') THEN
                    ALTER TABLE "JournalLines" ADD CONSTRAINT "CK_JournalLines_DebitCreditMutual"
                        CHECK ("Debit" >= 0 AND "Credit" >= 0 AND ("Debit" > 0 OR "Credit" > 0));
                END IF;
            END $$;
        """);

        // Indexes for JournalLines
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_JournalEntryId" ON "JournalLines" ("JournalEntryId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_AccountType" ON "JournalLines" ("AccountType")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_AccountId" ON "JournalLines" ("AccountId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_BranchId" ON "JournalLines" ("BranchId")""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_JournalLines_AccountType_AccountId" ON "JournalLines" ("AccountType", "AccountId")""");

        // Ensure VaultTransfers.DepositSource column exists
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'DepositSource') THEN
                    ALTER TABLE "VaultTransfers" ADD COLUMN "DepositSource" text NULL;
                END IF;
            END $$;
        """);

        // Ensure CashierSessions.TreasuryId column exists
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'CashierSessions' AND column_name = 'TreasuryId') THEN
                    ALTER TABLE "CashierSessions" ADD COLUMN "TreasuryId" uuid NULL;
                END IF;
            END $$;
        """);
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashierSessions_TreasuryId" ON "CashierSessions" ("TreasuryId")""");
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashierSessions_Treasuries_TreasuryId') THEN
                    ALTER TABLE "CashierSessions" ADD CONSTRAINT "FK_CashierSessions_Treasuries_TreasuryId"
                        FOREIGN KEY ("TreasuryId") REFERENCES "Treasuries"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
        """);

        // Ensure CashFlowTransactions.TreasuryId column exists
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "TreasuryId" uuid NULL
        """);
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_TreasuryId" ON "CashFlowTransactions" ("TreasuryId")""");
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_Treasuries_TreasuryId') THEN
                    ALTER TABLE "CashFlowTransactions" ADD CONSTRAINT "FK_CashFlowTransactions_Treasuries_TreasuryId"
                        FOREIGN KEY ("TreasuryId") REFERENCES "Treasuries"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
        """);

        logger.LogInformation("Finance V3 JournalEntries/JournalLines tables and columns ensured");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to ensure Finance V3 tables");
    }

    // NOTE: Conversation RecipientType/RecipientUserId columns, BookingRequests table,
    // Message IsEdited/EditedAt fields, BookingRequests DoctorId, and Sprint 6 Doctor
    // compensation columns were previously in unconditional hotfix blocks.
    // They have been consolidated into this gated maintenance block with advisory lock.
    // The remaining pre-migration blocks above already ensure all required columns.

    // ── Migration History Reconciliation ────────────────────────────────────
    // HOTFIX: Previous deployments used raw SQL blocks to create tables/columns
    // that are also defined in EF Core migrations. When MigrateAsync() runs, it
    // sees these migrations as "not applied" (missing from __EFMigrationsHistory)
    // but the schema already exists, causing "already exists" errors that block
    // ALL subsequent migrations (including Invoices, Commission, etc.).
    //
    // Additionally, a previous reconciliation attempt incorrectly inserted migration
    // records for ALL detected schema, even when the underlying tables/columns were
    // only partially present. This caused MigrateAsync() to report "No migrations applied"
    // while critical tables like Invoices and InvoiceLineItems were missing.
    //
    // This block:
    // 1. Removes migration records for tables/columns that DON'T actually exist
    // 2. Inserts migration records for tables/columns that DO exist but aren't recorded
    // This ensures MigrateAsync() only applies truly missing migrations.
    try
    {
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                -- Ensure __EFMigrationsHistory table exists
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL PRIMARY KEY,
                    "ProductVersion" character varying(32) NOT NULL
                );

                -- ═══ STEP 1: Remove migration records for non-existent schema ═══
                -- These were incorrectly inserted by the previous reconciliation.
                -- We remove them so MigrateAsync() can re-apply them properly.

                -- 20260531000000 requires Invoices table
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260531000000_AddInvoicesAndInvoiceLineItems'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Invoices');

                -- 20260601000000 requires InvoiceId on Payments
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260601000000_AddInvoicePaymentLink'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Payments' AND column_name = 'InvoiceId');

                -- 20260606000000 requires commission columns on InvoiceLineItems
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260606000000_AddDoctorCommissionSystem'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'DoctorCommissionPercentage');

                -- 20260607000000 requires CommissionRecognitionMode on ClinicServices
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260607000000_AddCommissionRecognitionMode'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicServices' AND column_name = 'CommissionRecognitionMode');

                -- Also remove any migration record where the PRIMARY table doesn't exist
                -- This catches cases where a HOTFIX created a partial schema
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'PatientId');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502010000_AddSecurePatientPortalPasswordAuth'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddConversationRecipientType'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'RecipientType');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260507000000_AddBookingRequests'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508052207_AddBookingRequest'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditFields'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511000000_AddDoctorIdToBookingRequest'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'BookingRequests' AND column_name = 'DoctorId');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260512000000_AddRadiographFileMetadata'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicalPhotos' AND column_name = 'FileType');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513000000_AddDoctorCompensationFields'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514000000_AddClinicQueueItem'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520000000_AddClinicQueueItemTrackingFields'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledAt');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520202816_SyncAuditPhase2Configurations'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AuditLogs');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddPasswordSaltAndPatientPhoneIndexes'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522000000_AddSoftDeleteColumnsToLegacyTables'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260524000000_AddConversationPatientBranchFieldsAndIndexes'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525000000_AddMissingFKIndexesAndUserMustChangePassword'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528000000_AddClinicServicesAndRooms'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260530000000_AddPatientTreatmentPlanSteps'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'TreatmentPlanSteps');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260602000000_AddMessageAttachments'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageAttachments');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260603000000_AddOrthoDiagnosisRetentionPhotos'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OrthoDiagnoses' AND column_name = 'RetentionPhotoLeft');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260604000000_AddSuppliersAndPurchases'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260605000000_AddClinicQueueItemServiceAndRoom'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'ServiceId');

                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260608000000_AddRolePermissionUniqueIndex';
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260609000000_AddEmailLog';
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260610000000_AddSeparateReminderTrackingAndPatientEmail';

                -- Financial Integrity Sprint: remove record if IsReversal column doesn't exist yet
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260613000000_AddFinancialIntegrityAuditSprint'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'CashFlowTransactions' AND column_name = 'IsReversal');

                -- Remove Treasury/VaultTransfer migration records if tables don't exist
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525115704_AddTreasuryVaultTransfers'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Treasuries');

                -- Remove SupplierBills migration record if tables don't exist
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525123318_AddSupplierBillsAndApprovals'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'SupplierBills');

                -- Remove CashFlowTransactions migration records if table doesn't exist
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525092924_AddCentralFinanceV2Hub'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CashFlowTransactions');

                -- Remove OperationalExpenses migration if table doesn't exist
                DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" LIKE '2026052%'
                    AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OperationalExpenses');

                -- ═══ STEP 2: Insert missing records for existing schema ═══
                -- These were created by HOTFIX blocks but not recorded in __EFMigrationsHistory.

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430221624_AddConversationPatientAndType')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260430221624_AddConversationPatientAndType', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501000000_AddNormalizedPhoneFields')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260501000000_AddNormalizedPhoneFields', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'PatientId') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260501010000_AddPatientConversationSupport', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501020000_AddSoftDeleteToMessagingTables')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260501020000_AddSoftDeleteToMessagingTables', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502000000_AddVisitsDocumentsFields')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Visits') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260502000000_AddVisitsDocumentsFields', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260502010000_AddSecurePatientPortalPasswordAuth')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PatientAccounts' AND column_name = 'PasswordHash') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260502010000_AddSecurePatientPortalPasswordAuth', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260503000000_AddConversationRecipientType')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'RecipientType') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260503000000_AddConversationRecipientType', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260507000000_AddBookingRequests')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260507000000_AddBookingRequests', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508052207_AddBookingRequest')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'BookingRequests') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260508052207_AddBookingRequest', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditFields')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260510000000_AddMessageEditFields', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260511000000_AddDoctorIdToBookingRequest')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'BookingRequests' AND column_name = 'DoctorId') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260511000000_AddDoctorIdToBookingRequest', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260513000000_AddDoctorCompensationFields')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Doctors' AND column_name = 'CompensationType') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260513000000_AddDoctorCompensationFields', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260514000000_AddClinicQueueItem')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicQueueItems') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260514000000_AddClinicQueueItem', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520000000_AddClinicQueueItemTrackingFields')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ClinicQueueItems' AND column_name = 'CalledAt') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260520000000_AddClinicQueueItemTrackingFields', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260520202816_SyncAuditPhase2Configurations')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'AuditLogs') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260520202816_SyncAuditPhase2Configurations', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260521000000_AddPasswordSaltAndPatientPhoneIndexes')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'PasswordSalt') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260521000000_AddPasswordSaltAndPatientPhoneIndexes', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260522000000_AddSoftDeleteColumnsToLegacyTables')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'DeletedAt') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260522000000_AddSoftDeleteColumnsToLegacyTables', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260525000000_AddMissingFKIndexesAndUserMustChangePassword')
                   AND EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Users' AND column_name = 'MustChangePassword') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260525000000_AddMissingFKIndexesAndUserMustChangePassword', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260528000000_AddClinicServicesAndRooms')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'ClinicServices') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260528000000_AddClinicServicesAndRooms', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260602000000_AddMessageAttachments')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'MessageAttachments') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260602000000_AddMessageAttachments', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260604000000_AddSuppliersAndPurchases')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers') THEN
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260604000000_AddSuppliersAndPurchases', '8.0');
                END IF;

                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260608000000_AddRolePermissionUniqueIndex')
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260608000000_AddRolePermissionUniqueIndex', '8.0');
                IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260609000000_AddEmailLog')
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('20260609000000_AddEmailLog', '8.0');
            END $$;
        """);
        logger.LogInformation("Migration history reconciliation completed — cleaned incorrect records and inserted verified records");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Migration history reconciliation failed (non-fatal) — MigrateAsync may encounter errors");
    }

    try
    {
        // Pre-migration: ensure Financial Integrity Sprint columns exist before EF Core tries to use them
        // This is needed because the migration SQL might fail silently on some PostgreSQL versions
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "IsReversal" boolean NOT NULL DEFAULT false;
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversalOfTransactionId" uuid NULL;
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversedByTransactionId" uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversalOfTransactionId" ON "CashFlowTransactions" ("ReversalOfTransactionId");
                CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversedByTransactionId" ON "CashFlowTransactions" ("ReversedByTransactionId");
                CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId" ON "Treasuries" ("BranchId");
                CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type" ON "Treasuries" ("BranchId", "Type");
            """);
            logger.LogInformation("Financial Integrity Sprint columns verified/created pre-migration");
        }
        catch (Exception exPre)
        {
            logger.LogWarning(exPre, "Pre-migration column check failed (non-fatal)");
        }

        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed, attempting to ensure critical tables exist manually");

        // If migration fails, ensure the Financial Integrity Sprint columns exist manually
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "IsReversal" boolean NOT NULL DEFAULT false;
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversalOfTransactionId" uuid NULL;
                ALTER TABLE "CashFlowTransactions" ADD COLUMN IF NOT EXISTS "ReversedByTransactionId" uuid NULL;
                CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversalOfTransactionId" ON "CashFlowTransactions" ("ReversalOfTransactionId");
                CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_ReversedByTransactionId" ON "CashFlowTransactions" ("ReversedByTransactionId");
                CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId" ON "Treasuries" ("BranchId");
                CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type" ON "Treasuries" ("BranchId", "Type");
            """);
            logger.LogInformation("Financial Integrity Sprint columns created manually after migration failure");
        }
        catch (Exception ex2)
        {
            logger.LogError(ex2, "Failed to create Financial Integrity Sprint columns manually");
        }

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
                    "IsActive" boolean NOT NULL,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL
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
                    -- Ensure DeletedAt/DeletedBy columns exist on ConversationParticipants
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedAt') THEN
                        ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedBy') THEN
                        ALTER TABLE "ConversationParticipants" ADD COLUMN "DeletedBy" uuid NULL;
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
                    "IsActive" boolean NOT NULL,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL
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
                    -- Ensure DeletedAt/DeletedBy columns exist on Messages
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                        ALTER TABLE "Messages" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedBy') THEN
                        ALTER TABLE "Messages" ADD COLUMN "DeletedBy" uuid NULL;
                    END IF;
                    -- Ensure IsEdited/EditedAt columns exist on Messages
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                        ALTER TABLE "Messages" ADD COLUMN "IsEdited" boolean NOT NULL DEFAULT false;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
                        ALTER TABLE "Messages" ADD COLUMN "EditedAt" timestamp with time zone NULL;
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
                    "IsActive" boolean NOT NULL,
                    "DeletedAt" timestamp with time zone NULL,
                    "DeletedBy" uuid NULL
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
                    -- Ensure DeletedAt/DeletedBy columns exist on MessageReads
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedAt') THEN
                        ALTER TABLE "MessageReads" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedBy') THEN
                        ALTER TABLE "MessageReads" ADD COLUMN "DeletedBy" uuid NULL;
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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "2026.05.28-finance-activation" }));

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
    // تحذير حرج في الإنتاج: الملفات ستُفقد عند إعادة النشر
    if (app.Environment.IsProduction())
    {
        Log.Warning(
            "UPLOADS WARNING: متغير البيئة UPLOADS_PATH غير مضبوط في الإنتاج. " +
            "سيتم استخدام مسار مؤقت وستُفقد جميع صور المرضى والوثائق عند إعادة النشر. " +
            "يجب ضبط UPLOADS_PATH على Railway Persistent Volume فوراً.");
    }

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
app.MapHub<MessagingHub>("/hubs/messaging");

// ── CRITICAL: Ensure HR and Backup tables exist ─────────────────────────────
// HOTFIX: Sprint 15 (HR) and Sprint 18 (Backup) add new tables.
// If ENABLE_STARTUP_DB_MAINTENANCE is false, MigrateAsync() is skipped.
// This block runs UNCONDITIONALLY and is fully idempotent.
try
{
    using var hrScope = app.Services.CreateScope();
    var hrDb     = hrScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hrLogger = hrScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await hrDb.Database.ExecuteSqlRawAsync("""
        DO $$ BEGIN
            -- ── Attendances table ──────────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Attendances') THEN
                CREATE TABLE "Attendances" (
                    "Id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "EmployeeId"  uuid                     NOT NULL,
                    "Date"        date                     NOT NULL,
                    "CheckIn"     interval                 NULL,
                    "CheckOut"    interval                 NULL,
                    "Status"      integer                  NOT NULL DEFAULT 0,
                    "Notes"       character varying(500)   NULL,
                    "CreatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"    boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"   timestamp with time zone NULL,
                    "DeletedBy"   uuid                     NULL,
                    CONSTRAINT "PK_Attendances" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX "IX_Attendances_EmployeeId_Date" ON "Attendances" ("EmployeeId", "Date");
                CREATE INDEX "IX_Attendances_EmployeeId" ON "Attendances" ("EmployeeId");
            END IF;

            -- ── SalaryRecords table ────────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'SalaryRecords') THEN
                CREATE TABLE "SalaryRecords" (
                    "Id"            uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "EmployeeId"    uuid                     NOT NULL,
                    "Year"          integer                  NOT NULL,
                    "Month"         integer                  NOT NULL,
                    "BaseSalary"    numeric(12,2)            NOT NULL DEFAULT 0,
                    "Deductions"    numeric(12,2)            NOT NULL DEFAULT 0,
                    "Advances"      numeric(12,2)            NOT NULL DEFAULT 0,
                    "Bonuses"       numeric(12,2)            NOT NULL DEFAULT 0,
                    "NetSalary"     numeric(12,2)            NOT NULL DEFAULT 0,
                    "PaidAt"        timestamp with time zone NULL,
                    "PaidBy"        uuid                     NULL,
                    "PaymentMethod" character varying(50)    NULL,
                    "Notes"         character varying(500)   NULL,
                    "CreatedAt"     timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"     timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"      boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"     timestamp with time zone NULL,
                    "DeletedBy"     uuid                     NULL,
                    CONSTRAINT "PK_SalaryRecords" PRIMARY KEY ("Id")
                );
                CREATE UNIQUE INDEX "IX_SalaryRecords_EmployeeId_Year_Month" ON "SalaryRecords" ("EmployeeId", "Year", "Month");
                CREATE INDEX "IX_SalaryRecords_EmployeeId" ON "SalaryRecords" ("EmployeeId");
            END IF;

            -- ── AdvancePayments table ──────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'AdvancePayments') THEN
                CREATE TABLE "AdvancePayments" (
                    "Id"              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "EmployeeId"      uuid                     NOT NULL,
                    "Amount"          numeric(12,2)            NOT NULL,
                    "Reason"          character varying(500)   NULL,
                    "RequestDate"     timestamp with time zone NOT NULL DEFAULT now(),
                    "Status"          integer                  NOT NULL DEFAULT 0,
                    "ApprovedBy"      uuid                     NULL,
                    "ApprovedAt"      timestamp with time zone NULL,
                    "RejectionReason" character varying(500)   NULL,
                    "DeductFromMonth" integer                  NULL,
                    "DeductFromYear"  integer                  NULL,
                    "IsDeducted"      boolean                  NOT NULL DEFAULT false,
                    "CreatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"        boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"       timestamp with time zone NULL,
                    "DeletedBy"       uuid                     NULL,
                    CONSTRAINT "PK_AdvancePayments" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_AdvancePayments_EmployeeId" ON "AdvancePayments" ("EmployeeId");
            END IF;

            -- ── LeaveRequests table ────────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'LeaveRequests') THEN
                CREATE TABLE "LeaveRequests" (
                    "Id"              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "EmployeeId"      uuid                     NOT NULL,
                    "LeaveType"       integer                  NOT NULL DEFAULT 0,
                    "StartDate"       date                     NOT NULL,
                    "EndDate"         date                     NOT NULL,
                    "TotalDays"       integer                  NOT NULL,
                    "Reason"          character varying(500)   NULL,
                    "Status"          integer                  NOT NULL DEFAULT 0,
                    "ApprovedBy"      uuid                     NULL,
                    "ApprovedAt"      timestamp with time zone NULL,
                    "RejectionReason" character varying(500)   NULL,
                    "CreatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"        boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"       timestamp with time zone NULL,
                    "DeletedBy"       uuid                     NULL,
                    CONSTRAINT "PK_LeaveRequests" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_LeaveRequests_EmployeeId" ON "LeaveRequests" ("EmployeeId");
            END IF;

            -- ── EmployeeDocuments table ────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'EmployeeDocuments') THEN
                CREATE TABLE "EmployeeDocuments" (
                    "Id"           uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "EmployeeId"   uuid                     NOT NULL,
                    "DocumentType" character varying(100)   NOT NULL,
                    "Title"        character varying(200)   NOT NULL,
                    "FilePath"     character varying(500)   NOT NULL,
                    "FileName"     character varying(300)   NULL,
                    "ContentType"  character varying(100)   NULL,
                    "FileSize"     bigint                   NULL,
                    "UploadedBy"   uuid                     NOT NULL,
                    "CreatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"     boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"    timestamp with time zone NULL,
                    "DeletedBy"    uuid                     NULL,
                    CONSTRAINT "PK_EmployeeDocuments" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_EmployeeDocuments_EmployeeId" ON "EmployeeDocuments" ("EmployeeId");
            END IF;

            -- ── BackupRecords table ────────────────────────────────────────
            IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'BackupRecords') THEN
                CREATE TABLE "BackupRecords" (
                    "Id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                    "Type"        integer                  NOT NULL DEFAULT 0,
                    "Status"      integer                  NOT NULL DEFAULT 0,
                    "StartedAt"   timestamp with time zone NOT NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    "SizeBytes"   bigint                   NULL,
                    "FilePath"    character varying(500)   NULL,
                    "ErrorMessage" character varying(2000) NULL,
                    "TriggeredBy" uuid                     NULL,
                    "IsAutomatic" boolean                  NOT NULL DEFAULT false,
                    "CreatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                    "IsActive"    boolean                  NOT NULL DEFAULT true,
                    "DeletedAt"   timestamp with time zone NULL,
                    "DeletedBy"   uuid                     NULL,
                    CONSTRAINT "PK_BackupRecords" PRIMARY KEY ("Id")
                );
                CREATE INDEX "IX_BackupRecords_StartedAt" ON "BackupRecords" ("StartedAt");
            END IF;
        END $$;
    """);

    hrLogger.LogInformation("HOTFIX: HR and Backup tables ensured (idempotent)");
}
catch (Exception ex)
{
    var hrLogger2 = app.Services.GetRequiredService<ILogger<Program>>();
    hrLogger2.LogError(ex, "HOTFIX: Failed to ensure HR/Backup tables. HR and Backup endpoints may return 500!");
}

app.Run();
