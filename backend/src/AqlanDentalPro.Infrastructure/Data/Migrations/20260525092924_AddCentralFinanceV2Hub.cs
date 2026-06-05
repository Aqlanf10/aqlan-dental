using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCentralFinanceV2Hub : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- ============================================================
-- Step 0: Create Employees table (MUST be before any FK to Employees)
-- This table was never created by any previous migration or startup SQL,
-- yet this migration and others reference it via FK.
-- ============================================================
DO $$ BEGIN
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
END $$;

-- FK: Employees.UserId -> Users.Id (ON DELETE RESTRICT)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_Users_UserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users') THEN
            ALTER TABLE ""Employees"" ADD CONSTRAINT ""FK_Employees_Users_UserId""
                FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;

-- FK: Employees.BranchId -> Branches.Id (ON DELETE SET NULL)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Employees_Branches_BranchId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Branches') THEN
            ALTER TABLE ""Employees"" ADD CONSTRAINT ""FK_Employees_Branches_BranchId""
                FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE SET NULL;
        END IF;
    END IF;
END $$;

-- Indexes on Employees
CREATE INDEX IF NOT EXISTS ""IX_Employees_UserId"" ON ""Employees"" (""UserId"");
CREATE INDEX IF NOT EXISTS ""IX_Employees_BranchId"" ON ""Employees"" (""BranchId"");

-- ============================================================
-- Step 1: Idempotent column additions on existing tables
-- ============================================================
ALTER TABLE ""Patients"" ADD COLUMN IF NOT EXISTS ""Email"" TEXT NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderSentAt"" TIMESTAMPTZ NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""EmailReminderWindowsSent"" TEXT NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""SmsReminderWindowsSent"" TEXT NULL;
ALTER TABLE ""Appointments"" ADD COLUMN IF NOT EXISTS ""WhatsAppReminderSentAt"" TIMESTAMPTZ NULL;

-- ============================================================
-- Step 2: Create tables (IF NOT EXISTS) with PK inline
-- ============================================================

-- BackupRecords (no foreign keys)
CREATE TABLE IF NOT EXISTS ""BackupRecords"" (
    ""Id"" UUID NOT NULL,
    ""Type"" INTEGER NOT NULL,
    ""Status"" INTEGER NOT NULL,
    ""StartedAt"" TIMESTAMPTZ NOT NULL,
    ""CompletedAt"" TIMESTAMPTZ NULL,
    ""SizeBytes"" BIGINT NULL,
    ""FilePath"" VARCHAR(500) NULL,
    ""ErrorMessage"" VARCHAR(2000) NULL,
    ""TriggeredBy"" UUID NULL,
    ""IsAutomatic"" BOOLEAN NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_BackupRecords"" PRIMARY KEY (""Id"")
);

-- SmsTemplates (no foreign keys)
CREATE TABLE IF NOT EXISTS ""SmsTemplates"" (
    ""Id"" UUID NOT NULL,
    ""TemplateKey"" VARCHAR(50) NOT NULL,
    ""NameAr"" VARCHAR(100) NOT NULL,
    ""ContentTemplate"" VARCHAR(500) NOT NULL,
    ""IsTemplateActive"" BOOLEAN NOT NULL,
    ""Category"" VARCHAR(30) NOT NULL,
    ""MaxLength"" INTEGER NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_SmsTemplates"" PRIMARY KEY (""Id"")
);

-- CashierSessions (FKs added separately)
CREATE TABLE IF NOT EXISTS ""CashierSessions"" (
    ""Id"" UUID NOT NULL,
    ""SessionNumber"" TEXT NOT NULL,
    ""CashierId"" UUID NOT NULL,
    ""BranchId"" UUID NOT NULL,
    ""OpeningTime"" TIMESTAMPTZ NOT NULL,
    ""ClosingTime"" TIMESTAMPTZ NULL,
    ""OpeningBalance"" NUMERIC NOT NULL,
    ""ExpectedClosingCash"" NUMERIC NOT NULL,
    ""ActualClosingCash"" NUMERIC NULL,
    ""ExpectedClosingCard"" NUMERIC NOT NULL,
    ""ActualClosingCard"" NUMERIC NULL,
    ""ExpectedClosingBank"" NUMERIC NOT NULL,
    ""ActualClosingBank"" NUMERIC NULL,
    ""ShortageOrSurplus"" NUMERIC NULL,
    ""Status"" INTEGER NOT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_CashierSessions"" PRIMARY KEY (""Id"")
);

