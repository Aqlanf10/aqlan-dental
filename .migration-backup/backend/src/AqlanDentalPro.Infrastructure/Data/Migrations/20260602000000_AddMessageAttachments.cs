using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds MessageAttachments table for multi-attachment support on messages.
/// Converted to idempotent raw SQL with IF NOT EXISTS guards.
/// Backward compatible: existing single-attachment fields on Message are retained.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260602000000_AddMessageAttachments")]
public partial class AddMessageAttachments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- Create MessageAttachments table if not present
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'MessageAttachments'
    ) THEN
        CREATE TABLE ""MessageAttachments"" (
            ""Id""        uuid                     NOT NULL DEFAULT gen_random_uuid(),
            ""MessageId"" uuid                     NOT NULL,
            ""FileUrl""   character varying(1000)  NOT NULL,
            ""FileName""  character varying(255)   NOT NULL,
            ""FileSize""  bigint                   NOT NULL DEFAULT 0,
            ""MimeType""  character varying(100)   NOT NULL,
            ""IsActive""  boolean                  NOT NULL DEFAULT true,
            ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
            ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
            ""DeletedAt"" timestamp with time zone NULL,
            ""DeletedBy"" uuid                     NULL,
            CONSTRAINT ""PK_MessageAttachments"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- FK: MessageAttachments → Messages
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'FK_MessageAttachments_Messages_MessageId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'Messages'
        ) THEN
            ALTER TABLE ""MessageAttachments""
                ADD CONSTRAINT ""FK_MessageAttachments_Messages_MessageId""
                FOREIGN KEY (""MessageId"")
                REFERENCES ""Messages""(""Id"")
                ON DELETE CASCADE;
        END IF;
    END IF;

    -- Index on MessageId
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public'
          AND tablename  = 'MessageAttachments'
          AND indexname  = 'IX_MessageAttachments_MessageId'
    ) THEN
        CREATE INDEX ""IX_MessageAttachments_MessageId""
            ON ""MessageAttachments"" (""MessageId"");
    END IF;
END $$;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""MessageAttachments"";");
    }
}
