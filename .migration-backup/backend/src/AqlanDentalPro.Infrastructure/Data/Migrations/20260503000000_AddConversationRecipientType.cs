using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds RecipientType and RecipientUserId columns to Conversations.
/// Converted to idempotent raw SQL with IF NOT EXISTS guards.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260503000000_AddConversationRecipientType")]
public partial class AddConversationRecipientType : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations') THEN
        -- Add RecipientType column if not exists
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'RecipientType') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""RecipientType"" character varying(20) NULL;
        END IF;

        -- Add RecipientUserId column if not exists
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'RecipientUserId') THEN
            ALTER TABLE ""Conversations"" ADD COLUMN ""RecipientUserId"" uuid NULL;
        END IF;

        -- Index on RecipientType
        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_RecipientType') THEN
            CREATE INDEX ""IX_Conversations_RecipientType"" ON ""Conversations"" (""RecipientType"");
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
        IF EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'Conversations' AND indexname = 'IX_Conversations_RecipientType') THEN
            DROP INDEX ""IX_Conversations_RecipientType"";
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'RecipientUserId') THEN
            ALTER TABLE ""Conversations"" DROP COLUMN ""RecipientUserId"";
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'RecipientType') THEN
            ALTER TABLE ""Conversations"" DROP COLUMN ""RecipientType"";
        END IF;
    END IF;
END $$;
");
    }
}
