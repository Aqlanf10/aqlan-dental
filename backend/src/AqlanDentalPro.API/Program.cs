using AqlanDentalPro.API.Configuration;
using AqlanDentalPro.API.Hubs;
using AqlanDentalPro.API.Middleware;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

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

// ── Redis ─────────────────────────────────────────────────────────────────
builder.Services.AddRedisConfiguration(builder.Configuration);

// ── JWT Authentication ────────────────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);

// ── Role-Based Authorization Policies ─────────────────────────────────────────
builder.Services.AddAuthorizationPolicies();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCorsConfiguration(builder.Configuration);

// ── Rate Limiting (H1 FIX: prevent brute-force on auth endpoints) ────────────
builder.Services.AddRateLimiterConfiguration();

// ── DI — Repositories & Services ─────────────────────────────────────────────
builder.Services.AddApplicationServices();

// ── FluentValidation ──────────────────────────────────────────────────────────
builder.Services.AddFluentValidationConfiguration();

// ── Static Files + Controllers + Swagger ─────────────────────────────────────
builder.Services.AddControllersConfiguration();
builder.WebHost.ConfigureKestrel(opts =>
{
    opts.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

var app = builder.Build();

// ── Startup Database Maintenance ──────────────────────────────────────────
// All startup DB schema hotfixes, gated maintenance, and table creation
// extracted to Configuration/StartupDatabaseMaintenance.cs for clarity.
await app.RunStartupDatabaseMaintenanceAsync(builder.Configuration);

// ── Middleware Pipeline ───────────────────────────────────────────────────────
app.UseSecurityHeaders();
app.UseMiddleware<ErrorHandlingMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "2026.05.28-finance-live" }));

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

app.Run();
