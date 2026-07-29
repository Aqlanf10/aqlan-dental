using System.Reflection;
using AqlanDentalPro.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

public class OrthoProductionSchemaReconciliationTests
{
    private static IReadOnlyList<MigrationOperation> GetUpOperations()
    {
        var migration = new AddOrthoPhotoCategoriesAndCaseLinks();
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var method = typeof(AddOrthoPhotoCategoriesAndCaseLinks)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!;

        method.Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string GetSql() =>
        string.Join(
            Environment.NewLine,
            GetUpOperations()
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));

    [Fact]
    public void Up_ReconcilesAllMissingCoreOrthoObjectsBeforeIndexes()
    {
        var sql = GetSql();

        Assert.Contains("CREATE TABLE IF NOT EXISTS \"OrthoDiagnoses\"", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"OrthoClinicalPhotos\"", sql);
        Assert.Contains("CREATE TABLE IF NOT EXISTS \"RecordsChecklists\"", sql);
        Assert.Contains(
            "ADD COLUMN IF NOT EXISTS \"PlanLabel\" character varying(5) NOT NULL DEFAULT 'A'",
            sql);
    }

    [Fact]
    public void Up_ReconciliationIsAdditiveAndIdempotent()
    {
        var firstSql = GetUpOperations().OfType<SqlOperation>().First().Sql;

        Assert.Contains("CREATE TABLE IF NOT EXISTS", firstSql);
        Assert.Contains("ADD COLUMN IF NOT EXISTS", firstSql);
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM pg_constraint", firstSql);
        Assert.DoesNotContain("DROP TABLE", firstSql);
        Assert.DoesNotContain("DROP COLUMN", firstSql);
        Assert.DoesNotContain("DELETE FROM", firstSql);
        Assert.DoesNotContain("UPDATE ", firstSql);
    }

    [Fact]
    public void Up_CreatesRequiredRelationshipsAndUniqueIndexes()
    {
        var sql = GetSql();

        Assert.Contains("FK_OrthoDiagnoses_OrthoCases_OrthoCaseId", sql);
        Assert.Contains("FK_OrthoDiagnoses_Doctors_ApprovedBy", sql);
        Assert.Contains("FK_OrthoClinicalPhotos_OrthoCases_OrthoCaseId", sql);
        Assert.Contains("FK_RecordsChecklists_OrthoCases_OrthoCaseId", sql);
        Assert.Contains("IX_OrthoDiagnoses_OrthoCaseId", sql);
        Assert.Contains("IX_RecordsChecklists_OrthoCaseId", sql);
        Assert.Contains("IX_TreatmentPlans_OrthoCaseId_PlanLabel", sql);
    }

    [Fact]
    public void Up_CreatesPhotoTableBeforePhotoCategoryIndex()
    {
        var sql = GetSql();
        var tablePosition = sql.IndexOf(
            "CREATE TABLE IF NOT EXISTS \"OrthoClinicalPhotos\"",
            StringComparison.Ordinal);
        var indexPosition = sql.IndexOf(
            "IX_OrthoClinicalPhotos_OrthoCaseId_Category",
            StringComparison.Ordinal);

        Assert.True(tablePosition >= 0);
        Assert.True(indexPosition > tablePosition);
    }
}
