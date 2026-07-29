using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddCephVtoScenarios : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CephVtoScenarios",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CephAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                ScenarioGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                UpperIncisorMoveMm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                LowerIncisorMoveMm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                OverjetBeforeMm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                OverjetAfterMm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephVtoScenarios", x => x.Id);
                table.ForeignKey(
                    name: "FK_CephVtoScenarios_CephAnalyses_CephAnalysisId",
                    column: x => x.CephAnalysisId,
                    principalTable: "CephAnalyses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CephVtoScenarios_CephAnalysisId",
            table: "CephVtoScenarios",
            column: "CephAnalysisId");

        migrationBuilder.CreateIndex(
            name: "IX_CephVtoScenarios_CephAnalysisId_ScenarioGroupId_VersionNumber",
            table: "CephVtoScenarios",
            columns: new[] { "CephAnalysisId", "ScenarioGroupId", "VersionNumber" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CephVtoScenarios");
    }
}
