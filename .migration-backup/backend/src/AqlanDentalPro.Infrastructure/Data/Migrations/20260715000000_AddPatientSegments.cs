using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// YOLO-S5: Patient segments.
///
/// Creates:
///   - PatientSegments table:
///       Name (varchar 200, required)
///       Description (varchar 1000, nullable)
///       Color (varchar 20, nullable)
///       IsDynamic (boolean, required)
///       QueryJson (text, nullable — reserved for custom dynamic segments later)
///       + BaseEntity audit columns (CreatedAt/UpdatedAt/IsActive/DeletedAt/DeletedBy)
///   - PatientSegmentMembers table:
///       SegmentId, PatientId, AddedAt
///       Unique (SegmentId, PatientId) — a patient can be in a custom segment once
///       FK to PatientSegments ON DELETE CASCADE (segment delete removes members)
///       FK to Patients ON DELETE CASCADE (patient delete removes membership)
///
/// Rules honored:
///   - Idempotent (CREATE TABLE IF NOT EXISTS / ADD CONSTRAINT IF NOT EXISTS via
///     DO $$ ... END $$ block) — safe on databases where the C-08 startup hotfix
///     already created the tables. Mirrors the existing
///     20260712000000_AddAppointmentEnhancements pattern (raw SQL because the
///     EF migration chain is historically broken — see CLAUDE.md pitfall).
///   - Pre-built dynamic segments (ortho overdue, outstanding balance,
///     no recent visit, lab ready) are NOT stored — they are computed in
///     PatientSegmentsController at read time and identified by stable keys.
///
/// Down: drops both tables. Safe to roll back because no production data
/// dependencies yet (custom segments are user-created, can be recreated).
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260715000000_AddPatientSegments")]
public partial class AddPatientSegments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── PatientSegments table ────────────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "PatientSegments" (
                "Id"          uuid                     NOT NULL DEFAULT gen_random_uuid(),
                "Name"        character varying(200)   NOT NULL,
                "Description" character varying(1000)  NULL,
                "Color"       character varying(20)    NULL,
                "IsDynamic"   boolean                  NOT NULL DEFAULT false,
                "QueryJson"   text                     NULL,
                "CreatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt"   timestamp with time zone NOT NULL DEFAULT now(),
                "IsActive"    boolean                  NOT NULL DEFAULT true,
                "DeletedAt"   timestamp with time zone NULL,
                "DeletedBy"   uuid                     NULL,
                CONSTRAINT "PK_PatientSegments" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_PatientSegments_Name"
                ON "PatientSegments" ("Name");
            CREATE INDEX IF NOT EXISTS "IX_PatientSegments_IsActive"
                ON "PatientSegments" ("IsActive");
            """);

        // ── PatientSegmentMembers table ──────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "PatientSegmentMembers" (
                "Id"         uuid                     NOT NULL DEFAULT gen_random_uuid(),
                "SegmentId"  uuid                     NOT NULL,
                "PatientId"  uuid                     NOT NULL,
                "AddedAt"    timestamp with time zone NOT NULL DEFAULT now(),
                "CreatedAt"  timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt"  timestamp with time zone NOT NULL DEFAULT now(),
                "IsActive"   boolean                  NOT NULL DEFAULT true,
                "DeletedAt"  timestamp with time zone NULL,
                "DeletedBy"  uuid                     NULL,
                CONSTRAINT "PK_PatientSegmentMembers" PRIMARY KEY ("Id"),
                CONSTRAINT "UQ_PatientSegmentMembers_SegmentId_PatientId"
                    UNIQUE ("SegmentId", "PatientId")
            );

            CREATE INDEX IF NOT EXISTS "IX_PatientSegmentMembers_SegmentId"
                ON "PatientSegmentMembers" ("SegmentId");
            CREATE INDEX IF NOT EXISTS "IX_PatientSegmentMembers_PatientId"
                ON "PatientSegmentMembers" ("PatientId");

            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_PatientSegmentMembers_PatientSegments_SegmentId'
                ) THEN
                    ALTER TABLE "PatientSegmentMembers"
                        ADD CONSTRAINT "FK_PatientSegmentMembers_PatientSegments_SegmentId"
                        FOREIGN KEY ("SegmentId") REFERENCES "PatientSegments"("Id")
                        ON DELETE CASCADE;
                END IF;
            END $$;

            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_PatientSegmentMembers_Patients_PatientId'
                ) THEN
                    ALTER TABLE "PatientSegmentMembers"
                        ADD CONSTRAINT "FK_PatientSegmentMembers_Patients_PatientId"
                        FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id")
                        ON DELETE CASCADE;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "PatientSegmentMembers";
            DROP TABLE IF EXISTS "PatientSegments";
            """);
    }
}
