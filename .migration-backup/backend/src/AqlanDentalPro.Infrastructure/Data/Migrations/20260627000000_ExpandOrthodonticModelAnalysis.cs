using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandOrthodonticModelAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve any lab links created while EF used the old shadow FK,
            // then remove that compatibility column without losing data.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = current_schema()
                          AND table_name = 'InvoiceLineItems'
                          AND column_name = 'LabOrderId1'
                    ) THEN
                        UPDATE "InvoiceLineItems"
                        SET "LabOrderId" = COALESCE("LabOrderId", "LabOrderId1")
                        WHERE "LabOrderId1" IS NOT NULL;

                        ALTER TABLE "InvoiceLineItems"
                            DROP CONSTRAINT IF EXISTS "FK_InvoiceLineItems_LabOrders_LabOrderId1";
                        DROP INDEX IF EXISTS "IX_InvoiceLineItems_LabOrderId1";
                        ALTER TABLE "InvoiceLineItems" DROP COLUMN "LabOrderId1";
                    END IF;
                END $$;

                DROP INDEX IF EXISTS "IX_ModelAnalyses_OrthoCaseId";
                """);

            migrationBuilder.AddColumn<string>(
                name: "AnalysisVersion",
                table: "ModelAnalyses",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "2.0");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "ModelAnalyses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                table: "ModelAnalyses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DentitionStage",
                table: "ModelAnalyses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Permanent");

            migrationBuilder.AddColumn<string>(
                name: "InputDataJson",
                table: "ModelAnalyses",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ResultDataJson",
                table: "ModelAnalyses",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateIndex(
                name: "IX_ModelAnalyses_OrthoCaseId_AnalysisDate",
                table: "ModelAnalyses",
                columns: new[] { "OrthoCaseId", "AnalysisDate" });

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_InvoiceLineItems_LabOrderId"
                    ON "InvoiceLineItems" ("LabOrderId");

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId'
                    ) THEN
                        ALTER TABLE "InvoiceLineItems"
                            ADD CONSTRAINT "FK_InvoiceLineItems_LabOrders_LabOrderId"
                            FOREIGN KEY ("LabOrderId") REFERENCES "LabOrders" ("Id")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "InvoiceLineItems"
                    DROP CONSTRAINT IF EXISTS "FK_InvoiceLineItems_LabOrders_LabOrderId";
                DROP INDEX IF EXISTS "IX_InvoiceLineItems_LabOrderId";
                """);

            migrationBuilder.DropIndex(
                name: "IX_ModelAnalyses_OrthoCaseId_AnalysisDate",
                table: "ModelAnalyses");

            migrationBuilder.DropColumn(
                name: "AnalysisVersion",
                table: "ModelAnalyses");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "ModelAnalyses");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                table: "ModelAnalyses");

            migrationBuilder.DropColumn(
                name: "DentitionStage",
                table: "ModelAnalyses");

            migrationBuilder.DropColumn(
                name: "InputDataJson",
                table: "ModelAnalyses");

            migrationBuilder.DropColumn(
                name: "ResultDataJson",
                table: "ModelAnalyses");

            migrationBuilder.CreateIndex(
                name: "IX_ModelAnalyses_OrthoCaseId",
                table: "ModelAnalyses",
                column: "OrthoCaseId");

            migrationBuilder.Sql(
                """
                ALTER TABLE "InvoiceLineItems"
                    ADD COLUMN IF NOT EXISTS "LabOrderId1" uuid NULL;

                UPDATE "InvoiceLineItems"
                SET "LabOrderId1" = "LabOrderId"
                WHERE "LabOrderId" IS NOT NULL;

                CREATE INDEX IF NOT EXISTS "IX_InvoiceLineItems_LabOrderId1"
                    ON "InvoiceLineItems" ("LabOrderId1");

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId1'
                    ) THEN
                        ALTER TABLE "InvoiceLineItems"
                            ADD CONSTRAINT "FK_InvoiceLineItems_LabOrders_LabOrderId1"
                            FOREIGN KEY ("LabOrderId1") REFERENCES "LabOrders" ("Id");
                    END IF;
                END $$;
                """);
        }
    }
}
