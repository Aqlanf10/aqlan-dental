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
        migrationBuilder.AddColumn<decimal>(
            name: "AiProposalXCoord",
            table: "CephLandmarks",
            type: "numeric(12,4)",
            precision: 12,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "AiProposalYCoord",
            table: "CephLandmarks",
            type: "numeric(12,4)",
            precision: 12,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsReviewed",
            table: "CephLandmarks",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "PlacementSource",
            table: "CephLandmarks",
            type: "character varying(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "manual");

        migrationBuilder.AddColumn<string>(
            name: "Reasoning",
            table: "CephLandmarks",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ReviewErrorMm",
            table: "CephLandmarks",
            type: "numeric(10,4)",
            precision: 10,
            scale: 4,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceLandmarkKey",
            table: "CephLandmarks",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SourceModelId",
            table: "CephLandmarks",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CephLandmarks_SourceModelId_IsReviewed_IsActive",
            table: "CephLandmarks",
            columns: new[] { "SourceModelId", "IsReviewed", "IsActive" });
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
