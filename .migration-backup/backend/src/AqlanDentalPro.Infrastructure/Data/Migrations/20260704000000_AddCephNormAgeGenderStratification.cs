using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// CLIN-10 — Stratify cephalometric norms by age/gender (patient-safety fix).
///
/// Adds three nullable columns to CephNorms:
///   • AgeMin  (integer, null = no lower bound, inclusive)
///   • AgeMax  (integer, null = no upper bound, inclusive)
///   • Sex     (character varying(1), null = applies to both sexes; "M" or "F")
///
/// A row with all three null remains "un-stratified" and matches any patient
/// (the pre-CLIN-10 behavior) — the lookup falls back to it when no
/// age/sex-specific row matches. CephService.FindBestCephNorm picks the most
/// specific match (sex-specific &gt; sex-null, age-banded &gt; age-null) at compute
/// time, so a 10-year-old is never compared against an adult norm and vice
/// versa.
///
/// Drops the legacy unique index on (MeasurementName, AnalysisGroup) — it would
/// block seeding the stratified rows (child / adolescent / adult × M / F / any
/// all share the same (MeasurementName, AnalysisGroup) pair). Replaces it with
/// a non-unique composite index that supports both the best-match lookup query
/// and the unique-by-strata invariant enforced in CephNormSeeder. Pattern
/// mirrors 20260702000000_AddOrthoCaseLinksToAppointmentsAndVisits (EF Core
/// migrationBuilder API, no raw SQL).
/// </summary>
public partial class AddCephNormAgeGenderStratification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Production may already have received this schema through the guarded
        // startup repair before EF reaches this migration. Keep every statement
        // idempotent so a partially repaired database can continue through the
        // migration chain.
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_CephNorms_MeasurementName_AnalysisGroup";
            ALTER TABLE "CephNorms" ADD COLUMN IF NOT EXISTS "AgeMin" integer NULL;
            ALTER TABLE "CephNorms" ADD COLUMN IF NOT EXISTS "AgeMax" integer NULL;
            ALTER TABLE "CephNorms" ADD COLUMN IF NOT EXISTS "Sex" character varying(1) NULL;
            CREATE INDEX IF NOT EXISTS "IX_CephNorms_MeasurementName_AnalysisGroup_AgeMin_AgeMax_Sex"
                ON "CephNorms" ("MeasurementName", "AnalysisGroup", "AgeMin", "AgeMax", "Sex");
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CephNorms_MeasurementName_AnalysisGroup_AgeMin_AgeMax_Sex",
            table: "CephNorms");

        migrationBuilder.DropColumn(
            name: "Sex",
            table: "CephNorms");

        migrationBuilder.DropColumn(
            name: "AgeMax",
            table: "CephNorms");

        migrationBuilder.DropColumn(
            name: "AgeMin",
            table: "CephNorms");

        // Restore the legacy unique index (only safe on a table where all
        // stratified rows have been deleted first — otherwise duplicates on
        // (MeasurementName, AnalysisGroup) will cause CREATE UNIQUE INDEX to
        // fail. The Down path is for development rollbacks only; production
        // roll-forward is the supported direction per CLAUDE.md.)
        migrationBuilder.CreateIndex(
            name: "IX_CephNorms_MeasurementName_AnalysisGroup",
            table: "CephNorms",
            columns: new[] { "MeasurementName", "AnalysisGroup" },
            unique: true);
    }
}
