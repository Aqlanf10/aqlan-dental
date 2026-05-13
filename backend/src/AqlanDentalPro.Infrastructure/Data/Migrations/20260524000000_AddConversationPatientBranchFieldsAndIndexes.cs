using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// TD-020 Phase C1-d — Convert B10/B11/B12/B13 Program.cs raw SQL blocks to an idempotent EF migration.
///
/// Adds ConversationType, PatientId, BranchId columns to the Conversations table,
/// creates the corresponding indexes, and adds the PatientId foreign key —
/// safely guarded so that it is a no-op on any database where these already exist.
/// </summary>
[Migration("20260524000000_AddConversationPatientBranchFieldsAndIndexes")]
public partial class AddConversationPatientBranchFieldsAndIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A. Add ConversationType column — idempotent: skipped if column already exists.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Conversations' AND column_name = 'ConversationType'
                   ) THEN
                    ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                END IF;
            END $$;
            """);

        // B. Add PatientId column — idempotent: skipped if column already exists.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Conversations' AND column_name = 'PatientId'
                   ) THEN
                    ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                END IF;
            END $$;
            """);

        // C. Add BranchId column — idempotent: skipped if column already exists.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND NOT EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Conversations' AND column_name = 'BranchId'
                   ) THEN
                    ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                END IF;
            END $$;
            """);

        // D. Create index on PatientId — idempotent: skipped if Conversations table does not exist.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
                    CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId"
                        ON "Conversations" ("PatientId");
                END IF;
            END $$;
            """);

        // E. Create index on ConversationType — idempotent: skipped if Conversations table does not exist.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
                    CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType"
                        ON "Conversations" ("ConversationType");
                END IF;
            END $$;
            """);

        // F. Add FK from Conversations.PatientId to Patients.Id — fully guarded:
        //    requires Conversations table, Patients table, PatientId column, and absent constraint.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')
                   AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients')
                   AND EXISTS (
                       SELECT 1 FROM information_schema.columns
                       WHERE table_name = 'Conversations' AND column_name = 'PatientId'
                   )
                   AND NOT EXISTS (
                       SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId'
                   ) THEN
                    ALTER TABLE "Conversations"
                        ADD CONSTRAINT "FK_Conversations_Patients_PatientId"
                        FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE SET NULL;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no-op.
        // ConversationType/PatientId/BranchId and their indexes/FK may have existed before
        // this migration due to legacy Program.cs B10-B13 runtime maintenance or earlier
        // EF migrations (20260430221624, 20260501010000). Dropping them could break
        // active messaging and conversation features.
    }
}
