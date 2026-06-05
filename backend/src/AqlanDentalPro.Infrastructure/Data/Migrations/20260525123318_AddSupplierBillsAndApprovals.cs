using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddSupplierBillsAndApprovals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1-6. AddColumn on OperationalExpenses (nullable columns)
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'ApprovalNotes') THEN
        ALTER TABLE ""OperationalExpenses"" ADD COLUMN ""ApprovalNotes"" text NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'ApprovalStatus') THEN
        ALTER TABLE ""OperationalExpenses"" ADD COLUMN ""ApprovalStatus"" integer NOT NULL DEFAULT 0;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'ApprovedAt') THEN
        ALTER TABLE ""OperationalExpenses"" ADD COLUMN ""ApprovedAt"" timestamptz NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'ApprovedById') THEN
        ALTER TABLE ""OperationalExpenses"" ADD COLUMN ""ApprovedById"" uuid NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'CashFlowTransactionId') THEN
        ALTER TABLE ""OperationalExpenses"" ADD COLUMN ""CashFlowTransactionId"" uuid NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'IsPostedToLedger') THEN
        ALTER TABLE ""OperationalExpenses"" ADD COLUMN ""IsPostedToLedger"" boolean NOT NULL DEFAULT false;
    END IF;
END $$;
");

        // 7. CreateTable SupplierBills (no inline FKs — added separately below)
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""SupplierBills"" (
    ""Id"" uuid NOT NULL,
    ""BillNumber"" text NULL,
    ""SupplierId"" uuid NULL,
    ""Description"" text NULL,
    ""TotalAmount"" numeric NOT NULL,
    ""PaidAmount"" numeric NOT NULL,
    ""Status"" integer NOT NULL,
    ""BillDate"" date NULL,
    ""DueDate"" date NULL,
    ""PurchaseOrderId"" uuid NULL,
    ""LabOrderId"" uuid NULL,
    ""AttachmentUrl"" text NULL,
    ""Notes"" text NULL,
    ""BranchId"" uuid NULL,
    ""CreatedBy"" uuid NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_SupplierBills"" PRIMARY KEY (""Id"")
);
");

        // 8. CreateTable SupplierBillPayments (no inline FKs — added separately below)
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""SupplierBillPayments"" (
    ""Id"" uuid NOT NULL,
    ""SupplierBillId"" uuid NULL,
    ""Amount"" numeric NOT NULL,
    ""PaymentMethod"" text NULL,
    ""PaymentDate"" date NULL,
    ""ReferenceNumber"" text NULL,
    ""Notes"" text NULL,
    ""PaidBy"" uuid NULL,
    ""CashFlowTransactionId"" uuid NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_SupplierBillPayments"" PRIMARY KEY (""Id"")
);
");

        // 9. Create indexes
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'ApprovedById') THEN
        CREATE INDEX IF NOT EXISTS ""IX_OperationalExpenses_ApprovedById"" ON ""OperationalExpenses"" (""ApprovedById"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'OperationalExpenses' AND column_name = 'CashFlowTransactionId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_OperationalExpenses_CashFlowTransactionId"" ON ""OperationalExpenses"" (""CashFlowTransactionId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SupplierBillPayments' AND column_name = 'CashFlowTransactionId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_SupplierBillPayments_CashFlowTransactionId"" ON ""SupplierBillPayments"" (""CashFlowTransactionId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_SupplierBillPayments_SupplierBillId"" ON ""SupplierBillPayments"" (""SupplierBillId"");");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SupplierBills' AND column_name = 'BranchId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_SupplierBills_BranchId"" ON ""SupplierBills"" (""BranchId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SupplierBills' AND column_name = 'LabOrderId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_SupplierBills_LabOrderId"" ON ""SupplierBills"" (""LabOrderId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SupplierBills' AND column_name = 'PurchaseOrderId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_SupplierBills_PurchaseOrderId"" ON ""SupplierBills"" (""PurchaseOrderId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'SupplierBills' AND column_name = 'SupplierId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_SupplierBills_SupplierId"" ON ""SupplierBills"" (""SupplierId"");
    END IF;
END $$;
");

        // 10. AddForeignKey constraints — SupplierBills (idempotent)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierBills_Suppliers_SupplierId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Suppliers') THEN
            ALTER TABLE ""SupplierBills"" ADD CONSTRAINT ""FK_SupplierBills_Suppliers_SupplierId""
                FOREIGN KEY (""SupplierId"") REFERENCES ""Suppliers""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierBills_PurchaseOrders_PurchaseOrderId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'PurchaseOrders') THEN
            ALTER TABLE ""SupplierBills"" ADD CONSTRAINT ""FK_SupplierBills_PurchaseOrders_PurchaseOrderId""
                FOREIGN KEY (""PurchaseOrderId"") REFERENCES ""PurchaseOrders""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierBills_LabOrders_LabOrderId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
            ALTER TABLE ""SupplierBills"" ADD CONSTRAINT ""FK_SupplierBills_LabOrders_LabOrderId""
                FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierBills_Branches_BranchId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Branches') THEN
            ALTER TABLE ""SupplierBills"" ADD CONSTRAINT ""FK_SupplierBills_Branches_BranchId""
                FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        // 11. AddForeignKey constraints — SupplierBillPayments (idempotent)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierBillPayments_SupplierBills_SupplierBillId') THEN
        ALTER TABLE ""SupplierBillPayments"" ADD CONSTRAINT ""FK_SupplierBillPayments_SupplierBills_SupplierBillId""
            FOREIGN KEY (""SupplierBillId"") REFERENCES ""SupplierBills""(""Id"") ON DELETE CASCADE;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_SupplierBillPayments_CashFlowTransactions_CashFlowTransactionId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CashFlowTransactions') THEN
            ALTER TABLE ""SupplierBillPayments"" ADD CONSTRAINT ""FK_SupplierBillPayments_CashFlowTransactions_CashFlowTransactionId""
                FOREIGN KEY (""CashFlowTransactionId"") REFERENCES ""CashFlowTransactions""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
");

        // 12. AddForeignKey constraints — OperationalExpenses (idempotent)
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OperationalExpenses_CashFlowTransactions_CashFlowTransactionId') THEN
        ALTER TABLE ""OperationalExpenses"" ADD CONSTRAINT ""FK_OperationalExpenses_CashFlowTransactions_CashFlowTransactionId""
            FOREIGN KEY (""CashFlowTransactionId"") REFERENCES ""CashFlowTransactions"" (""Id"") ON DELETE RESTRICT;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_OperationalExpenses_Users_ApprovedById') THEN
        ALTER TABLE ""OperationalExpenses"" ADD CONSTRAINT ""FK_OperationalExpenses_Users_ApprovedById""
            FOREIGN KEY (""ApprovedById"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT;
    END IF;
END $$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_OperationalExpenses_CashFlowTransactions_CashFlowTransactionId",
            table: "OperationalExpenses");

        migrationBuilder.DropForeignKey(
            name: "FK_OperationalExpenses_Users_ApprovedById",
            table: "OperationalExpenses");

        migrationBuilder.DropTable(name: "SupplierBillPayments");
        migrationBuilder.DropTable(name: "SupplierBills");

        migrationBuilder.DropIndex(name: "IX_OperationalExpenses_CashFlowTransactionId", table: "OperationalExpenses");
        migrationBuilder.DropIndex(name: "IX_OperationalExpenses_ApprovedById", table: "OperationalExpenses");

        migrationBuilder.DropColumn(name: "IsPostedToLedger", table: "OperationalExpenses");
        migrationBuilder.DropColumn(name: "CashFlowTransactionId", table: "OperationalExpenses");
        migrationBuilder.DropColumn(name: "ApprovedById", table: "OperationalExpenses");
        migrationBuilder.DropColumn(name: "ApprovedAt", table: "OperationalExpenses");
        migrationBuilder.DropColumn(name: "ApprovalStatus", table: "OperationalExpenses");
        migrationBuilder.DropColumn(name: "ApprovalNotes", table: "OperationalExpenses");
    }
}
