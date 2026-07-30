using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// WebApplicationFactory for the Aqlan Dental Pro API backed by a real PostgreSQL
/// instance spun up via Testcontainers. The factory:
///
///   * Starts a throwaway <c>postgres:16-alpine</c> container in <see cref="InitializeAsync"/>.
///   * Overrides the connection string, JWT secret, and Redis config so the app boots
///     cleanly under the <c>Testing</c> environment (avoiding the Production fail-fast
///     guard and the <c>UPLOADS_PATH</c> warning).
///   * Strips the three background <see cref="IHostedService"/> jobs
///     (<see cref="OverdueNotificationJob"/>, <see cref="AppointmentReminderJob"/>,
///     <see cref="AutoBackupJob"/>) from the DI container so tests don't fire
///     reminders, overdue notifications, or backups while the test host is running.
///   * Applies EF Core migrations against the container in <see cref="InitializeAsync"/>
///     and exposes <see cref="ResetDatabaseAsync"/> so each test class can start from
///     a clean schema.
///
/// The factory also exposes <see cref="GenerateJwt"/> / <see cref="CreateAuthenticatedClient"/>
/// helpers so tests can mint valid JWTs (signed with the same test secret wired into
/// the app's JwtBearer configuration) without going through the login endpoint.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// Test JWT signing key — must be ≥ 32 chars and must NOT match any of the
    /// known-bad secrets rejected by <c>Program.cs</c>'s Production fail-fast
    /// guard (<c>CHANGE_ME</c>, <c>dev_secret_key_change_in_production_must_be_long</c>,
    /// <c>change_me_at_least_64_chars</c>). We are running under <c>Testing</c>
    /// so the fail-fast never fires, but we keep the secret honest regardless.
    /// </summary>
    public const string TestJwtSecret =
        "TEST-18_integration_test_jwt_secret_0123456789_!@#$%^&*()";

    public const string TestJwtIssuer = "AqlanDentalPro";
    public const string TestJwtAudience = "AqlanDentalProClient";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("aqlan_integration_tests")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    /// <summary>
    /// Connection string of the running PostgreSQL container. Available after
    /// <see cref="InitializeAsync"/> completes; <see cref="ConfigureWebHost"/>
    /// reads this when the host is built.
    /// </summary>
    public string ConnectionString => _dbContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use "Testing" environment so the Production fail-fast (JWT/DB password)
        // is skipped and the UPLOADS_PATH warning is not logged. Not Development
        // either — Development would still trigger Swagger and other dev-only
        // behavior that isn't relevant for these tests.
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["Jwt:SecretKey"] = TestJwtSecret,
                ["Jwt:Issuer"] = TestJwtIssuer,
                ["Jwt:Audience"] = TestJwtAudience,
                ["Jwt:AccessTokenExpiryMinutes"] = "60",
                ["Jwt:RefreshTokenExpiryDays"] = "1",
                // Redis is required by the DI container (TokenService/LoginAttemptService)
                // but degrades gracefully when unavailable. Pointing it at localhost
                // keeps the connection multiplexer happy without spinning up Redis.
                ["Redis:ConnectionString"] = "localhost:6379",
                ["AllowedOrigins"] = "http://localhost",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // ── Disable background hosted services ────────────────────────────────
            // These fire reminders, overdue notifications, and backups on a schedule.
            // In tests they would pollute the database with side effects, slow the
            // test run, and (in the case of AutoBackupJob) try to write to disk.
            // We remove their IHostedService registrations rather than the concrete
            // service registrations so the rest of the DI graph stays intact.
            RemoveHostedService<OverdueNotificationJob>(services);
            RemoveHostedService<AppointmentReminderJob>(services);
            RemoveHostedService<AutoBackupJob>(services);
        });
    }

    /// <summary>
    /// Removes the <c>IHostedService</c> registration whose implementation type
    /// matches <typeparamref name="T"/>. Background jobs in this codebase are
    /// registered via <c>AddHostedService&lt;T&gt;</c> which adds them under the
    /// <see cref="IHostedService"/> service type with
    /// <see cref="ServiceLifetime.Singleton"/> and the implementation type set.
    /// </summary>
    private static void RemoveHostedService<T>(IServiceCollection services) where T : class
    {
        var descriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(T))
            .ToList();
        foreach (var d in descriptors)
            services.Remove(d);
    }

    // ── IAsyncLifetime ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called by xUnit when the factory is first instantiated as a fixture.
    /// Starts the PostgreSQL container, then triggers host creation by accessing
    /// <see cref="WebApplicationFactory{TEntryPoint}.Services"/> — which in turn
    /// runs <c>Program.cs</c>'s startup maintenance (including the fresh-DB EF
    /// migration bootstrap). Finally, explicitly call <c>MigrateAsync</c> to be
    /// certain the schema is fully up to date before any test runs.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Force the host to build now (lazily built on first access) so that
        // Program.cs startup maintenance runs against the live container and
        // the DI container is ready for the tests.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // CORE-CI-001: build the schema from the EF MODEL, never from the migration chain.
        //
        // What used to be here was `await db.Database.MigrateAsync()`, described as an
        // idempotent no-op on the assumption that startup maintenance had already prepared
        // the database. Neither half held. StartupDatabaseMaintenance wraps every step in
        // try/catch and logs warnings, so when it fails it fails silently — and MigrateAsync
        // was therefore the ONLY thing attempting to create the schema. It cannot: CLAUDE.md
        // records that 31 hand-written migrations carry no [Migration] attribute, so the chain
        // is unreplayable on an empty database. It threw in this fixture, and all 24
        // integration tests died before their first assertion. The CI job reported success
        // anyway because the step carried continue-on-error, so this went unnoticed.
        //
        // EnsureCreated is the right tool for a throwaway container: it creates the full
        // schema from the current EF model and records no migration history — which is exactly
        // what StartupDatabaseMaintenance does for a genuinely empty production database. It
        // also means the schema under test always matches the model, so a drift between the
        // two surfaces here rather than in production.
        await db.Database.EnsureCreatedAsync();

        // Verify rather than assume. If the schema is still absent, every test below would
        // fail with some unrelated-looking error; failing here says plainly what went wrong.
        await db.Database.OpenConnectionAsync();
        bool usersTableExists;
        using (var checkCmd = db.Database.GetDbConnection().CreateCommand())
        {
            checkCmd.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users')";
            usersTableExists = await checkCmd.ExecuteScalarAsync() is bool b && b;
        }

        if (!usersTableExists)
            throw new InvalidOperationException(
                "Integration test database has no schema after EnsureCreated — no test can be trusted.");
    }

    /// <summary>
    /// xUnit calls <see cref="IAsyncLifetime.DisposeAsync"/> when the fixture is
    /// torn down. We stop + dispose the Testcontainer and then hand off to the
    /// base factory to dispose the in-memory test server.
    /// </summary>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    // ── Test helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Drops and re-creates the database schema by deleting the database and
    /// re-applying all EF Core migrations. Call from each test class's
    /// <c>IAsyncLifetime.InitializeAsync</c> so tests start from a clean slate.
    /// </summary>
    /// <remarks>
    /// <see cref="DatabaseFacade.EnsureDeletedAsync"/> + <see cref="MigrateAsync"/>
    /// is preferred over <see cref="DatabaseFacade.EnsureCreatedAsync"/> because
    /// the latter bypasses the migration history and would diverge from the real
    /// production schema (no <c>__EFMigrationsHistory</c> table, different index
    /// names, etc.).
    /// </remarks>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// Mints a JWT signed with <see cref="TestJwtSecret"/> carrying the same
    /// claim layout that <see cref="TokenService.GenerateAccessToken"/> produces
    /// in production. Tests use this instead of hitting <c>POST /api/auth/login</c>
    /// because we already know the test user's identity and don't want to fight
    /// Argon2 password hashing or rate limiting.
    /// </summary>
    public string GenerateJwt(
        Guid userId,
        string username,
        UserRole role,
        Guid? branchId = null,
        bool mustChangePassword = false)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(1);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(ClaimTypes.Role, role.ToString()),
            new("branchId", branchId?.ToString() ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("mustChangePassword", mustChangePassword.ToString().ToLowerInvariant()),
        };

        var token = new JwtSecurityToken(
            issuer: TestJwtIssuer,
            audience: TestJwtAudience,
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Returns an <see cref="HttpClient"/> that automatically attaches the
    /// Authorization: Bearer header for the given user. Convenience wrapper
    /// around <see cref="GenerateJwt"/>.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        Guid userId,
        string username,
        UserRole role,
        Guid? branchId = null)
    {
        var client = CreateClient();
        var token = GenerateJwt(userId, username, role, branchId);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Returns a scoped <see cref="AppDbContext"/> against the Testcontainer
    /// PostgreSQL instance. Tests use this to seed data directly without going
    /// through the API.
    /// </summary>
    public async Task<AppDbContext> CreateDbContextAsync()
    {
        var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Task.CompletedTask;
        return db;
    }
}
