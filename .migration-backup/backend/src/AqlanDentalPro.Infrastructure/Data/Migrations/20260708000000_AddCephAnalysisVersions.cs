using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// CEPH-EPIC batch C-B — ceph analysis version snapshots. Idempotent raw
    /// SQL (matches the established project pattern, e.g.
    /// 20260625000000_AddCephNorms and 20260629000000_AddPhotoAnalysis). The
    /// startup hotfix EnsureCephAnalysisVersionsSchemaAsync may create this
    /// table before gated MigrateAsync runs on a fresh DB, so a plain
    /// CreateTable would fail with "relation already exists" and leave the
    /// migration unrecorded (the original C-08 trap documented in CLAUDE.md).
    /// </summary>
    public partial class AddCephAnalysisVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""CephAnalysisVersions"" (
    ""Id"" uuid NOT NULL,
    ""CephAnalysisId"" uuid NOT NULL,
    ""Label"" character varying(100) NOT NULL,
    ""LandmarksJson"" text NOT NULL,
    ""MeasurementsJson"" text NOT NULL,
    ""DiagnosisJson"" text NULL,
    ""SnapshotDate"" date NOT NULL,
    ""CreatedByUserId"" uuid NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_CephAnalysisVersions"" PRIMARY KEY (""Id"")
);
");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CephAnalysisVersions_CephAnalysisId"" " +
                @"ON ""CephAnalysisVersions"" (""CephAnalysisId"");");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_CephAnalysisVersions_CephAnalysisId_SnapshotDate"" " +
                @"ON ""CephAnalysisVersions"" (""CephAnalysisId"", ""SnapshotDate"");");

            // Add the FK + cascade only when CephAnalyses exists and the
            // constraint is not already present (the hotfix may have created
            // the table without it).
            migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CephAnalyses')
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CephAnalysisVersions_CephAnalyses_CephAnalysisId') THEN
        ALTER TABLE ""CephAnalysisVersions""
            ADD CONSTRAINT ""FK_CephAnalysisVersions_CephAnalyses_CephAnalysisId""
            FOREIGN KEY (""CephAnalysisId"") REFERENCES ""CephAnalyses"" (""Id"") ON DELETE CASCADE;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""CephAnalysisVersions"";");
        }
    }
}
