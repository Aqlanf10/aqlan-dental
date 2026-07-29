using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds RecordsChecklists table, new fields to OrthoDiagnoses,
/// and PlanLabel to TreatmentPlans. Does NOT drop or modify existing columns.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260611000000_AddOrthoRecordsChecklistAndDiagnosisFields")]
public partial class AddOrthoRecordsChecklistAndDiagnosisFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── New table: RecordsChecklists ──
        migrationBuilder.CreateTable(
            name: "RecordsChecklists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrthoCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                ExtraoralFrontal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ExtraoralProfile = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ExtraoralSmile = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IntraoralFrontal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IntraoralRight = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                IntraoralLeft = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                UpperOcclusal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                LowerOcclusal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                Opg = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                LateralCeph = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                Cbct = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                StudyModels = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                Consent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                Contract = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecordsChecklists", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecordsChecklists_OrthoCases_OrthoCaseId",
                    column: x => x.OrthoCaseId,
                    principalTable: "OrthoCases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RecordsChecklists_OrthoCaseId",
            table: "RecordsChecklists",
            column: "OrthoCaseId",
            unique: true);

        // ── OrthoDiagnoses: new diagnosis fields ──
        migrationBuilder.AddColumn<string>(
            name: "SoftTissueDiagnosis",
            table: "OrthoDiagnoses",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FunctionalDiagnosis",
            table: "OrthoDiagnoses",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Etiology",
            table: "OrthoDiagnoses",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ApprovedBy",
            table: "OrthoDiagnoses",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ApprovedAt",
            table: "OrthoDiagnoses",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_OrthoDiagnoses_ApprovedBy",
            table: "OrthoDiagnoses",
            column: "ApprovedBy");

        migrationBuilder.AddForeignKey(
            name: "FK_OrthoDiagnoses_Doctors_ApprovedBy",
            table: "OrthoDiagnoses",
            column: "ApprovedBy",
            principalTable: "Doctors",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // ── TreatmentPlans: PlanLabel for Plan A/B/C ──
        migrationBuilder.AddColumn<string>(
            name: "PlanLabel",
            table: "TreatmentPlans",
            type: "character varying(5)",
            maxLength: 5,
            nullable: false,
            defaultValue: "A");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OrthoDiagnoses_Doctors_ApprovedBy",
            table: "OrthoDiagnoses");

        migrationBuilder.DropIndex(
            name: "IX_OrthoDiagnoses_ApprovedBy",
            table: "OrthoDiagnoses");

        migrationBuilder.DropTable("RecordsChecklists");

        migrationBuilder.DropColumn(name: "SoftTissueDiagnosis", table: "OrthoDiagnoses");
        migrationBuilder.DropColumn(name: "FunctionalDiagnosis", table: "OrthoDiagnoses");
        migrationBuilder.DropColumn(name: "Etiology", table: "OrthoDiagnoses");
        migrationBuilder.DropColumn(name: "ApprovedBy", table: "OrthoDiagnoses");
        migrationBuilder.DropColumn(name: "ApprovedAt", table: "OrthoDiagnoses");
        migrationBuilder.DropColumn(name: "PlanLabel", table: "TreatmentPlans");
    }
}
