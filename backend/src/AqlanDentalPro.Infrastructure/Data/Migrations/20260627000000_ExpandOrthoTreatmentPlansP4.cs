using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Orthodontics P4: structured treatment objectives, phased timelines,
/// mechanics, interdisciplinary planning, and separately audited patient
/// presentation/decision fields.
///
/// The migration is additive and idempotent. Existing treatment plans remain
/// valid and default to NotPresented without changing their clinical approval.
/// </summary>
public partial class ExpandOrthoTreatmentPlansP4 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DO $$ BEGIN
    IF to_regclass('public."TreatmentPlans"') IS NOT NULL THEN
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "MechanicsPlan" character varying(4000) NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "AuxiliaryAppliances" character varying(2000) NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "SpaceManagementPlan" character varying(2000) NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "InterdisciplinaryPlan" character varying(2000) NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "PatientDecisionStatus" character varying(30) NOT NULL DEFAULT 'NotPresented';
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "PresentedAt" timestamp with time zone NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "PatientDecisionAt" timestamp with time zone NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "PatientDecisionBy" character varying(200) NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "PatientConsentMethod" character varying(100) NULL;
        ALTER TABLE "TreatmentPlans" ADD COLUMN IF NOT EXISTS "PatientDecisionNotes" character varying(2000) NULL;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS "TreatmentPlanObjectives" (
    "Id" uuid NOT NULL,
    "TreatmentPlanId" uuid NOT NULL,
    "Category" character varying(50) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "Priority" integer NOT NULL DEFAULT 2,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "IsAchieved" boolean NOT NULL DEFAULT false,
    "AchievedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL,
    CONSTRAINT "PK_TreatmentPlanObjectives" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TreatmentPlanObjectives_TreatmentPlans_TreatmentPlanId"
        FOREIGN KEY ("TreatmentPlanId") REFERENCES "TreatmentPlans" ("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "TreatmentPlanPhases" (
    "Id" uuid NOT NULL,
    "TreatmentPlanId" uuid NOT NULL,
    "PhaseName" character varying(150) NOT NULL,
    "SequenceNumber" integer NOT NULL,
    "ObjectiveSummary" character varying(1000) NULL,
    "PlannedAppliance" character varying(500) NULL,
    "Mechanics" character varying(2000) NULL,
    "TargetDurationMonths" integer NULL,
    "PlannedStartDate" date NULL,
    "PlannedEndDate" date NULL,
    "Status" character varying(30) NOT NULL DEFAULT 'Planned',
    "Notes" character varying(1000) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL,
    CONSTRAINT "PK_TreatmentPlanPhases" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TreatmentPlanPhases_TreatmentPlans_TreatmentPlanId"
        FOREIGN KEY ("TreatmentPlanId") REFERENCES "TreatmentPlans" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_TreatmentPlanObjectives_TreatmentPlanId_SortOrder"
    ON "TreatmentPlanObjectives" ("TreatmentPlanId", "SortOrder");
CREATE INDEX IF NOT EXISTS "IX_TreatmentPlanPhases_TreatmentPlanId_SequenceNumber"
    ON "TreatmentPlanPhases" ("TreatmentPlanId", "SequenceNumber");
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DROP TABLE IF EXISTS "TreatmentPlanPhases";
DROP TABLE IF EXISTS "TreatmentPlanObjectives";

DO $$ BEGIN
    IF to_regclass('public."TreatmentPlans"') IS NOT NULL THEN
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "PatientDecisionNotes";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "PatientConsentMethod";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "PatientDecisionBy";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "PatientDecisionAt";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "PresentedAt";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "PatientDecisionStatus";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "InterdisciplinaryPlan";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "SpaceManagementPlan";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "AuxiliaryAppliances";
        ALTER TABLE "TreatmentPlans" DROP COLUMN IF EXISTS "MechanicsPlan";
    END IF;
END $$;
""");
    }
}
