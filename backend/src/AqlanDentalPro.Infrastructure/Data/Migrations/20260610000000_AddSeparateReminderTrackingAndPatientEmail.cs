using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds separate email/WhatsApp reminder tracking fields
/// to Appointments and Email field to Patients.
/// Does NOT drop or modify existing columns.
/// </summary>
public partial class AddSeparateReminderTrackingAndPatientEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Appointment: separate reminder tracking ──
        migrationBuilder.AddColumn<DateTime>(
            name: "EmailReminderSentAt",
            table: "Appointments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "WhatsAppReminderSentAt",
            table: "Appointments",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EmailReminderWindowsSent",
            table: "Appointments",
            type: "text",
            nullable: true);

        // ── Patient: email field ──
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Patients",
            type: "text",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmailReminderSentAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "WhatsAppReminderSentAt", table: "Appointments");
        migrationBuilder.DropColumn(name: "EmailReminderWindowsSent", table: "Appointments");
        migrationBuilder.DropColumn(name: "Email", table: "Patients");
    }
}
