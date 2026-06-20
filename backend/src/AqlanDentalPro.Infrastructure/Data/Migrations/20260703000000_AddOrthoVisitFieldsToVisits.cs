using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// CLIN-05: Adds WireUpper/WireLower/CurrentStage (nullable text) to Visits so
    /// OrthoService.AddVisitAsync can mirror the OrthoVisit's clinical fields onto
    /// the linked Visit. Daily-operations reads Visit directly — these columns are
    /// the bridge that surfaces ortho clinical notes in the daily-operations screen
    /// without a separate OrthoVisits join.
    /// </summary>
    public partial class AddOrthoVisitFieldsToVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WireUpper",
                table: "Visits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WireLower",
                table: "Visits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStage",
                table: "Visits",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WireUpper",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "WireLower",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "CurrentStage",
                table: "Visits");
        }
    }
}
