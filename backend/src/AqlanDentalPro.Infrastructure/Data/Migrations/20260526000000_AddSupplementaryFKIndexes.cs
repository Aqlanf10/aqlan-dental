using Microsoft.EntityFrameworkCore.Migrations;

/// <summary>
/// DB-01 FIX (Phase 2): Add supplementary FK indexes for frequently queried columns
/// not covered by the initial 20260525000000 migration.
///
/// This migration is ADDITIVE ONLY — no drops, no destructive changes, no data changes.
/// Uses idempotent SQL with IF NOT EXISTS guards for safe deployment.
///
/// Indexes added:
/// - Visits.DoctorId          (visits by doctor)
/// - Payments.DoctorId        (payments by doctor)
/// - Payments.BranchId        (payments by branch)
/// - LabOrders.DoctorId       (lab orders by doctor)
/// - LabOrders.OrthoCaseId    (lab orders by ortho case)
/// - ClinicQueueItems.DoctorId (queue items by doctor)
/// </summary>
public partial class AddSupplementaryFKIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Use idempotent raw SQL to prevent failures if index already exists
        migrationBuilder.Sql(@"
            DO $$ BEGIN
                -- Visits.DoctorId
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Visits_DoctorId') THEN
                    CREATE INDEX ""IX_Visits_DoctorId"" ON ""Visits"" (""DoctorId"");
                END IF;

                -- Payments.DoctorId
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Payments_DoctorId') THEN
                    CREATE INDEX ""IX_Payments_DoctorId"" ON ""Payments"" (""DoctorId"");
                END IF;

                -- Payments.BranchId
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Payments_BranchId') THEN
                    CREATE INDEX ""IX_Payments_BranchId"" ON ""Payments"" (""BranchId"");
                END IF;

                -- LabOrders.DoctorId
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_LabOrders_DoctorId') THEN
                    CREATE INDEX ""IX_LabOrders_DoctorId"" ON ""LabOrders"" (""DoctorId"");
                END IF;

                -- LabOrders.OrthoCaseId
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_LabOrders_OrthoCaseId') THEN
                    CREATE INDEX ""IX_LabOrders_OrthoCaseId"" ON ""LabOrders"" (""OrthoCaseId"");
                END IF;

                -- ClinicQueueItems.DoctorId
                IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_ClinicQueueItems_DoctorId') THEN
                    CREATE INDEX ""IX_ClinicQueueItems_DoctorId"" ON ""ClinicQueueItems"" (""DoctorId"");
                END IF;
            END $$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Idempotent drop — only drop if exists
        migrationBuilder.Sql(@"
            DO $$ BEGIN
                DROP INDEX IF EXISTS ""IX_ClinicQueueItems_DoctorId"";
                DROP INDEX IF EXISTS ""IX_LabOrders_OrthoCaseId"";
                DROP INDEX IF EXISTS ""IX_LabOrders_DoctorId"";
                DROP INDEX IF EXISTS ""IX_Payments_BranchId"";
                DROP INDEX IF EXISTS ""IX_Payments_DoctorId"";
                DROP INDEX IF EXISTS ""IX_Visits_DoctorId"";
            END $$;
        ");
    }
}
