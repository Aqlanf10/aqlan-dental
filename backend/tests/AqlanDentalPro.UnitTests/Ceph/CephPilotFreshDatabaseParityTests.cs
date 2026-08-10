using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// CORE-F-002 applied to the Pilot: every check constraint the migration adds must also be
/// declared in the EF model.
/// <para>
/// A brand-new database is not built by replaying the migration chain. It is built from
/// <c>GenerateCreateScript()</c> over the model, in
/// <c>StartupDatabaseMaintenance.EnsureFreshDatabaseMigratedAsync</c>. Anything expressed only
/// in a migration is therefore absent from a fresh deployment, and absent silently — PR #812
/// found a ledger table and two ledger triggers missing from every fresh database for exactly
/// this reason, and nothing had reported it.
/// </para>
/// <para>
/// For the Pilot the loss would not be a missing feature but an invalid study. These
/// constraints are what stop the database accepting a project with blinding switched off, one
/// person assigned as both reviewers, or a case marked Ready before anyone confirmed the
/// radiograph carries no patient identity. Every screen would still look right.
/// </para>
/// </summary>
public sealed class CephPilotFreshDatabaseParityTests
{
    private sealed class MigrationProbe : AddCephPilotFoundation
    {
        public IReadOnlyList<MigrationOperation> Operations()
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            Up(builder);
            return builder.Operations;
        }
    }

    private static AppDbContext NewModelContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=x;Password=x")
            .Options);

    /// <summary>
    /// Check constraints are not kept in the runtime model — EF strips anything it does not
    /// need to execute queries — so they have to be read from the design-time model. That is
    /// also the model <c>GenerateCreateScript</c> builds from, which makes it the right one
    /// to assert against here.
    /// </summary>
    private static IModel DesignTimeModel(AppDbContext db) =>
        db.GetService<IDesignTimeModel>().Model;

    private static readonly string[] PilotTables =
    [
        "CephPilotProjects",
        "CephPilotCases",
        "CephPilotExportArtifacts",
    ];

    [Fact]
    public void EveryPilotCheckConstraintInTheMigrationIsAlsoInTheEfModel()
    {
        var fromMigration = new MigrationProbe().Operations()
            .OfType<AddCheckConstraintOperation>()
            .Where(op => PilotTables.Contains(op.Table))
            .Select(op => op.Name)
            .ToHashSet(StringComparer.Ordinal);

        fromMigration.Should().NotBeEmpty(
            "the migration is the reason this test exists — if it stops adding checks, this test " +
            "would silently pass while proving nothing");

        using var db = NewModelContext();
        var fromModel = DesignTimeModel(db).GetEntityTypes()
            .Where(e => PilotTables.Contains(e.GetTableName()))
            .SelectMany(e => e.GetCheckConstraints())
            .Select(c => c.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = fromMigration.Except(fromModel).OrderBy(n => n, StringComparer.Ordinal).ToList();

        missing.Should().BeEmpty(
            "a fresh database is created from the EF model, so a constraint that exists only in " +
            "the migration protects upgraded databases and nothing else. Missing from the model: {0}",
            string.Join(", ", missing));
    }

    [Theory]
    // Named individually rather than only as a set, so a failure says which guarantee was lost
    // instead of just how many.
    [InlineData("CK_CephPilotProjects_DistinctReviewers")]
    [InlineData("CK_CephPilotProjects_RatifiedVersion")]
    [InlineData("CK_CephPilotProjects_BlindingEnabled")]
    [InlineData("CK_CephPilotCases_ReadyRequiresGate")]
    [InlineData("CK_CephPilotExportArtifacts_ComparatorOnly")]
    public void TheFreshDatabaseScriptCarriesTheConstraint(string constraintName)
    {
        using var db = NewModelContext();

        // The literal artifact a new deployment is built from, not a proxy for it.
        var createScript = db.Database.GenerateCreateScript();

        createScript.Should().Contain(constraintName,
            "this is the script StartupDatabaseMaintenance runs against an empty database");
    }

    [Fact]
    public void TheModelAndTheMigrationAgreeOnWhatEachConstraintSays()
    {
        // Matching names is not enough: a model constraint with the same name but weaker SQL
        // would pass the test above while leaving a fresh database less protected than an
        // upgraded one — the failure mode hardest to notice.
        var migrationSql = new MigrationProbe().Operations()
            .OfType<AddCheckConstraintOperation>()
            .Where(op => PilotTables.Contains(op.Table))
            .ToDictionary(op => op.Name, op => Normalize(op.Sql), StringComparer.Ordinal);

        using var db = NewModelContext();
        var modelSql = DesignTimeModel(db).GetEntityTypes()
            .Where(e => PilotTables.Contains(e.GetTableName()))
            .SelectMany(e => e.GetCheckConstraints())
            .ToDictionary(c => c.Name, c => Normalize(c.Sql), StringComparer.Ordinal);

        foreach (var (name, sql) in migrationSql)
        {
            modelSql.Should().ContainKey(name);
            modelSql[name].Should().Be(sql,
                "the model and the migration must express the same rule for {0}, or an upgraded " +
                "database and a fresh one enforce different things", name);
        }
    }

    private static string Normalize(string sql) =>
        string.Concat(sql.Where(c => !char.IsWhiteSpace(c)));
}
