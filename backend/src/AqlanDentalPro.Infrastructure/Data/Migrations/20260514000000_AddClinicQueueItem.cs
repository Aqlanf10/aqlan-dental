using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[Migration("20260514000000_AddClinicQueueItem")]
public partial class AddClinicQueueItem : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ClinicQueueItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                DoctorId = table.Column<Guid>(type: "uuid", nullable: true),
                RoomName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Waiting"),
                CalledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CalledBy = table.Column<Guid>(type: "uuid", nullable: true),
                InRoomAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                QueueDate = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ClinicQueueItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_ClinicQueueItems_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_ClinicQueueItems_Appointments_AppointmentId",
                    column: x => x.AppointmentId,
                    principalTable: "Appointments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ClinicQueueItems_Visits_VisitId",
                    column: x => x.VisitId,
                    principalTable: "Visits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ClinicQueueItems_Doctors_DoctorId",
                    column: x => x.DoctorId,
                    principalTable: "Doctors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_QueueDate_Status",
            table: "ClinicQueueItems",
            columns: new[] { "QueueDate", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_PatientId_QueueDate_Active",
            table: "ClinicQueueItems",
            columns: new[] { "PatientId", "QueueDate" },
            filter: "\"Status\" NOT IN ('Completed', 'Cancelled')",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ClinicQueueItems");
    }
}
