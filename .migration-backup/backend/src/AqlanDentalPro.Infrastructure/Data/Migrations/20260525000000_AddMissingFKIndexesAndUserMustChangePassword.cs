using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

/// <summary>
/// DB-01 FIX: Add missing indexes on frequently queried foreign key columns.
/// Also adds User.MustChangePassword column (SEC-02 FIX).
/// This migration is additive only — no drops, no destructive changes.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260525000000_AddMissingFKIndexesAndUserMustChangePassword")]
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
        //
        // FIX (Replit migration recovery, 2026-07-29): several of these index names
        // (e.g. IX_Visits_PatientId) are also created by earlier migrations (InitialCreate,
        // AddVisitsDocumentsFields), so the original non-idempotent CreateIndex calls always
        // failed with "relation already exists" on a fresh database. Converted all of them to
        // idempotent raw SQL (matching this codebase's established pattern); no index
        // definitions were changed.
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Visits_PatientId"" ON ""Visits"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Visits_AppointmentId"" ON ""Visits"" (""AppointmentId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Payments_PatientId"" ON ""Payments"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Payments_ContractId"" ON ""Payments"" (""ContractId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Contracts_PatientId"" ON ""Contracts"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrders_PatientId"" ON ""LabOrders"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Messages_SenderId"" ON ""Messages"" (""SenderId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Notifications_UserId"" ON ""Notifications"" (""UserId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Appointments_PatientId"" ON ""Appointments"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Appointments_DoctorId"" ON ""Appointments"" (""DoctorId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_ClinicQueueItems_PatientId"" ON ""ClinicQueueItems"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_ClinicQueueItems_AppointmentId"" ON ""ClinicQueueItems"" (""AppointmentId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_ClinicQueueItems_VisitId"" ON ""ClinicQueueItems"" (""VisitId"");");
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
