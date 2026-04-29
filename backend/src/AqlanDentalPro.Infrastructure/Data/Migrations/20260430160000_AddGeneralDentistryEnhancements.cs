using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

public partial class AddGeneralDentistryEnhancements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // PerioRecord - per-tooth periodontal charting
        migrationBuilder.CreateTable(
            name: "PerioRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PatientId = table.Column<Guid>(nullable: false),
                ToothNumber = table.Column<int>(nullable: false),
                ProbingDepth = table.Column<decimal>(nullable: false, defaultValue: 0m),
                ClinicalAttachment = table.Column<decimal>(nullable: false, defaultValue: 0m),
                BleedingOnProbing = table.Column<bool>(nullable: false, defaultValue: false),
                PlaqueIndex = table.Column<int>(nullable: false, defaultValue: 0),
                GingivalIndex = table.Column<int>(nullable: false, defaultValue: 0),
                Furcation = table.Column<int>(nullable: false, defaultValue: 0),
                Mobility = table.Column<int>(nullable: false, defaultValue: 0),
                Notes = table.Column<string>(maxLength: 500, nullable: true),
                DoctorId = table.Column<Guid>(nullable: true),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PeriRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_PeriRecords_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PeriRecords_Doctors_DoctorId",
                    column: x => x.DoctorId,
                    principalTable: "Doctors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PeriRecords_PatientId",
            table: "PerioRecords",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_PeriRecords_ToothNumber",
            table: "PerioRecords",
            column: "ToothNumber");

        // GeneralTreatmentPlanItem - general dentistry treatment plan
        migrationBuilder.CreateTable(
            name: "GeneralTreatmentPlanItems",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                PatientId = table.Column<Guid>(nullable: false),
                ToothNumber = table.Column<string>(maxLength: 10, nullable: true),
                Treatment = table.Column<string>(maxLength: 200, nullable: false),
                Priority = table.Column<string>(maxLength: 10, nullable: false, defaultValue: "medium"),
                Status = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "planned"),
                EstimatedCost = table.Column<decimal>(nullable: true),
                Notes = table.Column<string>(maxLength: 500, nullable: true),
                CompletedAt = table.Column<DateTime>(nullable: true),
                DoctorId = table.Column<Guid>(nullable: true),
                IsActive = table.Column<bool>(nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GeneralTreatmentPlanItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_GeneralTreatmentPlanItems_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_GeneralTreatmentPlanItems_Doctors_DoctorId",
                    column: x => x.DoctorId,
                    principalTable: "Doctors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_GeneralTreatmentPlanItems_PatientId",
            table: "GeneralTreatmentPlanItems",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_GeneralTreatmentPlanItems_Status",
            table: "GeneralTreatmentPlanItems",
            column: "Status");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "GeneralTreatmentPlanItems");
        migrationBuilder.DropTable(name: "PerioRecords");
    }
}
