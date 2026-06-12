using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Ortho Module Phase 2 — standardized records and images.
///
/// - OrthoClinicalPhotos: adds Category / Subtype / TreatmentPhase (nullable strings,
///   validated against OrthoPhotoCategory / OrthoTreatmentPhase enums at the API layer)
///   and IsSelectedForReport (boolean NOT NULL DEFAULT false).
/// - Radiographs / Documents: adds nullable OrthoCaseId linking patient-level records
///   to a specific orthodontic case (ON DELETE SET NULL — deleting an ortho case must
///   never delete patient radiographs or documents).
///
/// Everything is idempotent raw SQL (ADD COLUMN IF NOT EXISTS / guarded constraints),
/// matching the established hand-written migration pattern in this project.
/// </summary>
public partial class AddOrthoPhotoCategoriesAndCaseLinks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. OrthoClinicalPhotos — standardized record tagging columns
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""OrthoClinicalPhotos""') IS NOT NULL THEN
        ALTER TABLE ""OrthoClinicalPhotos"" ADD COLUMN IF NOT EXISTS ""Category"" character varying(50) NULL;
        ALTER TABLE ""OrthoClinicalPhotos"" ADD COLUMN IF NOT EXISTS ""Subtype"" character varying(100) NULL;
        ALTER TABLE ""OrthoClinicalPhotos"" ADD COLUMN IF NOT EXISTS ""TreatmentPhase"" character varying(20) NULL;
        ALTER TABLE ""OrthoClinicalPhotos"" ADD COLUMN IF NOT EXISTS ""IsSelectedForReport"" boolean NOT NULL DEFAULT false;
    END IF;
END $$;
");

        // 2. Radiographs / Documents — optional ortho case link
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""Radiographs""') IS NOT NULL THEN
        ALTER TABLE ""Radiographs"" ADD COLUMN IF NOT EXISTS ""OrthoCaseId"" uuid NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""Documents""') IS NOT NULL THEN
        ALTER TABLE ""Documents"" ADD COLUMN IF NOT EXISTS ""OrthoCaseId"" uuid NULL;
    END IF;
END $$;
");

        // 3. Guarded FK constraints to OrthoCases (SET NULL — never delete patient records)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Radiographs_OrthoCases_OrthoCaseId')
        AND to_regclass('public.""Radiographs""') IS NOT NULL
        AND to_regclass('public.""OrthoCases""') IS NOT NULL THEN
        ALTER TABLE ""Radiographs"" ADD CONSTRAINT ""FK_Radiographs_OrthoCases_OrthoCaseId""
            FOREIGN KEY (""OrthoCaseId"") REFERENCES ""OrthoCases"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Documents_OrthoCases_OrthoCaseId')
        AND to_regclass('public.""Documents""') IS NOT NULL
        AND to_regclass('public.""OrthoCases""') IS NOT NULL THEN
        ALTER TABLE ""Documents"" ADD CONSTRAINT ""FK_Documents_OrthoCases_OrthoCaseId""
            FOREIGN KEY (""OrthoCaseId"") REFERENCES ""OrthoCases"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");

        // 4. Indexes
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_OrthoClinicalPhotos_OrthoCaseId_Category"" ON ""OrthoClinicalPhotos"" (""OrthoCaseId"", ""Category"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Radiographs_OrthoCaseId"" ON ""Radiographs"" (""OrthoCaseId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Documents_OrthoCaseId"" ON ""Documents"" (""OrthoCaseId"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Radiographs_OrthoCases_OrthoCaseId') THEN
        ALTER TABLE ""Radiographs"" DROP CONSTRAINT ""FK_Radiographs_OrthoCases_OrthoCaseId"";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Documents_OrthoCases_OrthoCaseId') THEN
        ALTER TABLE ""Documents"" DROP CONSTRAINT ""FK_Documents_OrthoCases_OrthoCaseId"";
    END IF;
END $$;
");

        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_OrthoClinicalPhotos_OrthoCaseId_Category"";");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Radiographs_OrthoCaseId"";");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Documents_OrthoCaseId"";");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""OrthoClinicalPhotos""') IS NOT NULL THEN
        ALTER TABLE ""OrthoClinicalPhotos"" DROP COLUMN IF EXISTS ""Category"";
        ALTER TABLE ""OrthoClinicalPhotos"" DROP COLUMN IF EXISTS ""Subtype"";
        ALTER TABLE ""OrthoClinicalPhotos"" DROP COLUMN IF EXISTS ""TreatmentPhase"";
        ALTER TABLE ""OrthoClinicalPhotos"" DROP COLUMN IF EXISTS ""IsSelectedForReport"";
    END IF;
    IF to_regclass('public.""Radiographs""') IS NOT NULL THEN
        ALTER TABLE ""Radiographs"" DROP COLUMN IF EXISTS ""OrthoCaseId"";
    END IF;
    IF to_regclass('public.""Documents""') IS NOT NULL THEN
        ALTER TABLE ""Documents"" DROP COLUMN IF EXISTS ""OrthoCaseId"";
    END IF;
END $$;
");
    }
}