-- AdvancePayments (FK added separately)
CREATE TABLE IF NOT EXISTS ""AdvancePayments"" (
    ""Id"" UUID NOT NULL,
    ""EmployeeId"" UUID NOT NULL,
    ""Amount"" NUMERIC(12,2) NOT NULL,
    ""Reason"" VARCHAR(500) NULL,
    ""RequestDate"" TIMESTAMPTZ NOT NULL,
    ""Status"" INTEGER NOT NULL,
    ""ApprovedBy"" UUID NULL,
    ""ApprovedAt"" TIMESTAMPTZ NULL,
    ""RejectionReason"" VARCHAR(500) NULL,
    ""DeductFromMonth"" INTEGER NULL,
    ""DeductFromYear"" INTEGER NULL,
    ""IsDeducted"" BOOLEAN NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_AdvancePayments"" PRIMARY KEY (""Id"")
);

-- Attendances (FK added separately)
CREATE TABLE IF NOT EXISTS ""Attendances"" (
    ""Id"" UUID NOT NULL,
    ""EmployeeId"" UUID NOT NULL,
    ""Date"" DATE NOT NULL,
    ""CheckIn"" INTERVAL NULL,
    ""CheckOut"" INTERVAL NULL,
    ""Status"" INTEGER NOT NULL,
    ""Notes"" VARCHAR(500) NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_Attendances"" PRIMARY KEY (""Id"")
);

-- EmployeeDocuments (FK added separately)
CREATE TABLE IF NOT EXISTS ""EmployeeDocuments"" (
    ""Id"" UUID NOT NULL,
    ""EmployeeId"" UUID NOT NULL,
    ""DocumentType"" VARCHAR(100) NOT NULL,
    ""Title"" VARCHAR(200) NOT NULL,
    ""FilePath"" VARCHAR(500) NOT NULL,
    ""FileName"" VARCHAR(300) NULL,
    ""ContentType"" VARCHAR(100) NULL,
    ""FileSize"" BIGINT NULL,
    ""UploadedBy"" UUID NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_EmployeeDocuments"" PRIMARY KEY (""Id"")
);

-- LeaveRequests (FK added separately)
CREATE TABLE IF NOT EXISTS ""LeaveRequests"" (
    ""Id"" UUID NOT NULL,
    ""EmployeeId"" UUID NOT NULL,
    ""LeaveType"" INTEGER NOT NULL,
    ""StartDate"" DATE NOT NULL,
    ""EndDate"" DATE NOT NULL,
    ""TotalDays"" INTEGER NOT NULL,
    ""Reason"" VARCHAR(500) NULL,
    ""Status"" INTEGER NOT NULL,
    ""ApprovedBy"" UUID NULL,
    ""ApprovedAt"" TIMESTAMPTZ NULL,
    ""RejectionReason"" VARCHAR(500) NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_LeaveRequests"" PRIMARY KEY (""Id"")
);

-- OperationalExpenses (FKs added separately)
CREATE TABLE IF NOT EXISTS ""OperationalExpenses"" (
    ""Id"" UUID NOT NULL,
    ""ExpenseNumber"" TEXT NOT NULL,
    ""Title"" TEXT NOT NULL,
    ""Category"" INTEGER NOT NULL,
    ""Amount"" NUMERIC NOT NULL,
    ""ExpenseDate"" DATE NOT NULL,
    ""PaymentMethod"" TEXT NOT NULL,
    ""SupplierId"" UUID NULL,
    ""LabOrderId"" UUID NULL,
    ""Notes"" TEXT NULL,
    ""ReceiptAttachmentUrl"" TEXT NULL,
    ""PaidBy"" UUID NOT NULL,
    ""BranchId"" UUID NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_OperationalExpenses"" PRIMARY KEY (""Id"")
);

-- SalaryRecords (FK added separately)
CREATE TABLE IF NOT EXISTS ""SalaryRecords"" (
    ""Id"" UUID NOT NULL,
    ""EmployeeId"" UUID NOT NULL,
    ""Year"" INTEGER NOT NULL,
    ""Month"" INTEGER NOT NULL,
    ""BaseSalary"" NUMERIC(12,2) NOT NULL,
    ""Deductions"" NUMERIC(12,2) NOT NULL,
    ""Advances"" NUMERIC(12,2) NOT NULL,
    ""Bonuses"" NUMERIC(12,2) NOT NULL,
    ""NetSalary"" NUMERIC(12,2) NOT NULL,
    ""PaidAt"" TIMESTAMPTZ NULL,
    ""PaidBy"" UUID NULL,
    ""PaymentMethod"" VARCHAR(50) NULL,
    ""Notes"" VARCHAR(500) NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_SalaryRecords"" PRIMARY KEY (""Id"")
);

