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

        await BuildSchemaFromModelAsync(db);
    }

    /// <summary>
    /// CORE-CI-001 — builds the test schema the way production builds a fresh one.
    /// <para>
    /// This used to be <c>MigrateAsync()</c>, described as an idempotent no-op on the
    /// assumption that startup maintenance had already prepared the database. Neither half
    /// held. <c>StartupDatabaseMaintenance</c> wraps every step in try/catch and logs a
    /// warning, so when it fails it fails silently — leaving <c>MigrateAsync</c> as the only
    /// thing actually creating the schema. And it cannot: CLAUDE.md records that 31
    /// hand-written migrations carry no <c>[Migration]</c> attribute, so the chain is
    /// unreplayable on an empty database. It threw here, and all 32 integration tests died
    /// before their first assertion — while the CI job reported success, because the step
    /// carried continue-on-error.
    /// </para>
    /// <para>
    /// The replacement is not a shortcut: <c>EnsureFreshDatabaseMigratedAsync</c> does exactly
    /// this for a genuinely empty production database — schema from the EF model, then the
    /// discoverable migrations stamped as applied. So the schema under test is produced the
    /// same way production's is, and a drift between model and database surfaces here.
    /// </para>
    /// </summary>
    private static async Task BuildSchemaFromModelAsync(AppDbContext db)
    {
        await db.Database.OpenConnectionAsync();

        // Raw ADO, not ExecuteSqlRawAsync: that overload runs the statement through
        // String.Format to bind parameters, and a generated schema contains braces (defaults,
        // jsonb literals) which are then read as format placeholders and throw.
        var createScript = db.Database.GenerateCreateScript();
        using (var createCmd = db.Database.GetDbConnection().CreateCommand())
        {
            createCmd.CommandText = createScript;
            createCmd.CommandTimeout = 600;
            await createCmd.ExecuteNonQueryAsync();
        }

        // Stamp the history exactly as the production baseline does, so anything that later
        // consults it does not try to replay the unreplayable chain on top.
        using (var historyCmd = db.Database.GetDbConnection().CreateCommand())
        {
            historyCmd.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """;
            await historyCmd.ExecuteNonQueryAsync();
        }

        var productVersion = Microsoft.EntityFrameworkCore.Infrastructure.ProductInfo.GetVersion();
        foreach (var migrationId in db.Database.GetMigrations())
        {
            using var stampCmd = db.Database.GetDbConnection().CreateCommand();
            stampCmd.CommandText =
                """INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES (@id, @ver) ON CONFLICT DO NOTHING""";
            var idParam = stampCmd.CreateParameter(); idParam.ParameterName = "id"; idParam.Value = migrationId;
            var verParam = stampCmd.CreateParameter(); verParam.ParameterName = "ver"; verParam.Value = productVersion;
            stampCmd.Parameters.Add(idParam);
            stampCmd.Parameters.Add(verParam);
            await stampCmd.ExecuteNonQueryAsync();
        }

        // Verify rather than assume. Without this, a future regression in schema creation
        // would surface as 32 tests failing on unrelated-looking symptoms.
        using var checkCmd = db.Database.GetDbConnection().CreateCommand();
        checkCmd.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users')";
        if (await checkCmd.ExecuteScalarAsync() is not bool ok || !ok)
            throw new InvalidOperationException(
                "Integration test database has no schema after the create script ran — no test can be trusted.");
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
    /// CORE-CI-001: rebuilt from the EF model rather than by replaying migrations. The old
    /// comment here argued that MigrateAsync stays closer to "the real production schema" —
    /// but for an EMPTY database production does not replay the chain either. It cannot: 31
    /// hand-written migrations carry no [Migration] attribute. <c>BuildSchemaFromModelAsync</c>
    /// mirrors the production baseline, history stamp included, so the two agree.
    /// </remarks>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await BuildSchemaFromModelAsync(db);
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
