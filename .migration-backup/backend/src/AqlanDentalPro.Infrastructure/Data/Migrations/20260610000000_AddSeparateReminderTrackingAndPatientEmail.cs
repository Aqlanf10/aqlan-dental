using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Additive migration: adds separate email/WhatsApp reminder tracking fields
/// to Appointments and Email field to Patients.
///
/// Up: Idempotent — all column additions use ADD COLUMN IF NOT EXISTS because
/// these same columns are also created by the earlier AddCentralFinanceV2Hub
/// migration (20260525092924). On databases where Finance V2 was already applied,
/// those columns will already exist; on fresh databases, this migration will
/// create them if Finance V2 has not yet run. Either way, the operation succeeds.
///
/// Down: No-op for overlapping columns. The columns (EmailReminderSentAt,
/// WhatsAppReminderSentAt, EmailReminderWindowsSent, Patients.Email) are owned
/// by the earlier AddCentralFinanceV2Hub migration (20260525092924) which is the
/// schema owner. Rolling back this migration while AddCentralFinanceV2Hub remains
/// applied must NOT drop columns still required by Finance V2.
/// AddCentralFinanceV2Hub's own Down method handles the cleanup when it is rolled back.
///
/// Note on SmsReminderWindowsSent: This column is NOT added by this migration.
/// It is created exclusively by AddCentralFinanceV2Hub (20260525092924) and is
/// fully owned by that migration. No ownership change is needed.
///
/// Deployment-history compatibility: On databases where this migration originally
/// created these columns before Finance V2 was merged, AddCentralFinanceV2Hub's Up
/// would have been a no-op for those columns (IF NOT EXISTS). Rolling back this
/// migration while Finance V2 is still applied would incorrectly remove the columns
/// if we dropped them here. The no-op Down avoids this risk.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260610000000_AddSeparateReminderTrackingAndPatientEmail")]
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
        // NO-OP for all overlapping columns. These columns are owned by
        // AddCentralFinanceV2Hub (20260525092924) which is the earliest migration
        // that creates them. Rolling back this migration alone must not drop columns
        // that Finance V2 still depends on. AddCentralFinanceV2Hub's Down handles
        // the cleanup with DROP COLUMN IF EXISTS.
        //
        // If both migrations need to be rolled back, EF Core applies Down in reverse
        // chronological order: this Down (no-op) runs first, then
        // AddCentralFinanceV2Hub's Down drops the columns.
        //
        // Columns NOT touched by this Down (owned by AddCentralFinanceV2Hub):
        //   - Patients.Email
        //   - Appointments.EmailReminderSentAt
        //   - Appointments.EmailReminderWindowsSent
        //   - Appointments.WhatsAppReminderSentAt
        //   - Appointments.SmsReminderWindowsSent (not added by this migration at all)
    }
}
