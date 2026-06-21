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
        // Drop the legacy unique index first — it would block the new stratified
        // rows (child SNA + adolescent SNA + adult SNA share the same
        // (MeasurementName, AnalysisGroup) key).
        migrationBuilder.DropIndex(
            name: "IX_CephNorms_MeasurementName_AnalysisGroup",
            table: "CephNorms");

        // CLIN-10: age/gender stratification columns (nullable, backward compatible).
        migrationBuilder.AddColumn<int>(
            name: "AgeMin",
            table: "CephNorms",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AgeMax",
            table: "CephNorms",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Sex",
            table: "CephNorms",
            type: "character varying(1)",
            maxLength: 1,
            nullable: true);

        // Non-unique composite index — supports the best-match lookup
        // (WHERE MeasurementName = @m AND AnalysisGroup = @g
        //        AND (AgeMin IS NULL OR AgeMin <= @age)
        //        AND (AgeMax IS NULL OR AgeMax >= @age)
        //        AND (Sex IS NULL OR Sex = @sex)).
        migrationBuilder.CreateIndex(
            name: "IX_CephNorms_MeasurementName_AnalysisGroup_AgeMin_AgeMax_Sex",
            table: "CephNorms",
            columns: new[] { "MeasurementName", "AnalysisGroup", "AgeMin", "AgeMax", "Sex" });
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
