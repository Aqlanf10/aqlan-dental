using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Reconciliation Migration — Safety net for Employees table and HR FKs.
///
/// The Employees table is now created by AddCentralFinanceV2Hub (Step 0)
/// which runs before this migration. This migration serves as a safety net:
/// - Ensures Employees table exists (idempotent, no-op if already created)
/// - Ensures FK constraints from HR child tables to Employees exist
/// - Ensures indexes on Employees exist
///
/// Everything is idempotent raw SQL. No data loss. No drop. No truncate.
/// </summary>
public partial class Reconcile_MissingTablesAndFKs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- ================================================================
    -- Safety net: Ensure Employees table exists
    -- (Primary creation is in AddCentralFinanceV2Hub Step 0)
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'Employees'
    ) THEN
        CREATE TABLE ""Employees"" (
            ""Id""               UUID                      NOT NULL DEFAULT gen_random_uuid(),
            ""UserId""           UUID                      NOT NULL,
            ""FullName""         VARCHAR(200)              NOT NULL,
            ""Phone""            VARCHAR(20)               NULL,
            ""NationalId""       VARCHAR(50)               NULL,
            ""Position""         VARCHAR(100)              NULL,
            ""BranchId""         UUID                      NULL,
            ""HireDate""         TIMESTAMPTZ               NULL,
            ""BaseSalary""       NUMERIC(12,2)             NULL,
            ""EmergencyContact"" VARCHAR(200)              NULL,
            ""EmergencyPhone""   VARCHAR(20)               NULL,
            ""Notes""            VARCHAR(1000)             NULL,
            ""CreatedAt""        TIMESTAMPTZ               NOT NULL DEFAULT now(),
            ""UpdatedAt""        TIMESTAMPTZ               NOT NULL DEFAULT now(),
            ""IsActive""         BOOLEAN                   NOT NULL DEFAULT true,
            ""DeletedAt""        TIMESTAMPTZ               NULL,
            ""DeletedBy""        UUID                      NULL,
            CONSTRAINT ""PK_Employees"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- ================================================================
    -- Safety net: FKs for Employees
    -- ================================================================
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_Users_UserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users') THEN
            ALTER TABLE ""Employees"" ADD CONSTRAINT ""FK_Employees_Users_UserId""
                FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_Branches_BranchId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Branches') THEN
            ALTER TABLE ""Employees"" ADD CONSTRAINT ""FK_Employees_Branches_BranchId""
                FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE SET NULL;
        END IF;
    END IF;

    -- ================================================================
    -- Safety net: Indexes on Employees
    -- ================================================================
    CREATE INDEX IF NOT EXISTS ""IX_Employees_UserId"" ON ""Employees"" (""UserId"");
    CREATE INDEX IF NOT EXISTS ""IX_Employees_BranchId"" ON ""Employees"" (""BranchId"");

    -- ================================================================
    -- Safety net: FKs from HR child tables to Employees
    -- (Primary creation is in AddCentralFinanceV2Hub Step 3)
    -- ================================================================
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AdvancePayments_Employees_EmployeeId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AdvancePayments' AND column_name = 'EmployeeId') THEN
            ALTER TABLE ""AdvancePayments"" ADD CONSTRAINT ""FK_AdvancePayments_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Attendances_Employees_EmployeeId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Attendances' AND column_name = 'EmployeeId') THEN
            ALTER TABLE ""Attendances"" ADD CONSTRAINT ""FK_Attendances_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeDocuments_Employees_EmployeeId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'EmployeeDocuments' AND column_name = 'EmployeeId') THEN
            ALTER TABLE ""EmployeeDocuments"" ADD CONSTRAINT ""FK_EmployeeDocuments_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_Employees_EmployeeId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LeaveRequests' AND column_name = 'EmployeeId') THEN
            ALTER TABLE ""LeaveRequests"" ADD CONSTRAINT ""FK_LeaveRequests_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRecords_Employees_EmployeeId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SalaryRecords' AND column_name = 'EmployeeId') THEN
            ALTER TABLE ""SalaryRecords"" ADD CONSTRAINT ""FK_SalaryRecords_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- Drop FKs from HR child tables to Employees
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AdvancePayments_Employees_EmployeeId') THEN
        ALTER TABLE ""AdvancePayments"" DROP CONSTRAINT ""FK_AdvancePayments_Employees_EmployeeId"";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Attendances_Employees_EmployeeId') THEN
        ALTER TABLE ""Attendances"" DROP CONSTRAINT ""FK_Attendances_Employees_EmployeeId"";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeDocuments_Employees_EmployeeId') THEN
        ALTER TABLE ""EmployeeDocuments"" DROP CONSTRAINT ""FK_EmployeeDocuments_Employees_EmployeeId"";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_Employees_EmployeeId') THEN
        ALTER TABLE ""LeaveRequests"" DROP CONSTRAINT ""FK_LeaveRequests_Employees_EmployeeId"";
    END IF;

    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRecords_Employees_EmployeeId') THEN
        ALTER TABLE ""SalaryRecords"" DROP CONSTRAINT ""FK_SalaryRecords_Employees_EmployeeId"";
    END IF;

    -- Drop Employees table (only if empty to prevent accidental data loss)
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Employees') THEN
        IF NOT EXISTS (SELECT 1 FROM ""Employees"" LIMIT 1) THEN
            DROP TABLE ""Employees"";
        END IF;
    END IF;
END $$;
        """);
    }
}
