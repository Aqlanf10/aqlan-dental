using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds EmailLog table for tracking outgoing emails (statistics, daily limits, debugging).
///
/// Up: Idempotent — uses IF NOT EXISTS because AddCentralFinanceV2Hub (20260525092924)
/// may have already created the EmailLogs table on databases where Finance V2 was applied first.
///
/// Down: No-op for the EmailLogs table. The EmailLogs table is owned by the earlier
/// AddCentralFinanceV2Hub migration (20260525092924) which is the schema owner.
/// Rolling back this migration while AddCentralFinanceV2Hub remains applied must NOT
/// drop the table still required by Finance V2. AddCentralFinanceV2Hub's own Down
/// method handles the cleanup when it is rolled back.
///
/// Deployment-history compatibility: On databases where this migration created EmailLogs
/// before Finance V2 was merged, AddCentralFinanceV2Hub's Up would have been a no-op
/// (IF NOT EXISTS). Rolling back this migration while Finance V2 is still applied would
/// incorrectly remove the table if we dropped it here. The no-op Down avoids this risk.
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
        // NO-OP: EmailLogs table is owned by AddCentralFinanceV2Hub (20260525092924).
        // Rolling back this migration alone must not drop the table that Finance V2
        // still depends on. AddCentralFinanceV2Hub's Down handles cleanup.
        //
        // If both migrations need to be rolled back, EF Core applies Down in reverse
        // chronological order: this Down (no-op) runs first, then
        // AddCentralFinanceV2Hub's Down drops the table.
    }
}
