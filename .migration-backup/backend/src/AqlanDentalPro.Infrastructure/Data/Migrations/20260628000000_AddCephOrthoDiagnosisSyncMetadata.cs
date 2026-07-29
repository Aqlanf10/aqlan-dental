using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCephOrthoDiagnosisSyncMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CephSourceAnalysisId",
                table: "OrthoDiagnoses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CephSyncedAt",
                table: "OrthoDiagnoses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrthoDiagnoses_CephSourceAnalysisId",
                table: "OrthoDiagnoses",
                column: "CephSourceAnalysisId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrthoDiagnoses_CephAnalyses_CephSourceAnalysisId",
                table: "OrthoDiagnoses",
                column: "CephSourceAnalysisId",
                principalTable: "CephAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrthoDiagnoses_CephAnalyses_CephSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropIndex(
                name: "IX_OrthoDiagnoses_CephSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropColumn(
                name: "CephSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropColumn(
                name: "CephSyncedAt",
                table: "OrthoDiagnoses");
        }
    }
}
