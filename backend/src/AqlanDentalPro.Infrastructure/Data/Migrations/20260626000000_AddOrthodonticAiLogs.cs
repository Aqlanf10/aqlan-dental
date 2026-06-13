using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Ceph batch C-D — audit log for the orthodontic LLM draft-diagnosis assistant.
///
/// Creates the OrthodonticAiLogs table: one row per LLM call attempt
/// (success and failure) with a counts-only InputSummary (never patient
/// identifiers) and a short ErrorSummary (never secrets or exception dumps),
/// plus an index on AnalysisId.
///
/// Everything is idempotent raw SQL (CREATE TABLE IF NOT EXISTS / guarded drop),
/// matching the established hand-written migration pattern in this project.
/// </summary>
public partial class AddOrthodonticAiLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""OrthodonticAiLogs"" (
    ""Id"" uuid NOT NULL,
    ""AnalysisId"" uuid NOT NULL,
    ""UserId"" uuid NULL,
    ""Action"" character varying(50) NOT NULL,
    ""ModelId"" character varying(100) NULL,
    ""Succeeded"" boolean NOT NULL DEFAULT false,
    ""ErrorSummary"" character varying(300) NULL,
    ""InputSummary"" character varying(300) NULL,
    ""OutputLength"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""DeletedAt"" timestamp with time zone NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_OrthodonticAiLogs"" PRIMARY KEY (""Id"")
);
");

        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_OrthodonticAiLogs_AnalysisId"" ON ""OrthodonticAiLogs"" (""AnalysisId"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_OrthodonticAiLogs_AnalysisId"";");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF to_regclass('public.""OrthodonticAiLogs""') IS NOT NULL THEN
        DROP TABLE ""OrthodonticAiLogs"";
    END IF;
END $$;
");
    }
}
