using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

/// <summary>
/// DB-01 FIX: Add missing indexes on frequently queried foreign key columns.
/// Also adds User.MustChangePassword column (SEC-02 FIX).
/// This migration is additive only — no drops, no destructive changes.
/// </summary>
public partial class AddMissingFKIndexesAndUserMustChangePassword : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SEC-02 FIX: Add MustChangePassword column to Users table
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // DB-01 FIX: Add missing FK indexes for frequently queried columns
        // These are additive-only indexes that improve query performance without changing data

        // Visits.PatientId — most visits are queried by patient
        migrationBuilder.CreateIndex(
            name: "IX_Visits_PatientId",
            table: "Visits",
            column: "PatientId");

        // Visits.AppointmentId — linking visits to appointments
        migrationBuilder.CreateIndex(
            name: "IX_Visits_AppointmentId",
            table: "Visits",
            column: "AppointmentId");

        // Payments.PatientId — payment history queries by patient
        migrationBuilder.CreateIndex(
            name: "IX_Payments_PatientId",
            table: "Payments",
            column: "PatientId");

        // Payments.ContractId — contract-related payment queries
        migrationBuilder.CreateIndex(
            name: "IX_Payments_ContractId",
            table: "Payments",
            column: "ContractId");

        // Contracts.PatientId — patient contract queries
        migrationBuilder.CreateIndex(
            name: "IX_Contracts_PatientId",
            table: "Contracts",
            column: "PatientId");

        // LabOrders.PatientId — lab orders by patient
        migrationBuilder.CreateIndex(
            name: "IX_LabOrders_PatientId",
            table: "LabOrders",
            column: "PatientId");

        // Messages.SenderId — sender-based message queries
        migrationBuilder.CreateIndex(
            name: "IX_Messages_SenderId",
            table: "Messages",
            column: "SenderId");

        // Notifications.UserId — user notification queries
        migrationBuilder.CreateIndex(
            name: "IX_Notifications_UserId",
            table: "Notifications",
            column: "UserId");

        // Appointments.PatientId — patient appointment queries
        migrationBuilder.CreateIndex(
            name: "IX_Appointments_PatientId",
            table: "Appointments",
            column: "PatientId");

        // Appointments.DoctorId — individual doctor index for non-composite queries
        migrationBuilder.CreateIndex(
            name: "IX_Appointments_DoctorId",
            table: "Appointments",
            column: "DoctorId");

        // ClinicQueueItems.PatientId — queue lookup by patient
        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_PatientId",
            table: "ClinicQueueItems",
            column: "PatientId");

        // ClinicQueueItems.AppointmentId — queue lookup by appointment
        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_AppointmentId",
            table: "ClinicQueueItems",
            column: "AppointmentId");

        // ClinicQueueItems.VisitId — queue lookup by visit
        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_VisitId",
            table: "ClinicQueueItems",
            column: "VisitId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Remove indexes
        migrationBuilder.DropIndex(name: "IX_ClinicQueueItems_VisitId", table: "ClinicQueueItems");
        migrationBuilder.DropIndex(name: "IX_ClinicQueueItems_AppointmentId", table: "ClinicQueueItems");
        migrationBuilder.DropIndex(name: "IX_ClinicQueueItems_PatientId", table: "ClinicQueueItems");
        migrationBuilder.DropIndex(name: "IX_Appointments_DoctorId", table: "Appointments");
        migrationBuilder.DropIndex(name: "IX_Appointments_PatientId", table: "Appointments");
        migrationBuilder.DropIndex(name: "IX_Notifications_UserId", table: "Notifications");
        migrationBuilder.DropIndex(name: "IX_Messages_SenderId", table: "Messages");
        migrationBuilder.DropIndex(name: "IX_LabOrders_PatientId", table: "LabOrders");
        migrationBuilder.DropIndex(name: "IX_Contracts_PatientId", table: "Contracts");
        migrationBuilder.DropIndex(name: "IX_Payments_ContractId", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_Payments_PatientId", table: "Payments");
        migrationBuilder.DropIndex(name: "IX_Visits_AppointmentId", table: "Visits");
        migrationBuilder.DropIndex(name: "IX_Visits_PatientId", table: "Visits");

        // Remove MustChangePassword column
        migrationBuilder.DropColumn(name: "MustChangePassword", table: "Users");
    }
}
