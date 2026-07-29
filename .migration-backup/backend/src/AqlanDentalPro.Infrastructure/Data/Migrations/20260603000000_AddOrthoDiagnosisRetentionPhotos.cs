using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Sprint 8: Add OrthoDiagnosis and OrthoClinicalPhotos tables for
/// diagnosis summary and clinical photos linking.
/// Additive only — new tables, no destructive changes.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260603000000_AddOrthoDiagnosisRetentionPhotos")]
public partial class AddOrthoDiagnosisRetentionPhotos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── OrthoDiagnoses table ──────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "OrthoDiagnoses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrthoCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                SkeletalClassification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                DentalClassification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                FacialPattern = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ANB = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                Wits = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                FMA = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                SNA = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                SNB = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                IMPA = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                Summary = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrthoDiagnoses", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrthoDiagnoses_OrthoCases_OrthoCaseId",
                    column: x => x.OrthoCaseId,
                    principalTable: "OrthoCases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrthoDiagnoses_OrthoCaseId",
            table: "OrthoDiagnoses",
            column: "OrthoCaseId",
            unique: true);

        // ── OrthoClinicalPhotos table ─────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "OrthoClinicalPhotos",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrthoCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                PhotoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                PhotoType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                TakenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OrthoClinicalPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_OrthoClinicalPhotos_OrthoCases_OrthoCaseId",
                    column: x => x.OrthoCaseId,
                    principalTable: "OrthoCases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_OrthoClinicalPhotos_OrthoCaseId",
            table: "OrthoClinicalPhotos",
            column: "OrthoCaseId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OrthoClinicalPhotos");
        migrationBuilder.DropTable(name: "OrthoDiagnoses");
    }
}
