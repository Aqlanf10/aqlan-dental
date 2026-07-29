using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260717000000_AddCephLandmarkProvenance")]
public partial class AddCephLandmarkProvenance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The startup compatibility repair mirrors these columns. Use guarded
        // DDL so EF can record the migration after a partial production repair.
        migrationBuilder.Sql("""
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "AiProposalXCoord" numeric(12,4) NULL;
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "AiProposalYCoord" numeric(12,4) NULL;
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "IsReviewed" boolean NOT NULL DEFAULT false;
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "PlacementSource" character varying(30) NOT NULL DEFAULT 'manual';
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "Reasoning" character varying(200) NULL;
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "ReviewErrorMm" numeric(10,4) NULL;
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "SourceLandmarkKey" character varying(100) NULL;
            ALTER TABLE "CephLandmarks" ADD COLUMN IF NOT EXISTS "SourceModelId" character varying(100) NULL;
            CREATE INDEX IF NOT EXISTS "IX_CephLandmarks_SourceModelId_IsReviewed_IsActive"
                ON "CephLandmarks" ("SourceModelId", "IsReviewed", "IsActive");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CephLandmarks_SourceModelId_IsReviewed_IsActive",
            table: "CephLandmarks");

        migrationBuilder.DropColumn(name: "AiProposalXCoord", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "AiProposalYCoord", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "IsReviewed", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "PlacementSource", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "Reasoning", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "ReviewErrorMm", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "SourceLandmarkKey", table: "CephLandmarks");
        migrationBuilder.DropColumn(name: "SourceModelId", table: "CephLandmarks");
    }
}
