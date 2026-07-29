using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// إضافة دعم محادثات المرضى: ConversationType, PatientId, BranchId
/// Converted to idempotent raw SQL with IF NOT EXISTS guards.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260501010000_AddPatientConversationSupport")]
public partial class AddPatientConversationSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- Add ConversationType column if not exists
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""ConversationType"" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
        END IF;

        -- Add PatientId column if not exists
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""PatientId"" uuid NULL;
        END IF;

        -- Add BranchId column if not exists
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""BranchId"" uuid NULL;
        END IF;

        -- Index on PatientId
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_PatientId') THEN
            CREATE INDEX ""IX_Conversations_PatientId"" ON ""Conversations"" (""PatientId"");
        END IF;

        -- Index on ConversationType
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_ConversationType') THEN
            CREATE INDEX ""IX_Conversations_ConversationType"" ON ""Conversations"" (""ConversationType"");
        END IF;

        -- FK: Conversations → Patients
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Patients') THEN
                ALTER TABLE ""Conversations""
                    ADD CONSTRAINT ""FK_Conversations_Patients_PatientId""
                    FOREIGN KEY (""PatientId"") REFERENCES ""Patients""(""Id"") ON DELETE SET NULL;
            END IF;
        END IF;

        -- FK: Conversations → Branches
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Branches_BranchId') THEN
            IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Branches') THEN
                ALTER TABLE ""Conversations""
                    ADD CONSTRAINT ""FK_Conversations_Branches_BranchId""
                    FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE SET NULL;
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
        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Branches_BranchId') THEN
            ALTER TABLE ""Conversations"" DROP CONSTRAINT ""FK_Conversations_Branches_BranchId"";
        END IF;
        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
            ALTER TABLE ""Conversations"" DROP CONSTRAINT ""FK_Conversations_Patients_PatientId"";
        END IF;
        IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_ConversationType') THEN
            DROP INDEX ""IX_Conversations_ConversationType"";
        END IF;
        IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_PatientId') THEN
            DROP INDEX ""IX_Conversations_PatientId"";
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
            ALTER TABLE ""Conversations"" DROP COLUMN ""BranchId"";
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
