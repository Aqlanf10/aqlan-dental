using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoOrthoDiagnosisSyncMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FrontalSourceAnalysisId",
                table: "OrthoDiagnoses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoAnalysisSummary",
                table: "OrthoDiagnoses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhotoAnalysisSyncedAt",
                table: "OrthoDiagnoses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProfileSourceAnalysisId",
                table: "OrthoDiagnoses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrthoDiagnoses_FrontalSourceAnalysisId",
                table: "OrthoDiagnoses",
                column: "FrontalSourceAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_OrthoDiagnoses_ProfileSourceAnalysisId",
                table: "OrthoDiagnoses",
                column: "ProfileSourceAnalysisId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrthoDiagnoses_PhotoAnalyses_FrontalSourceAnalysisId",
                table: "OrthoDiagnoses",
                column: "FrontalSourceAnalysisId",
                principalTable: "PhotoAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrthoDiagnoses_PhotoAnalyses_ProfileSourceAnalysisId",
                table: "OrthoDiagnoses",
                column: "ProfileSourceAnalysisId",
                principalTable: "PhotoAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrthoDiagnoses_PhotoAnalyses_FrontalSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropForeignKey(
                name: "FK_OrthoDiagnoses_PhotoAnalyses_ProfileSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropIndex(
                name: "IX_OrthoDiagnoses_FrontalSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropIndex(
                name: "IX_OrthoDiagnoses_ProfileSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropColumn(
                name: "FrontalSourceAnalysisId",
                table: "OrthoDiagnoses");

            migrationBuilder.DropColumn(
                name: "PhotoAnalysisSummary",
                table: "OrthoDiagnoses");

            migrationBuilder.DropColumn(
                name: "PhotoAnalysisSyncedAt",
                table: "OrthoDiagnoses");

            migrationBuilder.DropColumn(
                name: "ProfileSourceAnalysisId",
                table: "OrthoDiagnoses");
        }
    }
}
