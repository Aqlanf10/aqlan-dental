using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// YOLO-S1: Appointment enhancements — companion/guardian + WhatsApp + color + treatment package.
///
/// Adds to Appointments:
///   - CompanionName, CompanionPhone, CompanionRelationship (nullable strings)
///   - AppointmentColor (nullable hex string for calendar display)
///   - PackageId (nullable FK → TreatmentPackages.Id, ON DELETE SET NULL)
///
/// Creates new TreatmentPackages table (simple catalog: Name/Description/TotalPrice/
/// SessionCount/Color/IsActive + BaseEntity audit columns).
///
/// All new columns are nullable — no behavior change for existing appointments.
/// Idempotent: uses ADD COLUMN IF NOT EXISTS / CREATE TABLE IF NOT EXISTS so the
/// migration is safe to apply on databases where the C-08 startup hotfix already
/// added the columns. Mirrors the existing migration pattern in this codebase
/// (see 20260610000000_AddSeparateReminderTrackingAndPatientEmail).
///
/// Down: drops the FK + index + columns + table. Safe to roll back because all
/// new fields are optional and have no production data dependencies yet.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260712000000_AddAppointmentEnhancements")]
public partial class AddAppointmentEnhancements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── TreatmentPackages table ───────────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "TreatmentPackages" (
                "Id"           uuid                     NOT NULL DEFAULT gen_random_uuid(),
                "Name"         character varying(200)   NOT NULL,
                "Description"  character varying(1000)  NULL,
                "TotalPrice"   numeric(12,2)            NOT NULL DEFAULT 0,
                "SessionCount" integer                  NOT NULL DEFAULT 1,
                "Color"        character varying(20)    NULL,
                "IsActive"     boolean                  NOT NULL DEFAULT true,
                "CreatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                "DeletedAt"    timestamp with time zone NULL,
                "DeletedBy"    uuid                     NULL,
                CONSTRAINT "PK_TreatmentPackages" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_TreatmentPackages_Name_IsActive"
                ON "TreatmentPackages" ("Name", "IsActive");
            """);

        // ── Appointments: companion + color + package columns ─────────────────
        migrationBuilder.Sql("""
            ALTER TABLE "Appointments"
                ADD COLUMN IF NOT EXISTS "CompanionName"         character varying(150) NULL,
                ADD COLUMN IF NOT EXISTS "CompanionPhone"        character varying(30)  NULL,
                ADD COLUMN IF NOT EXISTS "CompanionRelationship" character varying(50)  NULL,
                ADD COLUMN IF NOT EXISTS "AppointmentColor"      character varying(20)  NULL,
                ADD COLUMN IF NOT EXISTS "PackageId"             uuid                   NULL;

            CREATE INDEX IF NOT EXISTS "IX_Appointments_PackageId"
                ON "Appointments" ("PackageId");
            """);

        // FK: Appointments.PackageId → TreatmentPackages.Id (ON DELETE SET NULL — soft-deleting
        // a package must not break historical appointments that referenced it).
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_Appointments_TreatmentPackages_PackageId'
                ) THEN
                    ALTER TABLE "Appointments"
                        ADD CONSTRAINT "FK_Appointments_TreatmentPackages_PackageId"
                        FOREIGN KEY ("PackageId") REFERENCES "TreatmentPackages"("Id")
                        ON DELETE SET NULL;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop FK first, then index, then columns, then table.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_Appointments_TreatmentPackages_PackageId'
                ) THEN
                    ALTER TABLE "Appointments"
                        DROP CONSTRAINT "FK_Appointments_TreatmentPackages_PackageId";
                END IF;
            END $$;

            DROP INDEX IF EXISTS "IX_Appointments_PackageId";

            ALTER TABLE "Appointments"
                DROP COLUMN IF EXISTS "PackageId",
                DROP COLUMN IF EXISTS "AppointmentColor",
                DROP COLUMN IF EXISTS "CompanionRelationship",
                DROP COLUMN IF EXISTS "CompanionPhone",
                DROP COLUMN IF EXISTS "CompanionName";

            DROP TABLE IF EXISTS "TreatmentPackages";
            """);
    }
}
