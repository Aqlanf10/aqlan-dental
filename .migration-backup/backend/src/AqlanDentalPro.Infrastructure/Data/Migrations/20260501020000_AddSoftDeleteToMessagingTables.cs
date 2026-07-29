using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// إضافة أعمدة الحذف الناعم (DeletedAt, DeletedBy) لجداول المراسلة
/// Migration 20260501020000
/// Fixed: converted from non-standard $soft_delete_{tableName}$ dollar-quoting to standard $$.
/// </summary>
public partial class AddSoftDeleteToMessagingTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add DeletedAt and DeletedBy to Conversations (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "Conversations");

        // Add DeletedAt and DeletedBy to ConversationParticipants (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "ConversationParticipants");

        // Add DeletedAt and DeletedBy to Messages (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "Messages");

        // Add DeletedAt and DeletedBy to MessageReads (if not exists)
        AddSoftDeleteColumns(migrationBuilder, "MessageReads");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedBy') THEN
        ALTER TABLE ""MessageReads"" DROP COLUMN ""DeletedBy"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'MessageReads' AND column_name = 'DeletedAt') THEN
        ALTER TABLE ""MessageReads"" DROP COLUMN ""DeletedAt"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedBy') THEN
        ALTER TABLE ""Messages"" DROP COLUMN ""DeletedBy"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
        ALTER TABLE ""Messages"" DROP COLUMN ""DeletedAt"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedBy') THEN
        ALTER TABLE ""ConversationParticipants"" DROP COLUMN ""DeletedBy"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'ConversationParticipants' AND column_name = 'DeletedAt') THEN
        ALTER TABLE ""ConversationParticipants"" DROP COLUMN ""DeletedAt"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedBy') THEN
        ALTER TABLE ""Conversations"" DROP COLUMN ""DeletedBy"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedAt') THEN
        ALTER TABLE ""Conversations"" DROP COLUMN ""DeletedAt"";
    END IF;
END $$;
");
    }

    /// <summary>
    /// Adds DeletedAt and DeletedBy columns to a table using idempotent SQL.
    /// Uses standard $$ dollar-quoting for PostgreSQL DO blocks.
    /// </summary>
    private static void AddSoftDeleteColumns(MigrationBuilder migrationBuilder, string tableName)
    {
        migrationBuilder.Sql($@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{tableName}') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{tableName}' AND column_name = 'DeletedAt') THEN
            ALTER TABLE ""{tableName}"" ADD COLUMN ""DeletedAt"" timestamp with time zone NULL;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{tableName}' AND column_name = 'DeletedBy') THEN
            ALTER TABLE ""{tableName}"" ADD COLUMN ""DeletedBy"" uuid NULL;
        END IF;
    END IF;
END $$;
");
    }
}
