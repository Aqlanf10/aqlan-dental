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
builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", policy =>
    policy.WithOrigins(
            builder.Configuration["AllowedOrigins"]?.Split(',') ?? ["http://localhost:3000", "http://localhost:3001"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

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
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, logger);
}

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Serve uploaded files
var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aqlan Dental Pro v1"));

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLogMiddleware>();
app.MapControllers();

app.Run();
