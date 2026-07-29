using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Add PatientTreatmentPlanSteps table for unified treatment plan inside patient file.
/// Additive only — new table, no destructive changes.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260530000000_AddPatientTreatmentPlanSteps")]
public partial class AddPatientTreatmentPlanSteps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PatientTreatmentPlanSteps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                ServiceNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                Department = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                ToothNumber = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                ToothArea = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ResponsibleDoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                PlannedDate = table.Column<DateOnly>(type: "date", nullable: true),
                CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                EstimatedCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                RelatedAppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                RelatedVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                Notes = table.Column<string>(type: "text", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PatientTreatmentPlanSteps", x => x.Id);
                table.ForeignKey(
                    name: "FK_PatientTreatmentPlanSteps_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PatientTreatmentPlanSteps_ClinicServices_ServiceId",
                    column: x => x.ServiceId,
                    principalTable: "ClinicServices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PatientTreatmentPlanSteps_Doctors_ResponsibleDoctorId",
                    column: x => x.ResponsibleDoctorId,
                    principalTable: "Doctors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PatientTreatmentPlanSteps_Appointments_RelatedAppointmentId",
                    column: x => x.RelatedAppointmentId,
                    principalTable: "Appointments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_PatientTreatmentPlanSteps_Visits_RelatedVisitId",
                    column: x => x.RelatedVisitId,
                    principalTable: "Visits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_PatientId",
            table: "PatientTreatmentPlanSteps",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_ServiceId",
            table: "PatientTreatmentPlanSteps",
            column: "ServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_Status",
            table: "PatientTreatmentPlanSteps",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_ResponsibleDoctorId",
            table: "PatientTreatmentPlanSteps",
            column: "ResponsibleDoctorId");

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_RelatedAppointmentId",
            table: "PatientTreatmentPlanSteps",
            column: "RelatedAppointmentId");

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_RelatedVisitId",
            table: "PatientTreatmentPlanSteps",
            column: "RelatedVisitId");

        migrationBuilder.CreateIndex(
            name: "IX_PatientTreatmentPlanSteps_SequenceNumber",
            table: "PatientTreatmentPlanSteps",
            column: "SequenceNumber");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PatientTreatmentPlanSteps");
    }
}