-- SmsMessages (FK added separately)
CREATE TABLE IF NOT EXISTS ""SmsMessages"" (
    ""Id"" UUID NOT NULL,
    ""PatientId"" UUID NOT NULL,
    ""PhoneNumber"" VARCHAR(20) NOT NULL,
    ""TemplateType"" VARCHAR(50) NOT NULL,
    ""MessageContent"" VARCHAR(1000) NOT NULL,
    ""Status"" VARCHAR(20) NOT NULL,
    ""ExternalId"" VARCHAR(100) NULL,
    ""ErrorMessage"" VARCHAR(500) NULL,
    ""RetryCount"" INTEGER NOT NULL,
    ""SentAt"" TIMESTAMPTZ NULL,
    ""DeliveredAt"" TIMESTAMPTZ NULL,
    ""RelatedEntityId"" UUID NULL,
    ""RelatedEntityType"" VARCHAR(50) NULL,
    ""Gateway"" VARCHAR(30) NOT NULL,
    ""CharacterCount"" INTEGER NOT NULL,
    ""SegmentCount"" INTEGER NOT NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_SmsMessages"" PRIMARY KEY (""Id"")
);

-- CashFlowTransactions (FKs added separately; depends on CashierSessions)
CREATE TABLE IF NOT EXISTS ""CashFlowTransactions"" (
    ""Id"" UUID NOT NULL,
    ""TransactionNumber"" TEXT NOT NULL,
    ""Type"" INTEGER NOT NULL,
    ""Category"" INTEGER NOT NULL,
    ""Amount"" NUMERIC NOT NULL,
    ""PaymentMethod"" TEXT NOT NULL,
    ""TransactionDate"" DATE NOT NULL,
    ""ReferenceId"" UUID NULL,
    ""ReferenceNumber"" TEXT NULL,
    ""Description"" TEXT NOT NULL,
    ""PerformedBy"" UUID NOT NULL,
    ""BranchId"" UUID NOT NULL,
    ""CashierSessionId"" UUID NULL,
    ""CreatedAt"" TIMESTAMPTZ NOT NULL,
    ""UpdatedAt"" TIMESTAMPTZ NOT NULL,
    ""IsActive"" BOOLEAN NOT NULL,
    ""DeletedAt"" TIMESTAMPTZ NULL,
    ""DeletedBy"" UUID NULL,
    CONSTRAINT ""PK_CashFlowTransactions"" PRIMARY KEY (""Id"")
);

-- EmailLogs (idempotent via DO block)
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'EmailLogs') THEN
        CREATE TABLE ""EmailLogs"" (
            ""Id""                UUID NOT NULL,
            ""ToEmail""           TEXT NOT NULL,
            ""Subject""           TEXT NOT NULL,
            ""Category""          TEXT NOT NULL,
            ""Provider""          TEXT NULL,
            ""IsSent""            BOOLEAN NOT NULL,
            ""ErrorMessage""      TEXT NULL,
            ""ExternalId""        TEXT NULL,
            ""RelatedEntityType"" TEXT NULL,
            ""RelatedEntityId""   UUID NULL,
            ""CreatedAt""         TIMESTAMPTZ NOT NULL,
            CONSTRAINT ""PK_EmailLogs"" PRIMARY KEY (""Id"")
        );
    END IF;
END $$;

-- ============================================================
-- Step 3: Add foreign keys (idempotent via DO blocks)
-- ============================================================

-- AdvancePayments -> Employees
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AdvancePayments_Employees_EmployeeId') THEN
        ALTER TABLE ""AdvancePayments"" ADD CONSTRAINT ""FK_AdvancePayments_Employees_EmployeeId""
            FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
    END IF;
END $$;

-- Attendances -> Employees
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Attendances_Employees_EmployeeId') THEN
        ALTER TABLE ""Attendances"" ADD CONSTRAINT ""FK_Attendances_Employees_EmployeeId""
            FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
    END IF;
END $$;

