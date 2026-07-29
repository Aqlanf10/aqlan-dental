using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Sprint 1 — Fix enum column types Phase 2: Convert enum columns from integer to
/// varchar so that HasConversion&lt;string&gt;() configurations work.
/// Only includes columns where HasConversion&lt;string&gt;() is configured in entity configs.
/// Excluded: Treasuries.Type, VaultTransfers.Status/Type, OperationalExpenses.ApprovalStatus,
/// CashierSessions.Status (these use integer storage without HasConversion).
/// Idempotent — uses IF conditions to avoid errors on re-run.
/// </summary>
public partial class Sprint1_FixEnumColumnTypesPhase2 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ════════════════════════════════════════════════════════════════════
        // Phase 2 — Convert enum columns from integer → varchar
        // Only columns with HasConversion<string>() in entity configurations.
        // Each block is idempotent: skips if column is already varchar.
        // ════════════════════════════════════════════════════════════════════

        // ── 1. Users.Role: integer → varchar(50) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Users' 
                      AND column_name = 'Role' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Users"" ALTER COLUMN ""Role"" TYPE varchar(50) USING CASE
                        WHEN ""Role"" = 0 THEN 'Admin'
                        WHEN ""Role"" = 1 THEN 'Reception'
                        WHEN ""Role"" = 2 THEN 'Accountant'
                        WHEN ""Role"" = 3 THEN 'Orthodontist'
                        WHEN ""Role"" = 4 THEN 'GeneralDentist'
                        WHEN ""Role"" = 5 THEN 'OralSurgeon'
                        ELSE 'Admin'
                    END;
                END IF;
            END$$;
        ");

        // ── 2. Patients.Gender: integer → varchar(10) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Patients' 
                      AND column_name = 'Gender' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Patients"" ALTER COLUMN ""Gender"" TYPE varchar(10) USING CASE
                        WHEN ""Gender"" = 0 THEN 'Male'
                        WHEN ""Gender"" = 1 THEN 'Female'
                        ELSE 'Male'
                    END;
                END IF;
            END$$;
        ");

        // ── 3. Appointments.Status: integer → varchar(50) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Appointments' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Appointments"" ALTER COLUMN ""Status"" TYPE varchar(50) USING CASE
                        WHEN ""Status"" = 0 THEN 'Scheduled'
                        WHEN ""Status"" = 1 THEN 'Confirmed'
                        WHEN ""Status"" = 2 THEN 'Arrived'
                        WHEN ""Status"" = 3 THEN 'Waiting'
                        WHEN ""Status"" = 4 THEN 'Called'
                        WHEN ""Status"" = 5 THEN 'InRoom'
                        WHEN ""Status"" = 6 THEN 'InProgress'
                        WHEN ""Status"" = 7 THEN 'Completed'
                        WHEN ""Status"" = 8 THEN 'Cancelled'
                        WHEN ""Status"" = 9 THEN 'NoShow'
                        WHEN ""Status"" = 10 THEN 'Rescheduled'
                        ELSE 'Scheduled'
                    END;
                END IF;
            END$$;
        ");

        // ── 4. Appointments.Specialty: integer → varchar(50) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Appointments' 
                      AND column_name = 'Specialty' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Appointments"" ALTER COLUMN ""Specialty"" TYPE varchar(50) USING CASE
                        WHEN ""Specialty"" = 0 THEN 'General'
                        WHEN ""Specialty"" = 1 THEN 'Orthodontics'
                        WHEN ""Specialty"" = 2 THEN 'Surgery'
                        WHEN ""Specialty"" = 3 THEN 'Pediatrics'
                        WHEN ""Specialty"" = 4 THEN 'Endodontics'
                        WHEN ""Specialty"" = 5 THEN 'Periodontics'
                        WHEN ""Specialty"" = 6 THEN 'Prosthodontics'
                        WHEN ""Specialty"" = 7 THEN 'Cosmetic'
                        ELSE 'General'
                    END;
                END IF;
            END$$;
        ");

        // ── 5. Contracts.Status: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Contracts' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Contracts"" ALTER COLUMN ""Status"" TYPE varchar(20) USING CASE
                        WHEN ""Status"" = 0 THEN 'Draft'
                        WHEN ""Status"" = 1 THEN 'Active'
                        WHEN ""Status"" = 2 THEN 'Completed'
                        WHEN ""Status"" = 3 THEN 'Cancelled'
                        WHEN ""Status"" = 4 THEN 'Suspended'
                        ELSE 'Draft'
                    END;
                END IF;
            END$$;
        ");

        // ── 6. Invoices.Status: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Invoices' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Invoices"" ALTER COLUMN ""Status"" TYPE varchar(20) USING CASE
                        WHEN ""Status"" = 0 THEN 'Draft'
                        WHEN ""Status"" = 1 THEN 'Issued'
                        WHEN ""Status"" = 2 THEN 'Paid'
                        WHEN ""Status"" = 3 THEN 'PartiallyPaid'
                        WHEN ""Status"" = 4 THEN 'Overdue'
                        WHEN ""Status"" = 5 THEN 'Cancelled'
                        ELSE 'Draft'
                    END;
                END IF;
            END$$;
        ");

        // ── 7. PurchaseOrders.Status: integer → varchar(30) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'PurchaseOrders' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""PurchaseOrders"" ALTER COLUMN ""Status"" TYPE varchar(30) USING CASE
                        WHEN ""Status"" = 0 THEN 'Draft'
                        WHEN ""Status"" = 1 THEN 'Submitted'
                        WHEN ""Status"" = 2 THEN 'Approved'
                        WHEN ""Status"" = 3 THEN 'Received'
                        WHEN ""Status"" = 4 THEN 'PartiallyReceived'
                        WHEN ""Status"" = 5 THEN 'Cancelled'
                        ELSE 'Draft'
                    END;
                END IF;
            END$$;
        ");

        // ── 8. OrthoCases.Status: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'OrthoCases' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""OrthoCases"" ALTER COLUMN ""Status"" TYPE varchar(20) USING CASE
                        WHEN ""Status"" = 0 THEN 'Draft'
                        WHEN ""Status"" = 1 THEN 'Active'
                        WHEN ""Status"" = 2 THEN 'Completed'
                        WHEN ""Status"" = 3 THEN 'Cancelled'
                        WHEN ""Status"" = 4 THEN 'OnHold'
                        ELSE 'Draft'
                    END;
                END IF;
            END$$;
        ");

        // ── 9. SurgeryCases.Status: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'SurgeryCases' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""SurgeryCases"" ALTER COLUMN ""Status"" TYPE varchar(20) USING CASE
                        WHEN ""Status"" = 0 THEN 'Draft'
                        WHEN ""Status"" = 1 THEN 'Planned'
                        WHEN ""Status"" = 2 THEN 'Scheduled'
                        WHEN ""Status"" = 3 THEN 'InProgress'
                        WHEN ""Status"" = 4 THEN 'Completed'
                        WHEN ""Status"" = 5 THEN 'Cancelled'
                        ELSE 'Draft'
                    END;
                END IF;
            END$$;
        ");

        // ── 10. Doctors.CompensationType: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Doctors' 
                      AND column_name = 'CompensationType' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Doctors"" ALTER COLUMN ""CompensationType"" TYPE varchar(20) USING CASE
                        WHEN ""CompensationType"" = 0 THEN 'None'
                        WHEN ""CompensationType"" = 1 THEN 'Percentage'
                        WHEN ""CompensationType"" = 2 THEN 'Fixed'
                        WHEN ""CompensationType"" = 3 THEN 'Hybrid'
                        ELSE 'None'
                    END;
                END IF;
            END$$;
        ");

        // ── 11. ClinicQueueItems.Status: integer → varchar(30) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'ClinicQueueItems' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""ClinicQueueItems"" ALTER COLUMN ""Status"" TYPE varchar(30) USING CASE
                        WHEN ""Status"" = 0 THEN 'Waiting'
                        WHEN ""Status"" = 1 THEN 'Called'
                        WHEN ""Status"" = 2 THEN 'InRoom'
                        WHEN ""Status"" = 3 THEN 'InProgress'
                        WHEN ""Status"" = 4 THEN 'Completed'
                        WHEN ""Status"" = 5 THEN 'Cancelled'
                        WHEN ""Status"" = 6 THEN 'NoShow'
                        ELSE 'Waiting'
                    END;
                END IF;
            END$$;
        ");

        // ── 12. ClinicQueueItems.Priority: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'ClinicQueueItems' 
                      AND column_name = 'Priority' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""ClinicQueueItems"" ALTER COLUMN ""Priority"" TYPE varchar(20) USING CASE
                        WHEN ""Priority"" = 0 THEN 'Normal'
                        WHEN ""Priority"" = 1 THEN 'Urgent'
                        WHEN ""Priority"" = 2 THEN 'Emergency'
                        WHEN ""Priority"" = 3 THEN 'VIP'
                        ELSE 'Normal'
                    END;
                END IF;
            END$$;
        ");

        // ── 13. ClinicServices.Category: integer → varchar(30) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'ClinicServices' 
                      AND column_name = 'Category' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""ClinicServices"" ALTER COLUMN ""Category"" TYPE varchar(30) USING CASE
                        WHEN ""Category"" = 0 THEN 'Consultation'
                        WHEN ""Category"" = 1 THEN 'Treatment'
                        WHEN ""Category"" = 2 THEN 'Lab'
                        WHEN ""Category"" = 3 THEN 'Imaging'
                        WHEN ""Category"" = 4 THEN 'Surgical'
                        WHEN ""Category"" = 5 THEN 'Preventive'
                        WHEN ""Category"" = 6 THEN 'Other'
                        ELSE 'Other'
                    END;
                END IF;
            END$$;
        ");

        // ── 14. AuditLogs.Action: integer → varchar(50) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'AuditLogs' 
                      AND column_name = 'Action' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""AuditLogs"" ALTER COLUMN ""Action"" TYPE varchar(50) USING CASE
                        WHEN ""Action"" = 0 THEN 'Create'
                        WHEN ""Action"" = 1 THEN 'Update'
                        WHEN ""Action"" = 2 THEN 'Delete'
                        WHEN ""Action"" = 3 THEN 'Login'
                        WHEN ""Action"" = 4 THEN 'Logout'
                        WHEN ""Action"" = 5 THEN 'StatusChange'
                        ELSE 'Create'
                    END;
                END IF;
            END$$;
        ");

        // ── 15. Conversations.ConversationType: integer → varchar(20) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'Conversations' 
                      AND column_name = 'ConversationType' 
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""Conversations"" ALTER COLUMN ""ConversationType"" TYPE varchar(20) USING CASE
                        WHEN ""ConversationType"" = 0 THEN 'StaffToStaff'
                        WHEN ""ConversationType"" = 1 THEN 'StaffToPatient'
                        WHEN ""ConversationType"" = 2 THEN 'PatientToStaff'
                        ELSE 'StaffToStaff'
                    END;
                END IF;
            END$$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ════════════════════════════════════════════════════════════════════
        // Revert enum columns back from varchar → integer
        // Only columns converted in Up().
        // ════════════════════════════════════════════════════════════════════

        // ── 1. Users.Role: varchar(50) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Users"" ALTER COLUMN ""Role"" TYPE integer USING CASE
                WHEN ""Role"" = 'Admin' THEN 0
                WHEN ""Role"" = 'Reception' THEN 1
                WHEN ""Role"" = 'Accountant' THEN 2
                WHEN ""Role"" = 'Orthodontist' THEN 3
                WHEN ""Role"" = 'GeneralDentist' THEN 4
                WHEN ""Role"" = 'OralSurgeon' THEN 5
                ELSE 0
            END;
        ");

        // ── 2. Patients.Gender: varchar(10) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Patients"" ALTER COLUMN ""Gender"" TYPE integer USING CASE
                WHEN ""Gender"" = 'Male' THEN 0
                WHEN ""Gender"" = 'Female' THEN 1
                ELSE 0
            END;
        ");

        // ── 3. Appointments.Status: varchar(50) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Appointments"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Scheduled' THEN 0
                WHEN ""Status"" = 'Confirmed' THEN 1
                WHEN ""Status"" = 'Arrived' THEN 2
                WHEN ""Status"" = 'Waiting' THEN 3
                WHEN ""Status"" = 'Called' THEN 4
                WHEN ""Status"" = 'InRoom' THEN 5
                WHEN ""Status"" = 'InProgress' THEN 6
                WHEN ""Status"" = 'Completed' THEN 7
                WHEN ""Status"" = 'Cancelled' THEN 8
                WHEN ""Status"" = 'NoShow' THEN 9
                WHEN ""Status"" = 'Rescheduled' THEN 10
                ELSE 0
            END;
        ");

        // ── 4. Appointments.Specialty: varchar(50) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Appointments"" ALTER COLUMN ""Specialty"" TYPE integer USING CASE
                WHEN ""Specialty"" = 'General' THEN 0
                WHEN ""Specialty"" = 'Orthodontics' THEN 1
                WHEN ""Specialty"" = 'Surgery' THEN 2
                WHEN ""Specialty"" = 'Pediatrics' THEN 3
                WHEN ""Specialty"" = 'Endodontics' THEN 4
                WHEN ""Specialty"" = 'Periodontics' THEN 5
                WHEN ""Specialty"" = 'Prosthodontics' THEN 6
                WHEN ""Specialty"" = 'Cosmetic' THEN 7
                ELSE 0
            END;
        ");

        // ── 5. Contracts.Status: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Contracts"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Draft' THEN 0
                WHEN ""Status"" = 'Active' THEN 1
                WHEN ""Status"" = 'Completed' THEN 2
                WHEN ""Status"" = 'Cancelled' THEN 3
                WHEN ""Status"" = 'Suspended' THEN 4
                ELSE 0
            END;
        ");

        // ── 6. Invoices.Status: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Invoices"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Draft' THEN 0
                WHEN ""Status"" = 'Issued' THEN 1
                WHEN ""Status"" = 'Paid' THEN 2
                WHEN ""Status"" = 'PartiallyPaid' THEN 3
                WHEN ""Status"" = 'Overdue' THEN 4
                WHEN ""Status"" = 'Cancelled' THEN 5
                ELSE 0
            END;
        ");

        // ── 7. PurchaseOrders.Status: varchar(30) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""PurchaseOrders"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Draft' THEN 0
                WHEN ""Status"" = 'Submitted' THEN 1
                WHEN ""Status"" = 'Approved' THEN 2
                WHEN ""Status"" = 'Received' THEN 3
                WHEN ""Status"" = 'PartiallyReceived' THEN 4
                WHEN ""Status"" = 'Cancelled' THEN 5
                ELSE 0
            END;
        ");

        // ── 8. OrthoCases.Status: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""OrthoCases"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Draft' THEN 0
                WHEN ""Status"" = 'Active' THEN 1
                WHEN ""Status"" = 'Completed' THEN 2
                WHEN ""Status"" = 'Cancelled' THEN 3
                WHEN ""Status"" = 'OnHold' THEN 4
                ELSE 0
            END;
        ");

        // ── 9. SurgeryCases.Status: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""SurgeryCases"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Draft' THEN 0
                WHEN ""Status"" = 'Planned' THEN 1
                WHEN ""Status"" = 'Scheduled' THEN 2
                WHEN ""Status"" = 'InProgress' THEN 3
                WHEN ""Status"" = 'Completed' THEN 4
                WHEN ""Status"" = 'Cancelled' THEN 5
                ELSE 0
            END;
        ");

        // ── 10. Doctors.CompensationType: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Doctors"" ALTER COLUMN ""CompensationType"" TYPE integer USING CASE
                WHEN ""CompensationType"" = 'None' THEN 0
                WHEN ""CompensationType"" = 'Percentage' THEN 1
                WHEN ""CompensationType"" = 'Fixed' THEN 2
                WHEN ""CompensationType"" = 'Hybrid' THEN 3
                ELSE 0
            END;
        ");

        // ── 11. ClinicQueueItems.Status: varchar(30) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""ClinicQueueItems"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Waiting' THEN 0
                WHEN ""Status"" = 'Called' THEN 1
                WHEN ""Status"" = 'InRoom' THEN 2
                WHEN ""Status"" = 'InProgress' THEN 3
                WHEN ""Status"" = 'Completed' THEN 4
                WHEN ""Status"" = 'Cancelled' THEN 5
                WHEN ""Status"" = 'NoShow' THEN 6
                ELSE 0
            END;
        ");

        // ── 12. ClinicQueueItems.Priority: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""ClinicQueueItems"" ALTER COLUMN ""Priority"" TYPE integer USING CASE
                WHEN ""Priority"" = 'Normal' THEN 0
                WHEN ""Priority"" = 'Urgent' THEN 1
                WHEN ""Priority"" = 'Emergency' THEN 2
                WHEN ""Priority"" = 'VIP' THEN 3
                ELSE 0
            END;
        ");

        // ── 13. ClinicServices.Category: varchar(30) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""ClinicServices"" ALTER COLUMN ""Category"" TYPE integer USING CASE
                WHEN ""Category"" = 'Consultation' THEN 0
                WHEN ""Category"" = 'Treatment' THEN 1
                WHEN ""Category"" = 'Lab' THEN 2
                WHEN ""Category"" = 'Imaging' THEN 3
                WHEN ""Category"" = 'Surgical' THEN 4
                WHEN ""Category"" = 'Preventive' THEN 5
                WHEN ""Category"" = 'Other' THEN 6
                ELSE 6
            END;
        ");

        // ── 14. AuditLogs.Action: varchar(50) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""AuditLogs"" ALTER COLUMN ""Action"" TYPE integer USING CASE
                WHEN ""Action"" = 'Create' THEN 0
                WHEN ""Action"" = 'Update' THEN 1
                WHEN ""Action"" = 'Delete' THEN 2
                WHEN ""Action"" = 'Login' THEN 3
                WHEN ""Action"" = 'Logout' THEN 4
                WHEN ""Action"" = 'StatusChange' THEN 5
                ELSE 0
            END;
        ");

        // ── 15. Conversations.ConversationType: varchar(20) → integer ──
        migrationBuilder.Sql(@"
            ALTER TABLE ""Conversations"" ALTER COLUMN ""ConversationType"" TYPE integer USING CASE
                WHEN ""ConversationType"" = 'StaffToStaff' THEN 0
                WHEN ""ConversationType"" = 'StaffToPatient' THEN 1
                WHEN ""ConversationType"" = 'PatientToStaff' THEN 2
                ELSE 0
            END;
        ");
    }
}
