using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds separate email/WhatsApp reminder tracking fields
/// to Appointments and Email field to Patients.
/// Does NOT drop or modify existing columns.
///
/// Idempotent: All column additions use ADD COLUMN IF NOT EXISTS because
/// these same columns are also created by the earlier AddCentralFinanceV2Hub
/// migration (20260525092924). On databases where Finance V2 was already applied,
/// those columns will already exist; on fresh databases, this migration will
/// create them if Finance V2 has not yet run. Either way, the operation succeeds.
/// </summary>
public partial class AddSeparateReminderTrackingAndPatientEmail : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Appointment: separate reminder tracking ──
        // Using idempotent SQL because AddCentralFinanceV2Hub also adds these columns.
        migrationBuilder.Sql(@"
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderSentAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""WhatsAppReminderSentAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderWindowsSent"" TEXT NULL;
");

        // ── Patient: email field ──
        migrationBuilder.Sql(@"
ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Email"" TEXT NULL;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Idempotent drops: columns may have already been dropped by
        // AddCentralFinanceV2Hub Down.
        migrationBuilder.Sql(@"
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""EmailReminderSentAt"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""WhatsAppReminderSentAt"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""EmailReminderWindowsSent"";
ALTER TABLE ""Patients"" DROP COLUMN IF EXISTS ""Email"";
");
    }
}
