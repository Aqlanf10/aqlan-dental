using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent raw SQL (matches the established project pattern, e.g.
            // 20260625000000_AddCephNorms). The startup hotfix
            // EnsurePhotoAnalysisSchemaAsync may create this table before gated
            // MigrateAsync runs, so a plain CreateTable would fail with
            // "relation already exists" and leave this migration unrecorded.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""PhotoAnalyses"" (
    ""Id"" uuid NOT NULL,
    ""OrthoCaseId"" uuid NOT NULL,
    ""ViewType"" character varying(20) NOT NULL,
    ""ImageFileUrl"" character varying(1000) NOT NULL,
    ""LandmarksJson"" text NULL,
    ""MeasurementsJson"" text NULL,
    ""DoctorId"" uuid NULL,
    ""Notes"" character varying(2000) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_PhotoAnalyses"" PRIMARY KEY (""Id"")
);
");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_PhotoAnalyses_OrthoCaseId"" ON ""PhotoAnalyses"" (""OrthoCaseId"");");

            // Add the FK only when OrthoCases exists and the constraint is not
            // already present (the hotfix may have created the table without it).
            migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'OrthoCases')
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PhotoAnalyses_OrthoCases_OrthoCaseId') THEN
        ALTER TABLE ""PhotoAnalyses""
            ADD CONSTRAINT ""FK_PhotoAnalyses_OrthoCases_OrthoCaseId""
            FOREIGN KEY (""OrthoCaseId"") REFERENCES ""OrthoCases"" (""Id"") ON DELETE CASCADE;
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""PhotoAnalyses"";");
        }
    }
}
