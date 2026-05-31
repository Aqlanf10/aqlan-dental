using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Finance Phase 1: Add CreditNotes table, Supplier.Type and Supplier.Balance columns.
/// Uses raw SQL with IF NOT EXISTS for idempotent column additions.
/// </summary>
public partial class AddFinancePhase1CreditNotesAndSupplierFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. Add Supplier.Type column (idempotent) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_name = 'Suppliers' AND column_name = 'Type') THEN
                    ALTER TABLE ""Suppliers"" ADD COLUMN ""Type"" character varying(30) NOT NULL DEFAULT 'MedicalVendor';
                END IF;
            END$$;
        ");

        // ── 2. Add Supplier.Balance column (idempotent) ──
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_name = 'Suppliers' AND column_name = 'Balance') THEN
                    ALTER TABLE ""Suppliers"" ADD COLUMN ""Balance"" numeric(18,2) NOT NULL DEFAULT 0;
                END IF;
            END$$;
        ");

        // ── 3. Create CreditNotes table (idempotent) ──
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

        // ── 4. Create indexes (idempotent) ──
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_InvoiceId"" ON ""CreditNotes"" (""InvoiceId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_PatientId"" ON ""CreditNotes"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_Status"" ON ""CreditNotes"" (""Status"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_CreditNotes_BranchId"" ON ""CreditNotes"" (""BranchId"");");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CreditNotes");
        migrationBuilder.DropColumn(name: "Balance", table: "Suppliers");
        migrationBuilder.DropColumn(name: "Type", table: "Suppliers");
    }
}
