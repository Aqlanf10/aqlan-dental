using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerioAssessmentDetailFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonDocument>(
                name: "PocketDepths",
                table: "PerioAssessments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "BleedingOnProbing",
                table: "PerioAssessments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Mobility",
                table: "PerioAssessments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "FurcationInvolvement",
                table: "PerioAssessments",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlaqueIndex",
                table: "PerioAssessments",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GingivalIndex",
                table: "PerioAssessments",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AttachmentLoss",
                table: "PerioAssessments",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BoneLossPattern",
                table: "PerioAssessments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TreatmentPlan",
                table: "PerioAssessments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PocketDepths",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "BleedingOnProbing",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "Mobility",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "FurcationInvolvement",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "PlaqueIndex",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "GingivalIndex",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "AttachmentLoss",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "BoneLossPattern",
                table: "PerioAssessments");

            migrationBuilder.DropColumn(
                name: "TreatmentPlan",
                table: "PerioAssessments");
        }
    }
}