-- CashierSessions -> Branches
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashierSessions_Branches_BranchId') THEN
        ALTER TABLE ""CashierSessions"" ADD CONSTRAINT ""FK_CashierSessions_Branches_BranchId""
            FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE CASCADE;
    END IF;
END $$;

-- CashierSessions -> Users
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashierSessions_Users_CashierId') THEN
        ALTER TABLE ""CashierSessions"" ADD CONSTRAINT ""FK_CashierSessions_Users_CashierId""
            FOREIGN KEY (""CashierId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE;
    END IF;
END $$;

-- EmployeeDocuments -> Employees
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_EmployeeDocuments_Employees_EmployeeId') THEN
        ALTER TABLE ""EmployeeDocuments"" ADD CONSTRAINT ""FK_EmployeeDocuments_Employees_EmployeeId""
            FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
    END IF;
END $$;

-- LeaveRequests -> Employees
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LeaveRequests_Employees_EmployeeId') THEN
        ALTER TABLE ""LeaveRequests"" ADD CONSTRAINT ""FK_LeaveRequests_Employees_EmployeeId""
            FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
    END IF;
END $$;

-- OperationalExpenses -> Branches
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OperationalExpenses_Branches_BranchId') THEN
        ALTER TABLE ""OperationalExpenses"" ADD CONSTRAINT ""FK_OperationalExpenses_Branches_BranchId""
            FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE CASCADE;
    END IF;
END $$;

-- OperationalExpenses -> LabOrders
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OperationalExpenses_LabOrders_LabOrderId') THEN
        ALTER TABLE ""OperationalExpenses"" ADD CONSTRAINT ""FK_OperationalExpenses_LabOrders_LabOrderId""
            FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders""(""Id"") ON DELETE NO ACTION;
    END IF;
END $$;

-- OperationalExpenses -> Suppliers
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OperationalExpenses_Suppliers_SupplierId') THEN
        ALTER TABLE ""OperationalExpenses"" ADD CONSTRAINT ""FK_OperationalExpenses_Suppliers_SupplierId""
            FOREIGN KEY (""SupplierId"") REFERENCES ""Suppliers""(""Id"") ON DELETE NO ACTION;
    END IF;
END $$;

-- SalaryRecords -> Employees
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SalaryRecords_Employees_EmployeeId') THEN
        ALTER TABLE ""SalaryRecords"" ADD CONSTRAINT ""FK_SalaryRecords_Employees_EmployeeId""
            FOREIGN KEY (""EmployeeId"") REFERENCES ""Employees""(""Id"") ON DELETE RESTRICT;
    END IF;
END $$;

-- SmsMessages -> Patients
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SmsMessages_Patients_PatientId') THEN
        ALTER TABLE ""SmsMessages"" ADD CONSTRAINT ""FK_SmsMessages_Patients_PatientId""
            FOREIGN KEY (""PatientId"") REFERENCES ""Patients""(""Id"") ON DELETE SET NULL;
    END IF;
END $$;

-- CashFlowTransactions -> Branches
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_Branches_BranchId') THEN
        ALTER TABLE ""CashFlowTransactions"" ADD CONSTRAINT ""FK_CashFlowTransactions_Branches_BranchId""
            FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE CASCADE;
    END IF;
END $$;

-- CashFlowTransactions -> CashierSessions
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_CashierSessions_CashierSessionId') THEN
        ALTER TABLE ""CashFlowTransactions"" ADD CONSTRAINT ""FK_CashFlowTransactions_CashierSessions_CashierSessionId""
            FOREIGN KEY (""CashierSessionId"") REFERENCES ""CashierSessions""(""Id"") ON DELETE NO ACTION;
    END IF;
END $$;

-- ============================================================
-- Step 4: Create indexes (IF NOT EXISTS)
-- ============================================================

CREATE INDEX IF NOT EXISTS ""IX_AdvancePayments_EmployeeId"" ON ""AdvancePayments"" (""EmployeeId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Attendances_EmployeeId_Date"" ON ""Attendances"" (""EmployeeId"", ""Date"");
CREATE INDEX IF NOT EXISTS ""IX_BackupRecords_StartedAt"" ON ""BackupRecords"" (""StartedAt"");
CREATE INDEX IF NOT EXISTS ""IX_CashFlowTransactions_BranchId"" ON ""CashFlowTransactions"" (""BranchId"");
CREATE INDEX IF NOT EXISTS ""IX_CashFlowTransactions_CashierSessionId"" ON ""CashFlowTransactions"" (""CashierSessionId"");
CREATE INDEX IF NOT EXISTS ""IX_CashierSessions_BranchId"" ON ""CashierSessions"" (""BranchId"");
CREATE INDEX IF NOT EXISTS ""IX_CashierSessions_CashierId"" ON ""CashierSessions"" (""CashierId"");
CREATE INDEX IF NOT EXISTS ""IX_EmployeeDocuments_EmployeeId"" ON ""EmployeeDocuments"" (""EmployeeId"");
CREATE INDEX IF NOT EXISTS ""IX_LeaveRequests_EmployeeId"" ON ""LeaveRequests"" (""EmployeeId"");
CREATE INDEX IF NOT EXISTS ""IX_OperationalExpenses_BranchId"" ON ""OperationalExpenses"" (""BranchId"");
CREATE INDEX IF NOT EXISTS ""IX_OperationalExpenses_LabOrderId"" ON ""OperationalExpenses"" (""LabOrderId"");
CREATE INDEX IF NOT EXISTS ""IX_OperationalExpenses_SupplierId"" ON ""OperationalExpenses"" (""SupplierId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SalaryRecords_EmployeeId_Year_Month"" ON ""SalaryRecords"" (""EmployeeId"", ""Year"", ""Month"");
CREATE INDEX IF NOT EXISTS ""IX_SmsMessages_CreatedAt"" ON ""SmsMessages"" (""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_SmsMessages_CreatedAt_Status"" ON ""SmsMessages"" (""CreatedAt"", ""Status"");
CREATE INDEX IF NOT EXISTS ""IX_SmsMessages_PatientId"" ON ""SmsMessages"" (""PatientId"");
CREATE INDEX IF NOT EXISTS ""IX_SmsMessages_Status"" ON ""SmsMessages"" (""Status"");
CREATE INDEX IF NOT EXISTS ""IX_SmsMessages_TemplateType"" ON ""SmsMessages"" (""TemplateType"");
CREATE INDEX IF NOT EXISTS ""IX_SmsTemplates_Category"" ON ""SmsTemplates"" (""Category"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_SmsTemplates_TemplateKey"" ON ""SmsTemplates"" (""TemplateKey"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop Employees table (only if empty — safety net against data loss)
            // Must drop AFTER child tables that reference it
            // Child tables are dropped first below, then Employees at the end

            migrationBuilder.DropTable(
                name: "AdvancePayments");

            migrationBuilder.DropTable(
                name: "Attendances");

            migrationBuilder.DropTable(
                name: "BackupRecords");

            migrationBuilder.DropTable(
                name: "CashFlowTransactions");

            // Drop EmailLogs table — this migration is the schema owner for EmailLogs.
            // Later migration AddEmailLog (20260609000000) has a no-op Down to avoid
            // dropping a table still required by Finance V2.
            // DROP TABLE IF EXISTS is idempotent for safety if the table was already removed.
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""EmailLogs"";");

            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "OperationalExpenses");

            migrationBuilder.DropTable(
                name: "SalaryRecords");

            migrationBuilder.DropTable(
                name: "SmsMessages");

            migrationBuilder.DropTable(
                name: "SmsTemplates");

            migrationBuilder.DropTable(
                name: "CashierSessions");

            // Drop Employees table after all child tables (AdvancePayments, Attendances, etc.)
            // that reference it have been dropped above.
            // Safety: only drop if empty to prevent accidental data loss.
            migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Employees') THEN
        IF NOT EXISTS (SELECT 1 FROM ""Employees"" LIMIT 1) THEN
            DROP TABLE ""Employees"";
        END IF;
    END IF;
END $$;
");

            // Drop columns — this migration is the schema owner for these overlapping columns.
            // Later migration AddSeparateReminderTrackingAndPatientEmail (20260610000000) has
            // a no-op Down to avoid dropping columns still required by Finance V2.
            // DROP COLUMN IF EXISTS is idempotent for safety if columns were already removed.
            migrationBuilder.Sql(@"
ALTER TABLE ""Patients"" DROP COLUMN IF EXISTS ""Email"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""EmailReminderSentAt"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""EmailReminderWindowsSent"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""SmsReminderWindowsSent"";
ALTER TABLE ""Appointments"" DROP COLUMN IF EXISTS ""WhatsAppReminderSentAt"";
");
        }
    }
}
