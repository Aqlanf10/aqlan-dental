using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// TD-020 Phase C1-b — Convert B3/B8/B9 Program.cs raw SQL blocks to an idempotent EF migration.
///
/// This migration adds NormalizedPhone and NormalizedWhatsApp columns to the Patients table
/// and creates the corresponding unique filtered indexes — safely guarded so that it is a no-op
/// on any production database where the old Program.cs startup block already applied these changes.
///
/// NOTE: Data backfill (B4/B5) and deduplication (B6/B7) remain in Program.cs
/// and are out of scope for this migration.
/// </summary>
[Migration("20260523000000_AddPatientNormalizedPhoneFieldsAndIndexes")]
public partial class AddPatientNormalizedPhoneFieldsAndIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A. Add NormalizedPhone column — idempotent: skipped if column already exists.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone'
                   ) THEN
                    ALTER TABLE "Patients" ADD COLUMN "NormalizedPhone" character varying(20) NULL;
                END IF;
            END $$;
            """);

        // B. Add NormalizedWhatsApp column — idempotent: skipped if column already exists.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Patients' AND column_name = 'NormalizedWhatsApp'
                   ) THEN
                    ALTER TABLE "Patients" ADD COLUMN "NormalizedWhatsApp" character varying(20) NULL;
                END IF;
            END $$;
            """);

        // C. Create unique filtered index for NormalizedPhone — idempotent via IF NOT EXISTS.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedPhone"
                ON "Patients" ("NormalizedPhone")
                WHERE "NormalizedPhone" IS NOT NULL AND "NormalizedPhone" != '';
            """);

        // D. Create unique filtered index for NormalizedWhatsApp — idempotent via IF NOT EXISTS.
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Patients_NormalizedWhatsApp"
                ON "Patients" ("NormalizedWhatsApp")
                WHERE "NormalizedWhatsApp" IS NOT NULL AND "NormalizedWhatsApp" != '';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no-op.
        // NormalizedPhone/NormalizedWhatsApp may have existed before this migration
        // due to legacy Program.cs runtime schema maintenance.
        // Dropping them could remove existing normalized phone data/schema.
    }
}
