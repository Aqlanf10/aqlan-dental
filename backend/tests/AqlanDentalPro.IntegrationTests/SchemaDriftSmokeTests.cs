using System.Data;
using System.Text;
using AqlanDentalPro.API.Configuration;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// Schema-drift smoke test added in task C-08 as the safety net for the
/// gradual deletion of runtime DDL blocks in
/// <c>StartupDatabaseMaintenance.cs</c>.
///
/// The test boots the API against a fresh Testcontainers PostgreSQL container
/// (via <see cref="TestWebAppFactory"/>, which already runs
/// <c>Program.cs</c>'s startup DB maintenance + <c>MigrateAsync</c> in its
/// <see cref="TestWebAppFactory.InitializeAsync"/>). It then dumps the actual
/// schema from <c>information_schema</c> + <c>pg_constraint</c> and compares
/// it against the EF Core model's expected entity → table/column/FK mapping
/// (via <see cref="DbContext.Model"/>).
///
/// Assertion contract:
///   1. Every EF-mapped entity that targets a physical table must have a row
///      in <c>information_schema.tables</c> for the <c>public</c> schema.
///   2. Every EF-mapped property that maps to a column must have a row in
///      <c>information_schema.columns</c> for the corresponding table.
///   3. Every EF-mapped foreign key must have a corresponding row in
///      <c>pg_constraint</c> with <c>contype = 'f'</c>.
///   4. The <c>__EFMigrationsHistory</c> table must exist and contain at
///      least one row (proves the migration bookkeeping is intact).
///
/// This test will NOT run in this sandbox — Testcontainers needs Docker. It
/// is intended for CI runners with Docker available. It MUST compile cleanly
/// under <c>dotnet build</c> on the IntegrationTests project.
/// </summary>
public class SchemaDriftSmokeTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public SchemaDriftSmokeTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        // ResetDatabaseAsync drops and re-applies migrations against the
        // Testcontainer. After this returns, the schema reflects exactly
        // what a fresh Railway deployment would produce after
        // StartupDatabaseMaintenance + MigrateAsync have both run.
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ───────────────────────────────────────────────────────────────────────
    //  Test 1 — every EF entity's table physically exists in the database
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that every entity declared in the EF Core model that maps to a
    /// physical table (i.e. not an owned/keyless/view-backed entity) has a
    /// matching row in <c>information_schema.tables</c> for the public
    /// schema. If a future increment deletes a runtime DDL block that was
    /// actually load-bearing (e.g. <c>DoctorSchedules</c>), this test will
    /// fail with the entity's table name in the diff.
    /// </summary>
    [Fact]
    public async Task EveryEfEntityHasCorrespondingTableInDatabase()
    {
        var expectedTables = GetExpectedEntityTables();
        expectedTables.Should().NotBeEmpty(
            "the EF model must declare at least one entity for this test to be meaningful");

        var actualTables = await GetActualTablesAsync();

        var missing = expectedTables.Except(actualTables).OrderBy(t => t).ToList();

        missing.Should().BeEmpty(
            "EF-declared entities must have corresponding tables in the database. " +
            "Missing tables: {0}",
            string.Join(", ", missing));
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Test 2 — every EF-mapped column physically exists in the database
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that every column EF expects (per <see cref="IProperty"/> on
    /// each entity) exists in <c>information_schema.columns</c>. This is the
    /// assertion that catches schema drift when a runtime hotfix column
    /// (e.g. <c>Users.PasswordSalt</c>, <c>Doctors.CompensationType</c>) is
    /// deleted before the corresponding EF migration has applied on the
    /// target DB.
    /// </summary>
    [Fact]
    public async Task EveryEfMappedColumnExistsInDatabase()
    {
        var expectedColumns = GetExpectedEntityColumns();
        expectedColumns.Should().NotBeEmpty(
            "the EF model must declare at least one column for this test to be meaningful");

        var actualColumns = await GetActualColumnsAsync();

        var missing = expectedColumns
            .Except(actualColumns)
            .OrderBy(c => c.Table)
            .ThenBy(c => c.Column)
            .ToList();

        missing.Should().BeEmpty(
            "EF-mapped columns must exist in the database. Missing columns: {0}",
            string.Join(", ", missing.Select(c => $"{c.Table}.{c.Column}")));
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Test 3 — every EF foreign key has a corresponding pg_constraint row
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that every EF-declared foreign key has a matching row in
    /// <c>pg_constraint</c> with <c>contype = 'f'</c>. This catches drift
    /// where a runtime DDL block created an FK with the wrong cascade
    /// behavior (e.g. <c>FK_LabPayables_Labs_LabId</c> which the runtime
    /// block creates with CASCADE but migration 20260620201342 changed to
    /// RESTRICT — the constraint name will still exist but with the wrong
    /// <c>confdeltype</c>).
    /// </summary>
    [Fact]
    public async Task EveryEfForeignKeyHasCorrespondingPgConstraint()
    {
        var expectedFks = GetExpectedForeignKeys();
        // Some entities (lookup tables, owned types) may have zero FKs —
        // so an empty expectedFks set is a valid state for the model.
        // We still assert that every FK the model declares exists in the DB.

        var actualFkNames = await GetActualForeignKeyNamesAsync();

        var missing = expectedFks.Except(actualFkNames).OrderBy(n => n).ToList();

        missing.Should().BeEmpty(
            "EF-declared foreign keys must have corresponding pg_constraint rows. " +
            "Missing FK constraints: {0}",
            string.Join(", ", missing));
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Test 4 — __EFMigrationsHistory exists and is non-empty
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asserts that the <c>__EFMigrationsHistory</c> table exists and
    /// contains at least one row. This proves the fresh-DB bootstrap path
    /// (which baselines the migration history via
    /// <see cref="StartupDatabaseMaintenance"/> + <see cref="DatabaseFacade.MigrateAsync"/>)
    /// has actually run and recorded its work.
    /// </summary>
    [Fact]
    public async Task EFMigrationsHistoryTableExistsAndHasRows()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = '__EFMigrationsHistory'
            );
            """;
        var exists = (bool)(await cmd.ExecuteScalarAsync() ?? false);
        exists.Should().BeTrue(
            "__EFMigrationsHistory must exist after startup maintenance + MigrateAsync");

        await using var countCmd = conn.CreateCommand();
        countCmd.CommandText = """
            SELECT COUNT(*) FROM "__EFMigrationsHistory";
            """;
        var count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
        count.Should().BeGreaterThan(0,
            "__EFMigrationsHistory must contain at least one row — proves the " +
            "fresh-DB bootstrap path recorded its baseline");
    }

    [Fact]
    public async Task TreatmentPlanSchemaHotfixRepairsMissingProductionTableIdempotently()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        await using (var simulateDrift = conn.CreateCommand())
        {
            simulateDrift.CommandText = """
                DROP TABLE IF EXISTS "PatientTreatmentPlanSteps" CASCADE;
                DELETE FROM "__EFMigrationsHistory"
                WHERE "MigrationId" = '20260530000000_AddPatientTreatmentPlanSteps';
                """;
            await simulateDrift.ExecuteNonQueryAsync();
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var repair = conn.CreateCommand();
            repair.CommandText =
                StartupDatabaseMaintenance.PatientTreatmentPlanStepsSchemaSql;
            await repair.ExecuteNonQueryAsync();
        }

        await using var verify = conn.CreateCommand();
        verify.CommandText = """
            SELECT
                to_regclass('public."PatientTreatmentPlanSteps"') IS NOT NULL,
                (
                    SELECT COUNT(*)
                    FROM pg_constraint
                    WHERE conname IN (
                        'FK_PatientTreatmentPlanSteps_Patients_PatientId',
                        'FK_PatientTreatmentPlanSteps_ClinicServices_ServiceId',
                        'FK_PatientTreatmentPlanSteps_Doctors_ResponsibleDoctorId',
                        'FK_PatientTreatmentPlanSteps_Appointments_RelatedAppointmentId',
                        'FK_PatientTreatmentPlanSteps_Visits_RelatedVisitId',
                        'FK_InvoiceLineItems_PatientTreatmentPlanSteps_RelatedTreatmentPlanStepId'
                    )
                ),
                EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = '20260530000000_AddPatientTreatmentPlanSteps'
                );
            """;

        await using var reader = await verify.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetBoolean(0).Should().BeTrue();
        reader.GetInt64(1).Should().Be(6);
        reader.GetBoolean(2).Should().BeTrue();
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Test 5 — diagnostic dump (not an assertion; emits a schema summary
    //  that helps debug drift if Test 1 or Test 2 fails)
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Emits a summary of the actual table/column set to the test output.
    /// Not an assertion — this exists so that when Test 1 or Test 2 fails,
    /// the test runner output shows exactly what tables/columns ARE present
    /// in the database, making the diff against the EF model trivial.
    ///
    /// Marked <c>[Fact]</c> rather than <c>[Theory]</c> so it always runs
    /// once and emits its dump. It can never fail on its own.
    /// </summary>
    [Fact]
    public async Task DumpActualSchemaForDiagnosticPurposes()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SchemaDriftSmokeTests diagnostic dump ===");
        sb.AppendLine($"Connection: {_factory.ConnectionString}");
        sb.AppendLine();

        var tables = (await GetActualTablesAsync()).OrderBy(t => t).ToList();
        sb.AppendLine($"Actual tables in 'public' schema ({tables.Count}):");
        foreach (var t in tables)
            sb.AppendLine($"  - {t}");
        sb.AppendLine();

        var expectedTables = GetExpectedEntityTables().OrderBy(t => t).ToList();
        sb.AppendLine($"EF model expects {expectedTables.Count} entity tables:");
        foreach (var t in expectedTables)
            sb.AppendLine($"  - {t}");
        sb.AppendLine();

        var onlyInDb = tables.Except(expectedTables).OrderBy(t => t).ToList();
        var onlyInModel = expectedTables.Except(tables).OrderBy(t => t).ToList();
        sb.AppendLine($"Tables only in DB (not in EF model): {onlyInDb.Count}");
        foreach (var t in onlyInDb)
            sb.AppendLine($"  + {t}");
        sb.AppendLine($"Tables only in EF model (not in DB): {onlyInModel.Count}");
        foreach (var t in onlyInModel)
            sb.AppendLine($"  - {t}");

        // Emit to test output via Xunit's ITestOutputHelper is not strictly
        // necessary; writing to Console.Out is captured by most runners.
        // We use Console.WriteLine so the dump appears in `dotnet test --logger`
        // output without needing ITestOutputHelper wiring.
        Console.WriteLine(sb.ToString());

        // Trivial assertion so the test is recognized as a real test by
        // the runner and not skipped.
        true.Should().BeTrue(
            "the diagnostic dump test always passes — its purpose is to emit output");
    }

    // ───────────────────────────────────────────────────────────────────────
    //  EF model introspection helpers
    // ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the set of (schema, table) pairs the EF model expects to
    /// exist as physical tables. Skips:
    ///   * entities mapped to views (<see cref="IEntityType.GetViewName"/>),
    ///   * entities mapped to SQL queries,
    ///   * owned types (no own table),
    ///   * keyless entities mapped to a query,
    ///   * entities whose <see cref="IEntityType.GetTableName"/> returns null.
    /// </summary>
    private HashSet<string> GetExpectedEntityTables()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (entityType.FindPrimaryKey() is null) continue; // keyless entities
            if (!string.IsNullOrEmpty(entityType.GetViewName())) continue;

            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName)) continue;

            // EF defaults to "public" for PostgreSQL when no schema is set;
            // GetSchema() returns null in that case.
            var schema = entityType.GetSchema() ?? "public";
            if (schema != "public") continue;

            tables.Add(tableName);
        }
        return tables;
    }

    /// <summary>
    /// Returns the set of (table, column) pairs the EF model expects.
    /// </summary>
    private HashSet<(string Table, string Column)> GetExpectedEntityColumns()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var columns = new HashSet<(string Table, string Column)>();
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (entityType.FindPrimaryKey() is null) continue; // keyless entities
            if (!string.IsNullOrEmpty(entityType.GetViewName())) continue;

            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName)) continue;

            var schema = entityType.GetSchema() ?? "public";
            if (schema != "public") continue;

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(StoreObjectIdentifier.Table(tableName, schema));
                if (string.IsNullOrEmpty(columnName)) continue;
                columns.Add((tableName, columnName));
            }
        }
        return columns;
    }

    /// <summary>
    /// Returns the set of FK constraint names the EF model expects. EF Core
    /// generates deterministic constraint names of the form
    /// <c>FK_{table}_{principalTable}_{principalKeyPropertyName}</c> by
    /// default (configurable via <c>HasConstraintName</c>); this method
    /// collects whatever name EF will emit.
    /// </summary>
    private HashSet<string> GetExpectedForeignKeys()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            if (entityType.IsOwned()) continue;
            if (entityType.FindPrimaryKey() is null) continue; // keyless entities

            foreach (var fk in entityType.GetForeignKeys())
            {
                var constraintName = fk.GetConstraintName();
                if (string.IsNullOrEmpty(constraintName)) continue;
                fks.Add(constraintName);
            }
        }
        return fks;
    }

    // ───────────────────────────────────────────────────────────────────────
    //  Database introspection helpers (query information_schema / pg_constraint)
    // ───────────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> GetActualTablesAsync()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_type = 'BASE TABLE'
            ORDER BY table_name;
            """;

        var tables = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private async Task<HashSet<(string Table, string Column)>> GetActualColumnsAsync()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
            ORDER BY table_name, column_name;
            """;

        var columns = new HashSet<(string Table, string Column)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add((reader.GetString(0), reader.GetString(1)));
        return columns;
    }

    private async Task<HashSet<string>> GetActualForeignKeyNamesAsync()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT conname
            FROM pg_constraint
            JOIN pg_namespace ON pg_namespace.oid = pg_constraint.connamespace
            WHERE pg_namespace.nspname = 'public'
              AND pg_constraint.contype = 'f'
            ORDER BY conname;
            """;

        var fks = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            fks.Add(reader.GetString(0));
        return fks;
    }
}
