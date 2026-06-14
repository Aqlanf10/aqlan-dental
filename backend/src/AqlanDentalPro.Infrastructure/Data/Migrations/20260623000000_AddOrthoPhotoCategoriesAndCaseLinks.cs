using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Ortho Module Phase 2 — standardized records and images.
///
/// Reconciles the core orthodontic tables that were previously defined by
/// hand-written migrations without EF migration metadata. Existing databases
/// keep their data; missing tables/columns are created idempotently before the
/// Phase 2 indexes are applied.
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
        // 0. Reconcile legacy ortho schema. Several older hand-written migration
        // files had no designer/metadata, so EF never discovered them in some
        // environments even though the current model depends on these objects.
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""OrthoDiagnoses"" (
    ""Id"" uuid NOT NULL,
    ""OrthoCaseId"" uuid NOT NULL,
    ""SkeletalClassification"" character varying(50) NULL,
    ""DentalClassification"" character varying(50) NULL,
    ""FacialPattern"" character varying(50) NULL,
    ""ANB"" numeric(6,2) NULL,
    ""Wits"" numeric(6,2) NULL,
    ""FMA"" numeric(6,2) NULL,
    ""SNA"" numeric(6,2) NULL,
    ""SNB"" numeric(6,2) NULL,
    ""IMPA"" numeric(6,2) NULL,
    ""SoftTissueDiagnosis"" text NULL,
    ""FunctionalDiagnosis"" text NULL,
    ""Etiology"" text NULL,
    ""Summary"" text NULL,
    ""ApprovedBy"" uuid NULL,
    ""ApprovedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_OrthoDiagnoses"" PRIMARY KEY (""Id"")
);

ALTER TABLE ""OrthoDiagnoses"" ADD COLUMN IF NOT EXISTS ""SoftTissueDiagnosis"" text NULL;
ALTER TABLE ""OrthoDiagnoses"" ADD COLUMN IF NOT EXISTS ""FunctionalDiagnosis"" text NULL;
ALTER TABLE ""OrthoDiagnoses"" ADD COLUMN IF NOT EXISTS ""Etiology"" text NULL;
ALTER TABLE ""OrthoDiagnoses"" ADD COLUMN IF NOT EXISTS ""ApprovedBy"" uuid NULL;
ALTER TABLE ""OrthoDiagnoses"" ADD COLUMN IF NOT EXISTS ""ApprovedAt"" timestamp with time zone NULL;

CREATE TABLE IF NOT EXISTS ""OrthoClinicalPhotos"" (
    ""Id"" uuid NOT NULL,
    ""OrthoCaseId"" uuid NOT NULL,
    ""PhotoUrl"" character varying(1000) NOT NULL,
    ""PhotoType"" character varying(50) NOT NULL,
    ""Caption"" character varying(500) NULL,
    ""TakenAt"" timestamp with time zone NOT NULL,
    ""SortOrder"" integer NOT NULL DEFAULT 0,
    ""Category"" character varying(50) NULL,
    ""Subtype"" character varying(100) NULL,
    ""TreatmentPhase"" character varying(20) NULL,
    ""IsSelectedForReport"" boolean NOT NULL DEFAULT false,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_OrthoClinicalPhotos"" PRIMARY KEY (""Id"")
);

CREATE TABLE IF NOT EXISTS ""RecordsChecklists"" (
    ""Id"" uuid NOT NULL,
    ""OrthoCaseId"" uuid NOT NULL,
    ""ExtraoralFrontal"" boolean NOT NULL DEFAULT false,
    ""ExtraoralProfile"" boolean NOT NULL DEFAULT false,
    ""ExtraoralSmile"" boolean NOT NULL DEFAULT false,
    ""IntraoralFrontal"" boolean NOT NULL DEFAULT false,
    ""IntraoralRight"" boolean NOT NULL DEFAULT false,
    ""IntraoralLeft"" boolean NOT NULL DEFAULT false,
    ""UpperOcclusal"" boolean NOT NULL DEFAULT false,
    ""LowerOcclusal"" boolean NOT NULL DEFAULT false,
    ""Opg"" boolean NOT NULL DEFAULT false,
    ""LateralCeph"" boolean NOT NULL DEFAULT false,
    ""Cbct"" boolean NOT NULL DEFAULT false,
    ""StudyModels"" boolean NOT NULL DEFAULT false,
    ""Consent"" boolean NOT NULL DEFAULT false,
    ""Contract"" boolean NOT NULL DEFAULT false,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_RecordsChecklists"" PRIMARY KEY (""Id"")
);

ALTER TABLE ""TreatmentPlans""
    ADD COLUMN IF NOT EXISTS ""PlanLabel"" character varying(5) NOT NULL DEFAULT 'A';

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoDiagnoses_OrthoCases_OrthoCaseId') THEN
        ALTER TABLE ""OrthoDiagnoses"" ADD CONSTRAINT ""FK_OrthoDiagnoses_OrthoCases_OrthoCaseId""
            FOREIGN KEY (""OrthoCaseId"") REFERENCES ""OrthoCases"" (""Id"") ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoDiagnoses_Doctors_ApprovedBy') THEN
        ALTER TABLE ""OrthoDiagnoses"" ADD CONSTRAINT ""FK_OrthoDiagnoses_Doctors_ApprovedBy""
            FOREIGN KEY (""ApprovedBy"") REFERENCES ""Doctors"" (""Id"") ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OrthoClinicalPhotos_OrthoCases_OrthoCaseId') THEN
        ALTER TABLE ""OrthoClinicalPhotos"" ADD CONSTRAINT ""FK_OrthoClinicalPhotos_OrthoCases_OrthoCaseId""
            FOREIGN KEY (""OrthoCaseId"") REFERENCES ""OrthoCases"" (""Id"") ON DELETE CASCADE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_RecordsChecklists_OrthoCases_OrthoCaseId') THEN
        ALTER TABLE ""RecordsChecklists"" ADD CONSTRAINT ""FK_RecordsChecklists_OrthoCases_OrthoCaseId""
            FOREIGN KEY (""OrthoCaseId"") REFERENCES ""OrthoCases"" (""Id"") ON DELETE CASCADE;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_OrthoDiagnoses_OrthoCaseId""
    ON ""OrthoDiagnoses"" (""OrthoCaseId"");
CREATE INDEX IF NOT EXISTS ""IX_OrthoDiagnoses_ApprovedBy""
    ON ""OrthoDiagnoses"" (""ApprovedBy"");
CREATE INDEX IF NOT EXISTS ""IX_OrthoClinicalPhotos_OrthoCaseId""
    ON ""OrthoClinicalPhotos"" (""OrthoCaseId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_RecordsChecklists_OrthoCaseId""
    ON ""RecordsChecklists"" (""OrthoCaseId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_TreatmentPlans_OrthoCaseId_PlanLabel""
    ON ""TreatmentPlans"" (""OrthoCaseId"", ""PlanLabel"");
");

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
