using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260714042447_AddCephAiModelLineage")]
public partial class AddCephAiModelLineage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CephAiModelVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RegistryKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ModelId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                ModelVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PreprocessingVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                DatasetVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                LandmarkDefinitionVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                IdentitySha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                ArtifactSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ValidationEvidenceId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                StatisticsApprovalId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                ClinicalApprovalId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_CephAiModelVersions", item => item.Id));

        migrationBuilder.CreateTable(
            name: "CephAiModelDeployments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                ActiveModelVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousModelVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                PinnedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                PinnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephAiModelDeployments", item => item.Id);
                table.ForeignKey(
                    name: "FK_CephAiModelDeployments_CephAiModelVersions_ActiveModelVersionId",
                    column: item => item.ActiveModelVersionId,
                    principalTable: "CephAiModelVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CephAiModelDeployments_CephAiModelVersions_PreviousModelVersionId",
                    column: item => item.PreviousModelVersionId,
                    principalTable: "CephAiModelVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CephAiInferenceRuns",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                ModelVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                Operation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Precision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ImageWidth = table.Column<int>(type: "integer", nullable: false),
                ImageHeight = table.Column<int>(type: "integer", nullable: false),
                ImageSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                OriginalPredictionsJson = table.Column<string>(type: "jsonb", nullable: true),
                OutputSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                TriggeredByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephAiInferenceRuns", item => item.Id);
                table.ForeignKey(
                    name: "FK_CephAiInferenceRuns_CephAiModelVersions_ModelVersionId",
                    column: item => item.ModelVersionId,
                    principalTable: "CephAiModelVersions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CephAiInferenceRuns_CephAnalyses_AnalysisId",
                    column: item => item.AnalysisId,
                    principalTable: "CephAnalyses",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "SourceInferenceRunId",
            table: "CephLandmarks",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CephAiModelVersions_RegistryKey",
            table: "CephAiModelVersions",
            column: "RegistryKey",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_CephAiModelVersions_IdentitySha256",
            table: "CephAiModelVersions",
            column: "IdentitySha256",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_CephAiModelVersions_Status_Provider_ModelId",
            table: "CephAiModelVersions",
            columns: new[] { "Status", "Provider", "ModelId" });
        migrationBuilder.CreateIndex(
            name: "IX_CephAiModelDeployments_Slot",
            table: "CephAiModelDeployments",
            column: "Slot",
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_CephAiModelDeployments_ActiveModelVersionId",
            table: "CephAiModelDeployments",
            column: "ActiveModelVersionId");
        migrationBuilder.CreateIndex(
            name: "IX_CephAiModelDeployments_PreviousModelVersionId",
            table: "CephAiModelDeployments",
            column: "PreviousModelVersionId");
        migrationBuilder.CreateIndex(
            name: "IX_CephAiInferenceRuns_AnalysisId_StartedAt",
            table: "CephAiInferenceRuns",
            columns: new[] { "AnalysisId", "StartedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_CephAiInferenceRuns_ModelVersionId_Status_StartedAt",
            table: "CephAiInferenceRuns",
            columns: new[] { "ModelVersionId", "Status", "StartedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_CephLandmarks_SourceInferenceRunId",
            table: "CephLandmarks",
            column: "SourceInferenceRunId");
        migrationBuilder.AddForeignKey(
            name: "FK_CephLandmarks_CephAiInferenceRuns_SourceInferenceRunId",
            table: "CephLandmarks",
            column: "SourceInferenceRunId",
            principalTable: "CephAiInferenceRuns",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_CephLandmarks_CephAiInferenceRuns_SourceInferenceRunId",
            table: "CephLandmarks");
        migrationBuilder.DropIndex(
            name: "IX_CephLandmarks_SourceInferenceRunId",
            table: "CephLandmarks");
        migrationBuilder.DropColumn(
            name: "SourceInferenceRunId",
            table: "CephLandmarks");
        migrationBuilder.DropTable(name: "CephAiInferenceRuns");
        migrationBuilder.DropTable(name: "CephAiModelDeployments");
        migrationBuilder.DropTable(name: "CephAiModelVersions");
    }
}
