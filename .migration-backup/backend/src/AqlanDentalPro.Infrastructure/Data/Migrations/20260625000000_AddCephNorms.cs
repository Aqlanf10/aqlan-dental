using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Ceph batch C-A — configurable cephalometric norms.
///
/// Creates the CephNorms table (normal value ± SD per measurement per analysis
/// group, with optional explicit Min/Max normal range, Arabic name, clinical
/// category, and Arabic interpretation strings) plus a unique index on
/// (MeasurementName, AnalysisGroup).
///
/// Factory values are seeded at startup by StartupDatabaseMaintenance via
/// CephNormSeeder (only when the table is empty) — not inside this migration —
/// so fresh databases bootstrapped from the EF model baseline get the same seed.
///
/// Everything is idempotent raw SQL (CREATE TABLE IF NOT EXISTS / guarded drop),
/// matching the established hand-written migration pattern in this project.
/// </summary>
public partial class AddCephNorms : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""CephNorms"" (
    ""Id"" uuid NOT NULL,
    ""MeasurementName"" character varying(50) NOT NULL,
    ""NameAr"" character varying(200) NULL,
    ""AnalysisGroup"" character varying(30) NOT NULL,
    ""NormalValue"" numeric NOT NULL,
    ""StdDeviation"" numeric NOT NULL,
    ""MinNormal"" numeric NULL,
    ""MaxNormal"" numeric NULL,
    ""Unit"" character varying(10) NOT NULL,
    ""Category"" character varying(30) NULL,
    ""InterpretationBelow"" character varying(300) NULL,
    ""InterpretationNormal"" character varying(300) NULL,
    ""InterpretationAbove"" character varying(300) NULL,
    ""SortOrder"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_CephNorms"" PRIMARY KEY (""Id"")
);
");

        migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CephNorms_MeasurementName_AnalysisGroup"" ON ""CephNorms"" (""MeasurementName"", ""AnalysisGroup"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_CephNorms_MeasurementName_AnalysisGroup"";");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""CephNorms""') IS NOT NULL THEN
        DROP TABLE ""CephNorms"";
    END IF;
END $$;
");
    }
}
