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
// Support Railway's DATABASE_URL format (postgresql://user:pass@host:port/db)
// as well as the standard ConnectionStrings__DefaultConnection format.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"] switch
    {
        string dbUrl => ConvertRailwayUrlToNpgsql(dbUrl),
        null => null
    }
    ?? throw new InvalidOperationException("No database connection string found. Set ConnectionStrings__DefaultConnection or DATABASE_URL.");

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseNpgsql(connectionString,
        npgsql => npgsql.MigrationsAssembly("AqlanDentalPro.Infrastructure")));

static string ConvertRailwayUrlToNpgsql(string url)
{
    // Railway provides: postgresql://user:password@host:port/database
    // Npgsql needs:     Host=host;Port=port;Database=database;Username=user;Password=password
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':');
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.Trim('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=true";
}

// ── Redis ─────────────────────────────────────────────────────────────────────
// Support Railway's REDIS_URL format (redis://default:password@host:port)
var redisConnStr = builder.Configuration["Redis:ConnectionString"]
    ?? builder.Configuration["REDIS_URL"] switch
    {
        string rUrl => ConvertRailwayRedisUrl(rUrl),
        null => "localhost:6379"
    };

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnStr));

static string ConvertRailwayRedisUrl(string url)
{
    // Railway provides: redis://default:password@host:port
    // StackExchange.Redis needs: host:port,password=password
    var uri = new Uri(url);
    var password = uri.UserInfo.Contains(':') ? uri.UserInfo.Split(':')[1] : "";
    return $"{uri.Host}:{uri.Port},password={password},ssl=True,abortConnect=False";
}

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
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(opts => opts.AddPolicy("AllowFrontend", policy =>
    policy.WithOrigins(
            builder.Configuration["AllowedOrigins"]?.Split(',') ?? ["http://localhost:3000"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// ── DI — Repositories ────────────────────────────────────────────────────────
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();

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
builder.Services.AddScoped<CephService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddSingleton<ICephLandmarkDetector, CephLandmarkDetector>();

builder.Services.AddHttpContextAccessor();

// ── FluentValidation ──────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>(); // Application validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();               // API-level validators

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

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aqlan Dental Pro v1"));

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");
app.UseStaticFiles();   // Serve wwwroot/uploads
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuditLogMiddleware>();
app.MapControllers();

app.Run();
