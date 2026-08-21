using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Xunit;

namespace AqlanDentalPro.UnitTests.Infrastructure;

/// <summary>
/// CORE-EF-001 — <c>AppDbContextModelSnapshot.cs</c> must describe the same model the entity
/// configurations build.
///
/// <para>
/// The snapshot is the baseline every <c>dotnet ef migrations add</c> diffs against. Nothing at
/// runtime reads it, which is exactly why it rots unnoticed: the application boots, the tests
/// pass, and the damage only appears months later in the first migration someone tries to
/// generate. This repository reached that state — <c>migrations add</c> had stopped working
/// altogether, and the drift it had accumulated in the meantime was not cosmetic:
/// </para>
///
/// <list type="bullet">
///   <item>the snapshot claimed an index <c>IX_DoctorCommissionPayments_BranchId</c> that no
///   migration has ever created and no database has ever had. The next migration generated from
///   it would have opened with a <c>DropIndex</c> for an index that does not exist — which
///   Postgres answers with an error, aborting the migration mid-transaction on production;</item>
///   <item>the three <c>CephPilot*</c> tables were missing from it, so that same migration would
///   have tried to <c>CreateTable</c> tables the database already had.</item>
/// </list>
///
/// <para>
/// Neither would have been caught by review, because the developer generating that migration
/// would have been changing something else entirely and had no reason to read past their own
/// diff. So the guard belongs here, where it fails on the commit that causes it rather than on
/// the commit that trips over it.
/// </para>
///
/// <para>
/// The fix is always the same and always cheap: run <c>dotnet ef migrations add</c> for the
/// change you are making, and commit the regenerated snapshot alongside it. This test needs no
/// database — the comparison is model-to-model.
/// </para>
/// </summary>
public sealed class ModelSnapshotInSyncTests
{
    private static AppDbContext ModelOnlyContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=x;Password=x")
            .Options);

    [Fact]
    public void Snapshot_describes_the_same_model_as_the_entity_configurations()
    {
        using var db = ModelOnlyContext();

        var differences = PendingOperations(db);

        differences.Should().BeEmpty(
            "the model snapshot is stale. Regenerate it with `dotnet ef migrations add <Name> "
            + "--project src/AqlanDentalPro.Infrastructure --startup-project src/AqlanDentalPro.API` "
            + "and commit the updated AppDbContextModelSnapshot.cs. Undescribed changes: "
            + string.Join(", ", differences));
    }

    /// <summary>
    /// Guards the test itself. <see cref="IMigrationsModelDiffer"/> returning an empty list
    /// because it was handed nothing to compare would make the assertion above pass forever, so
    /// prove it reports a difference it should report.
    /// </summary>
    [Fact]
    public void The_differ_would_notice_a_model_that_had_moved()
    {
        using var db = ModelOnlyContext();

        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var relational = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();

        // The snapshot compared against an empty model must read as "create everything".
        var fromNothing = differ.GetDifferences(null, relational);

        fromNothing.Should().NotBeEmpty(
            "a differ that reports no work for building this schema from an empty database is "
            + "not comparing anything, and the sync assertion above would be vacuous");
        snapshot.Should().NotBeNull("the migrations assembly must expose a snapshot to compare");
    }

    private static List<string> PendingOperations(AppDbContext db)
    {
        var differ = db.GetService<IMigrationsModelDiffer>();
        var snapshotModel = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;

        snapshotModel.Should().NotBeNull("AppDbContextModelSnapshot.cs must be present");

        var source = ((IMutableModel)snapshotModel!).FinalizeModel();
        source = db.GetService<IModelRuntimeInitializer>().Initialize(source, designTime: true);

        return differ
            .GetDifferences(source.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel())
            .Select(op => op.GetType().Name)
            .ToList();
    }
}
