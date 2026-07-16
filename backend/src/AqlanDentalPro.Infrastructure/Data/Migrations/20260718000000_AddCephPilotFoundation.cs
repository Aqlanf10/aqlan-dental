using System;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260718000000_AddCephPilotFoundation")]
public partial class AddCephPilotFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CephPilotProjects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                LandmarkDefinitionVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                LockedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DatasetVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ReviewerAUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewerBUserId = table.Column<Guid>(type: "uuid", nullable: false),
                AdjudicatorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                IsAiHidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                IsWebCephHidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephPilotProjects", x => x.Id);
                table.ForeignKey("FK_CephPilotProjects_Users_AdjudicatorUserId", x => x.AdjudicatorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CephPilotProjects_Users_CreatedByUserId", x => x.CreatedByUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CephPilotProjects_Users_ReviewerAUserId", x => x.ReviewerAUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CephPilotProjects_Users_ReviewerBUserId", x => x.ReviewerBUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CephPilotCases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                CaseSequence = table.Column<int>(type: "integer", nullable: false),
                CaseCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SourceReference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                ImageStorageKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                ImageSha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                ImageWidth = table.Column<int>(type: "integer", nullable: false),
                ImageHeight = table.Column<int>(type: "integer", nullable: false),
                MmPerPixel = table.Column<decimal>(type: "numeric(12,8)", precision: 12, scale: 8, nullable: true),
                CalibrationSource = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                Orientation = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                SiteCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DeviceCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                PatientGroupToken = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                Split = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                QualityCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                SkeletalCategory = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                VerticalCategory = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                DeIdentificationConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PixelInspectionConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                NoBarcodeOrQrConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                LegalBasisConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                MetadataSanitized = table.Column<bool>(type: "boolean", nullable: false),
                OrientationConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                MigrationDecision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephPilotCases", x => x.Id);
                table.ForeignKey("FK_CephPilotCases_CephPilotProjects_ProjectId", x => x.ProjectId, "CephPilotProjects", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CephPilotExportArtifacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                ArtifactType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                StorageKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                IsComparatorOnly = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                ImportStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "staged"),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephPilotExportArtifacts", x => x.Id);
                table.ForeignKey("FK_CephPilotExportArtifacts_CephPilotCases_CaseId", x => x.CaseId, "CephPilotCases", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotProjects_DistinctReviewers",
            table: "CephPilotProjects",
            sql: "\"ReviewerAUserId\" <> \"ReviewerBUserId\" AND (\"AdjudicatorUserId\" IS NULL OR (\"AdjudicatorUserId\" <> \"ReviewerAUserId\" AND \"AdjudicatorUserId\" <> \"ReviewerBUserId\"))");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotProjects_RatifiedVersion",
            table: "CephPilotProjects",
            sql: "\"LandmarkDefinitionVersion\" = 'ADP-LM-LAT-v1.0'");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotProjects_BlindingEnabled",
            table: "CephPilotProjects",
            sql: "\"IsAiHidden\" = TRUE AND \"IsWebCephHidden\" = TRUE");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotCases_ReadyRequiresGate",
            table: "CephPilotCases",
            sql: "\"Status\" <> 'Ready' OR (\"MmPerPixel\" IS NOT NULL AND \"DeIdentificationConfirmed\" = TRUE AND \"PixelInspectionConfirmed\" = TRUE AND \"NoBarcodeOrQrConfirmed\" = TRUE AND \"LegalBasisConfirmed\" = TRUE AND \"MetadataSanitized\" = TRUE AND \"OrientationConfirmed\" = TRUE)");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotExportArtifacts_ComparatorOnly",
            table: "CephPilotExportArtifacts",
            sql: "\"IsComparatorOnly\" = TRUE");

        migrationBuilder.CreateIndex("IX_CephPilotProjects_AdjudicatorUserId", "CephPilotProjects", "AdjudicatorUserId");
        migrationBuilder.CreateIndex("IX_CephPilotProjects_Code", "CephPilotProjects", "Code", unique: true);
        migrationBuilder.CreateIndex("IX_CephPilotProjects_CreatedByUserId", "CephPilotProjects", "CreatedByUserId");
        migrationBuilder.CreateIndex("IX_CephPilotProjects_ReviewerAUserId_Status_IsActive", "CephPilotProjects", new[] { "ReviewerAUserId", "Status", "IsActive" });
        migrationBuilder.CreateIndex("IX_CephPilotProjects_ReviewerBUserId_Status_IsActive", "CephPilotProjects", new[] { "ReviewerBUserId", "Status", "IsActive" });
        migrationBuilder.CreateIndex("IX_CephPilotCases_ProjectId_CaseCode", "CephPilotCases", new[] { "ProjectId", "CaseCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_CephPilotCases_ProjectId_CaseSequence", "CephPilotCases", new[] { "ProjectId", "CaseSequence" }, unique: true);
        migrationBuilder.CreateIndex("IX_CephPilotCases_ProjectId_ImageSha256", "CephPilotCases", new[] { "ProjectId", "ImageSha256" }, unique: true);
        migrationBuilder.CreateIndex("IX_CephPilotCases_ProjectId_Status_IsActive", "CephPilotCases", new[] { "ProjectId", "Status", "IsActive" });
        migrationBuilder.CreateIndex("IX_CephPilotExportArtifacts_CaseId_ArtifactType_IsActive", "CephPilotExportArtifacts", new[] { "CaseId", "ArtifactType", "IsActive" });
        migrationBuilder.CreateIndex("IX_CephPilotExportArtifacts_CaseId_ArtifactType_Sha256", "CephPilotExportArtifacts", new[] { "CaseId", "ArtifactType", "Sha256" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CephPilotExportArtifacts");
        migrationBuilder.DropTable(name: "CephPilotCases");
        migrationBuilder.DropTable(name: "CephPilotProjects");
    }
}
