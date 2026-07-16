using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260719000000_AddCephPilotBlindedReviews")]
public class AddCephPilotBlindedReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CephPilotReviewSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewerSlot = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                LandmarkDefinitionVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ToolVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Revision = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephPilotReviewSessions", x => x.Id);
                table.ForeignKey("FK_CephPilotReviewSessions_CephPilotCases_CaseId", x => x.CaseId, "CephPilotCases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_CephPilotReviewSessions_Users_ReviewerUserId", x => x.ReviewerUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CephPilotReviewPoints",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                LandmarkKey = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                IsCore = table.Column<bool>(type: "boolean", nullable: false),
                Visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                XCoordPx = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                YCoordPx = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                ContourDecision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                AnnotatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CephPilotReviewPoints", x => x.Id);
                table.ForeignKey("FK_CephPilotReviewPoints_CephPilotReviewSessions_ReviewSessionId", x => x.ReviewSessionId, "CephPilotReviewSessions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotReviewSessions_RatifiedVersion",
            table: "CephPilotReviewSessions",
            sql: "\"LandmarkDefinitionVersion\" = 'ADP-LM-LAT-v1.0'");
        migrationBuilder.AddCheckConstraint(
            name: "CK_CephPilotReviewPoints_CoordinateContract",
            table: "CephPilotReviewPoints",
            sql: "(\"Visibility\" = 'Visible' AND \"XCoordPx\" IS NOT NULL AND \"YCoordPx\" IS NOT NULL) OR (\"Visibility\" = 'NotVisible' AND \"XCoordPx\" IS NULL AND \"YCoordPx\" IS NULL)");

        migrationBuilder.CreateIndex("IX_CephPilotReviewSessions_ReviewerUserId", "CephPilotReviewSessions", "ReviewerUserId");
        migrationBuilder.CreateIndex("IX_CephPilotReviewSessions_CaseId_ReviewerSlot", "CephPilotReviewSessions", new[] { "CaseId", "ReviewerSlot" }, unique: true);
        migrationBuilder.CreateIndex("IX_CephPilotReviewSessions_CaseId_ReviewerUserId", "CephPilotReviewSessions", new[] { "CaseId", "ReviewerUserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_CephPilotReviewPoints_ReviewSessionId_LandmarkKey", "CephPilotReviewPoints", new[] { "ReviewSessionId", "LandmarkKey" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CephPilotReviewPoints");
        migrationBuilder.DropTable(name: "CephPilotReviewSessions");
    }
}
