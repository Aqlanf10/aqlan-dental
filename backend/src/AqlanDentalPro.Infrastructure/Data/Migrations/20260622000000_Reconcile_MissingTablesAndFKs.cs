using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Reconciliation Migration — Creates the missing Employees table and adds
/// FK constraints from HR child tables (AdvancePayments, Attendances,
/// EmployeeDocuments, LeaveRequests, SalaryRecords) to Employees.
///
/// This migration is required because:
/// 1. The Employees table was never created by any migration or startup SQL.
/// 2. AddCentralFinanceV2Hub (20260525092924) creates HR child tables with
///    FK references to Employees, but Employees does not exist in Production.
/// 3. Without this migration, AddCentralFinanceV2Hub cannot apply.
///
/// Everything is idempotent raw SQL — safe to run on databases where
/// the table/FKs already exist (e.g., manual creation).
///
/// NO data loss. NO drop. NO truncate.
/// </summary>
public partial class Reconcile_MissingTablesAndFKs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- ================================================================
    -- 1. Create Employees table if not exists
    --    Matches Employee entity + EmployeeConfiguration exactly:
    --    - BaseEntity columns: Id, CreatedAt, UpdatedAt, IsActive, DeletedAt, DeletedBy
    --    - Employee own columns: UserId, FullName, Phone, NationalId, Position,
    --      BranchId, HireDate, BaseSalary, EmergencyContact, EmergencyPhone, Notes
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'Employees'
    ) THEN
        CREATE TABLE ""Employees"" (
            ""Id""               uuid                      NOT NULL DEFAULT gen_random_uuid(),
            ""UserId""           uuid                      NOT NULL,
            ""FullName""         character varying(200)    NOT NULL,
            ""Phone""            character varying(20)     NULL,
            ""NationalId""       character varying(50)     NULL,
            ""Position""         character varying(100)    NULL,
            ""BranchId""         uuid                      NULL,
            ""HireDate""         timestamp with time zone  NULL,
            ""BaseSalary""       numeric(12,2)             NULL,
            ""EmergencyContact"" character varying(200)    NULL,
            ""EmergencyPhone""   character varying(20)     NULL,
            ""Notes""            character varying(1000)   NULL,
            ""CreatedAt""        timestamp with time zone  NOT NULL DEFAULT now(),
            ""UpdatedAt""        timestamp with time zone  NOT NULL DEFAULT now(),
            ""IsActive""         boolean                   NOT NULL DEFAULT true,
            ""DeletedAt""        timestamp with time zone  NULL,
            ""DeletedBy""        uuid                      NULL,
            CONSTRAINT ""PK_Employees"" PRIMARY KEY (""Id"")
        );
    END IF;

    -- ================================================================
    -- 2. FK: Employees.UserId -> Users.Id (ON DELETE RESTRICT)
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_Users_UserId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'Users'
        ) THEN
            ALTER TABLE ""Employees""
                ADD CONSTRAINT ""FK_Employees_Users_UserId""
                FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"")
                ON DELETE RESTRICT;
        END IF;
    END IF;

    -- ================================================================
    -- 3. FK: Employees.BranchId -> Branches.Id (ON DELETE SET NULL)
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_Branches_BranchId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'Branches'
        ) THEN
            ALTER TABLE ""Employees""
                ADD CONSTRAINT ""FK_Employees_Branches_BranchId""
                FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"")
                ON DELETE SET NULL;
        END IF;
    END IF;

    -- ================================================================
    -- 4. Indexes on Employees
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public' AND tablename = 'Employees' AND indexname = 'IX_Employees_UserId'
    ) THEN
        CREATE INDEX ""IX_Employees_UserId"" ON ""Employees"" (""UserId"");
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'public' AND tablename = 'Employees' AND indexname = 'IX_Employees_BranchId'
    ) THEN
        CREATE INDEX ""IX_Employees_BranchId"" ON ""Employees"" (""BranchId"");
    END IF;

    -- ================================================================
    -- 5. FK: AdvancePayments.EmployeeId -> Employees.Id
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_AdvancePayments_Employees_EmployeeId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'AdvancePayments'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = 'AdvancePayments' AND column_name = 'EmployeeId'
        ) THEN
            ALTER TABLE ""AdvancePayments""
                ADD CONSTRAINT ""FK_AdvancePayments_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"")
                ON DELETE RESTRICT;
        END IF;
    END IF;

    -- ================================================================
    -- 6. FK: Attendances.EmployeeId -> Employees.Id
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Attendances_Employees_EmployeeId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'Attendances'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = 'Attendances' AND column_name = 'EmployeeId'
        ) THEN
            ALTER TABLE ""Attendances""
                ADD CONSTRAINT ""FK_Attendances_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"")
                ON DELETE RESTRICT;
        END IF;
    END IF;

    -- ================================================================
    -- 7. FK: EmployeeDocuments.EmployeeId -> Employees.Id
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeDocuments_Employees_EmployeeId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'EmployeeDocuments'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = 'EmployeeDocuments' AND column_name = 'EmployeeId'
        ) THEN
            ALTER TABLE ""EmployeeDocuments""
                ADD CONSTRAINT ""FK_EmployeeDocuments_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"")
                ON DELETE RESTRICT;
        END IF;
    END IF;

    -- ================================================================
    -- 8. FK: LeaveRequests.EmployeeId -> Employees.Id
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_Employees_EmployeeId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'LeaveRequests'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = 'LeaveRequests' AND column_name = 'EmployeeId'
        ) THEN
            ALTER TABLE ""LeaveRequests""
                ADD CONSTRAINT ""FK_LeaveRequests_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"")
                ON DELETE RESTRICT;
        END IF;
    END IF;

    -- ================================================================
    -- 9. FK: SalaryRecords.EmployeeId -> Employees.Id
    -- ================================================================
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRecords_Employees_EmployeeId'
    ) THEN
        IF EXISTS (
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = 'SalaryRecords'
        ) AND EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_name = 'SalaryRecords' AND column_name = 'EmployeeId'
        ) THEN
            ALTER TABLE ""SalaryRecords""
                ADD CONSTRAINT ""FK_SalaryRecords_Employees_EmployeeId""
                FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"")
                ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    -- Drop FKs from HR child tables to Employees (if they exist)
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
    IF EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'Employees'
    ) THEN
        IF NOT EXISTS (SELECT 1 FROM ""Employees"" LIMIT 1) THEN
            DROP TABLE ""Employees"";
        END IF;
    END IF;
END $$;
        ");
    }
}
