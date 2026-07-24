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

    // SEC-14: Reject known-bad JWT secrets that bypassed the original CHANGE_ME + length check.
    // docker-compose.yml defaults to "dev_secret_key_change_in_production_must_be_long"
    // (50 chars, no "CHANGE_ME") which passed the old guard and let the app boot with a
    // publicly-known signing key. .env.example uses lowercase "change_me_at_least_64_chars"
    // which also bypassed the old case-sensitive check. Comparison is now case-insensitive.
    var knownBadJwtSecrets = new[]
    {
        "CHANGE_ME",                                        // appsettings.json / .env.example placeholder
        "dev_secret_key_change_in_production_must_be_long", // docker-compose.yml default
    };
    var badJwt = knownBadJwtSecrets.Any(b => prodJwtKey.Contains(b, StringComparison.OrdinalIgnoreCase));
    if (badJwt || prodJwtKey.Length < 32)
        throw new InvalidOperationException(
            "SEC: مفتاح JWT غير آمن في الإنتاج. يجب ضبط Jwt:SecretKey بقيمة عشوائية لا تقل عن 32 حرفاً ولا تطابق أي قيمة افتراضية معروفة.");

    var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";

    // SEC-14: Reject known-bad DB passwords. docker-compose.yml defaults to
    // "aqlan_dev_password" and .env.example to "change_me_strong_password" — both
    // bypassed the old case-sensitive CHANGE_ME check.
    var knownBadDbPasswords = new[]
    {
        "CHANGE_ME",                 // appsettings.json placeholder
        "aqlan_dev_password",        // docker-compose.yml default
        "change_me_strong_password", // .env.example placeholder
    };
    var badConn = knownBadDbPasswords.Any(b => connStr.Contains(b, StringComparison.OrdinalIgnoreCase));
    if (badConn)
        throw new InvalidOperationException(
            "SEC: كلمة مرور قاعدة البيانات لم تُضبط في الإنتاج. يجب ضبط ConnectionStrings:DefaultConnection بقيمة حقيقية.");
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
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, version = "2026.06.12-lab-pdf-fix-v2" }));

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

// SEC-03 FIX: In Production, gate /uploads/* behind authentication. This middleware MUST run
// BEFORE UseStaticFiles (below) because UseStaticFiles short-circuits the pipeline and serves
// the file directly. Without this guard, anyone with a URL can read clinical photos, X-rays,
// and patient documents (PHI exposure). The guard validates the JWT from:
//   1. Authorization: Bearer <token> header (API clients)
//   2. aqlan_access_token cookie (set by the frontend authStore — lets <img src> work without
//      manual headers)
//   3. ?token=<token> query param (fallback for special cases)
// In Development, the guard is skipped so local <img src="/uploads/..."> works without auth.
if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/uploads"))
        {
            var authService = context.RequestServices.GetService<Microsoft.AspNetCore.Authentication.IAuthenticationService>();
            if (authService is null)
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("Auth service unavailable");
                return;
            }

            // SEC-03: If a Bearer token is present in the Authorization header OR in the
            // aqlan_access_token cookie, inject it into the Authorization header so the JWT
            // bearer handler can authenticate. This lets <img src="/uploads/..."> work because
            // browsers send cookies automatically on same-origin requests.
            if (string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString()))
            {
                var cookieToken = context.Request.Cookies["aqlan_access_token"];
                if (!string.IsNullOrEmpty(cookieToken))
                    context.Request.Headers.Authorization = $"Bearer {cookieToken}";
                else
                {
                    var queryToken = context.Request.Query["token"].ToString();
                    if (!string.IsNullOrEmpty(queryToken))
                        context.Request.Headers.Authorization = $"Bearer {queryToken}";
                }
            }

            if (string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString()))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }

            var authResult = await authService.AuthenticateAsync(context, Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
            if (!authResult.Succeeded)
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }
        await next();
    });
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
app.UseMiddleware<QueueDisplayAuthenticationMiddleware>();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseMiddleware<AuditLogMiddleware>();
app.MapControllers();
app.MapHub<MessagingHub>("/hubs/messaging");

app.Run();

// TEST-18: Expose Program as a public partial class so that
// WebApplicationFactory<Program> in the IntegrationTests project can reference it.
// Top-level statements generate an internal Program by default, which is
// inaccessible from a separate assembly. This declaration merges with the
// synthesized Program class without changing runtime behavior.
public partial class Program { }
