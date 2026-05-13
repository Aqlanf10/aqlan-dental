using Microsoft.EntityFrameworkCore.Migrations;

using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// TD-020 Phase C1-a: Formalizes soft-delete column additions that were
/// previously applied as raw SQL in Program.cs (block B2).
///
/// This migration is fully idempotent: safe to run on databases where the
/// old Program.cs raw SQL already added these columns. Every column addition
/// is guarded by:
///   1. IF the table exists (information_schema.tables)
///   2. IF the column does NOT already exist (information_schema.columns)
///
/// The 39 tables below all inherit from BaseEntity, which defines DeletedAt
/// (timestamp with time zone, nullable) and DeletedBy (uuid, nullable). These
/// columns are already part of the EF Core model (AppDbContextModelSnapshot).
/// This migration ensures they also exist in the physical database schema for
/// any database that was created before soft-delete support was added.
///
/// After this migration, the B2 raw SQL block in Program.cs is redundant and
/// has been removed.
/// </summary>
[Migration("20260522000000_AddSoftDeleteColumnsToLegacyTables")]
public partial class AddSoftDeleteColumnsToLegacyTables : Migration
{
    /// <summary>
    /// Tables that inherit from BaseEntity and need DeletedAt/DeletedBy columns.
    /// Sourced from the B2 block in Program.cs (line 505-516).
    /// </summary>
    private static readonly string[] BaseEntityTables =
    [
        "Patients", "Users", "Doctors", "Branches", "Appointments",
        "Conversations", "ConversationParticipants", "Messages", "MessageReads",
        "Visits", "Payments", "Contracts", "OrthoCases", "OrthoVisits",
        "TreatmentStages", "RetentionRecords", "SurgeryCases", "Prescriptions",
        "Notifications", "AuditLogs", "Settings", "Inventory", "LabOrders",
        "InternalReferrals", "ClinicalPhotos", "Radiographs", "Documents",
        "DentalCharts", "ToothConditions", "GeneralTreatments",
        "WhatsAppMessages", "WhatsAppTemplates", "PatientAccounts",
        "CephAnalyses", "PerioRecords", "GeneralTreatmentPlanItems",
        "MedicalHistories", "DentalHistories", "Receipts"
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in BaseEntityTables)
        {
            // Add DeletedAt column if table exists and column is missing.
            // Uses DO $$ block for IF NOT EXISTS guard on column existence.
            migrationBuilder.Sql($@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{table}') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE ""{table}"" ADD COLUMN ""DeletedAt"" timestamp with time zone NULL;
                        END IF;
                    END IF;
                END $$;
            ");

            // Add DeletedBy column if table exists and column is missing.
            migrationBuilder.Sql($@"
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = '{table}') THEN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE ""{table}"" ADD COLUMN ""DeletedBy"" uuid NULL;
                        END IF;
                    END IF;
                END $$;
            ");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally no-op.
        // DeletedAt/DeletedBy may have existed before this migration due to legacy
        // runtime schema maintenance (old Program.cs block B2). Dropping them could
        // remove existing soft-delete data/schema that predates this migration.
    }
}
