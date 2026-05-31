using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Fix enum column types: Convert SupplierBill.Status from integer to varchar(20)
/// so that HasConversion&lt;string&gt;() in SupplierBillConfiguration works correctly.
/// Also ensures Supplier.Type and CreditNote.Status columns are properly typed.
/// Idempotent — uses IF conditions to avoid errors on re-run.
/// </summary>
public partial class FixEnumColumnTypesPhase1 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. Convert SupplierBill.Status from integer to varchar(20) ──
        // This is the critical fix — the original migration created it as integer,
        // but the entity now uses HasConversion<string>() which expects varchar.
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                -- Check if Status column is still integer type
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns 
                    WHERE table_name = 'SupplierBills' 
                      AND column_name = 'Status' 
                      AND data_type = 'integer'
                ) THEN
                    -- Convert integer values to string equivalents
                    ALTER TABLE ""SupplierBills"" ALTER COLUMN ""Status"" TYPE varchar(20) USING CASE
                        WHEN ""Status"" = 0 THEN 'Unpaid'
                        WHEN ""Status"" = 1 THEN 'PartiallyPaid'
                        WHEN ""Status"" = 2 THEN 'FullyPaid'
                        WHEN ""Status"" = 3 THEN 'Cancelled'
                        ELSE 'Unpaid'
                    END;
                END IF;
            END$$;
        ");

        // ── 2. Ensure CreditNotes table exists (safety net) ──
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS ""CreditNotes"" (
                ""Id"" uuid NOT NULL,
                ""InvoiceId"" uuid NOT NULL,
                ""PatientId"" uuid NOT NULL,
                ""Amount"" numeric(12,2) NOT NULL,
                ""Reason"" character varying(500) NOT NULL,
                ""Status"" character varying(20) NOT NULL DEFAULT 'Draft',
                ""RefundPaymentId"" uuid NULL,
                ""BranchId"" uuid NOT NULL,
                ""CreatedBy"" uuid NOT NULL,
                ""Notes"" text NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""UpdatedAt"" timestamp with time zone NOT NULL,
                ""IsActive"" boolean NOT NULL DEFAULT true,
                ""DeletedAt"" timestamp with time zone NULL,
                ""DeletedBy"" uuid NULL,
                CONSTRAINT ""PK_CreditNotes"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_CreditNotes_Invoices_InvoiceId"" FOREIGN KEY (""InvoiceId"") 
                    REFERENCES ""Invoices""(""Id"") ON DELETE RESTRICT,
                CONSTRAINT ""FK_CreditNotes_Patients_PatientId"" FOREIGN KEY (""PatientId"") 
                    REFERENCES ""Patients""(""Id"") ON DELETE RESTRICT
            );
        ");

        // ── 3. Ensure CreditNotes indexes exist ──
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_InvoiceId"" ON ""CreditNotes"" (""InvoiceId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_PatientId"" ON ""CreditNotes"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_Status"" ON ""CreditNotes"" (""Status"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_BranchId"" ON ""CreditNotes"" (""BranchId"");");

        // ── 4. Ensure Suppliers.Type column exists and is varchar ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_name = 'Suppliers' AND column_name = 'Type') THEN
                    ALTER TABLE ""Suppliers"" ADD COLUMN ""Type"" character varying(30) NOT NULL DEFAULT 'MedicalVendor';
                END IF;
                -- If Type column exists but is not varchar, convert it
                IF EXISTS (SELECT 1 FROM information_schema.columns 
                           WHERE table_name = 'Suppliers' AND column_name = 'Type' AND data_type != 'character varying') THEN
                    ALTER TABLE ""Suppliers"" ALTER COLUMN ""Type"" TYPE varchar(30) USING ""Type""::varchar(30);
                END IF;
            END$$;
        ");

        // ── 5. Ensure Suppliers.Balance column exists ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_name = 'Suppliers' AND column_name = 'Balance') THEN
                    ALTER TABLE ""Suppliers"" ADD COLUMN ""Balance"" numeric(18,2) NOT NULL DEFAULT 0;
                END IF;
            END$$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Revert SupplierBill.Status back to integer
        migrationBuilder.Sql(@"
            ALTER TABLE ""SupplierBills"" ALTER COLUMN ""Status"" TYPE integer USING CASE
                WHEN ""Status"" = 'Unpaid' THEN 0
                WHEN ""Status"" = 'PartiallyPaid' THEN 1
                WHEN ""Status"" = 'FullyPaid' THEN 2
                WHEN ""Status"" = 'Cancelled' THEN 3
                ELSE 0
            END;
        ");
    }
}
