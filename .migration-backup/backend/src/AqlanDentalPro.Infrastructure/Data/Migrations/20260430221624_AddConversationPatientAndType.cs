using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds ConversationType and PatientId columns to Conversations.
/// NOTE: This overlaps with migration 20260501010000_AddPatientConversationSupport.
/// Converted to idempotent raw SQL to avoid "column already exists" errors.
/// </summary>
public partial class AddConversationPatientAndType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
        -- Add ConversationType column if not exists
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""ConversationType"" text NOT NULL DEFAULT '';
        END IF;

        -- Add PatientId column if not exists
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""PatientId"" uuid NULL;
        END IF;

        -- Index on PatientId
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_PatientId') THEN
            CREATE INDEX ""IX_Conversations_PatientId"" ON ""Conversations"" (""PatientId"");
        END IF;

        -- FK: Conversations → Patients
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients') THEN
                ALTER TABLE ""Conversations""
                    ADD CONSTRAINT ""FK_Conversations_Patients_PatientId""
                    FOREIGN KEY (""PatientId"") REFERENCES ""Patients""(""Id"") ON DELETE SET NULL;
            END IF;
        END IF;
    END IF;
END $$;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
            ALTER TABLE ""Conversations"" DROP CONSTRAINT ""FK_Conversations_Patients_PatientId"";
        END IF;
        IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_PatientId') THEN
            DROP INDEX ""IX_Conversations_PatientId"";
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
            ALTER TABLE ""Conversations"" DROP COLUMN ""PatientId"";
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
            ALTER TABLE ""Conversations"" DROP COLUMN ""ConversationType"";
        END IF;
    END IF;
END $$;
");
    }
}
