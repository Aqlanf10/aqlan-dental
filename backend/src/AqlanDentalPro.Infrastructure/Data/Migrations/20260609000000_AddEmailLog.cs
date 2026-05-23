using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds EmailLog table for tracking outgoing emails (statistics, daily limits, debugging).
/// </summary>
public partial class AddEmailLog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'EmailLogs') THEN
        CREATE TABLE ""EmailLogs"" (
            ""Id""            UUID PRIMARY KEY,
            ""ToEmail""       TEXT NOT NULL,
            ""Subject""       TEXT NOT NULL,
            ""Category""      TEXT NOT NULL DEFAULT 'general',
            ""Provider""      TEXT NULL,
            ""IsSent""        BOOLEAN NOT NULL DEFAULT FALSE,
            ""ErrorMessage""  TEXT NULL,
            ""ExternalId""    TEXT NULL,
            ""RelatedEntityType"" TEXT NULL,
            ""RelatedEntityId""   UUID NULL,
            ""CreatedAt""     TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        -- Index for daily count queries (most common: sent today)
        CREATE INDEX ""IX_EmailLogs_IsSent_CreatedAt"" ON ""EmailLogs"" (""IsSent"", ""CreatedAt"");

        -- Index for filtering by category
        CREATE INDEX ""IX_EmailLogs_Category_CreatedAt"" ON ""EmailLogs"" (""Category"", ""CreatedAt"");

        -- Index for related entity lookups
        CREATE INDEX ""IX_EmailLogs_RelatedEntity"" ON ""EmailLogs"" (""RelatedEntityType"", ""RelatedEntityId"");
    END IF;
END $$;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""EmailLogs"";");
    }
}
