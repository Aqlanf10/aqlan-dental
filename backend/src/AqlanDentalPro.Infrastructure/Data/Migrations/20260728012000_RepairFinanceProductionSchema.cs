using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Repairs finance objects that can be absent on databases whose historical
/// migration rows were reconciled before the corresponding schema was created.
/// Every statement is idempotent and preserves existing rows.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260728012000_RepairFinanceProductionSchema")]
public partial class RepairFinanceProductionSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "SupplierBills"
                ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER',
                ADD COLUMN IF NOT EXISTS "ExchangeRateToYer" numeric(18,6) NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS "ExchangeRateSource" character varying(30) NOT NULL DEFAULT 'same_currency',
                ADD COLUMN IF NOT EXISTS "IsOpeningBalance" boolean NOT NULL DEFAULT false;

            ALTER TABLE "SupplierBillPayments"
                ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER',
                ADD COLUMN IF NOT EXISTS "ExchangeRateToYer" numeric(18,6) NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS "ExchangeRateSource" character varying(30) NOT NULL DEFAULT 'same_currency';

            ALTER TABLE "JournalEntries"
                ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER',
                ADD COLUMN IF NOT EXISTS "ExchangeRateToYer" numeric(18,6) NOT NULL DEFAULT 1;

            CREATE INDEX IF NOT EXISTS "IX_SupplierBills_SupplierId_Currency"
                ON "SupplierBills" ("SupplierId", "Currency");
            CREATE INDEX IF NOT EXISTS "IX_SupplierBills_SupplierId_IsOpeningBalance"
                ON "SupplierBills" ("SupplierId", "IsOpeningBalance");

            CREATE TABLE IF NOT EXISTS "AccountingPeriods" (
                "Id" uuid NOT NULL,
                "BranchId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "StartDate" date NOT NULL,
                "EndDate" date NOT NULL,
                "Status" character varying(16) NOT NULL DEFAULT 'Open',
                "ClosedAt" timestamp with time zone NULL,
                "ClosedBy" uuid NULL,
                "Notes" character varying(1000) NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsActive" boolean NOT NULL,
                "DeletedAt" timestamp with time zone NULL,
                "DeletedBy" uuid NULL,
                CONSTRAINT "PK_AccountingPeriods" PRIMARY KEY ("Id")
            );
            CREATE INDEX IF NOT EXISTS "IX_AccountingPeriods_BranchId_StartDate_EndDate"
                ON "AccountingPeriods" ("BranchId", "StartDate", "EndDate");
            CREATE INDEX IF NOT EXISTS "IX_AccountingPeriods_ClosedBy"
                ON "AccountingPeriods" ("ClosedBy");

            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_AccountingPeriods_Branches_BranchId'
                ) THEN
                    ALTER TABLE "AccountingPeriods"
                        ADD CONSTRAINT "FK_AccountingPeriods_Branches_BranchId"
                        FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id")
                        ON DELETE RESTRICT;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_AccountingPeriods_Users_ClosedBy'
                ) THEN
                    ALTER TABLE "AccountingPeriods"
                        ADD CONSTRAINT "FK_AccountingPeriods_Users_ClosedBy"
                        FOREIGN KEY ("ClosedBy") REFERENCES "Users" ("Id")
                        ON DELETE RESTRICT;
                END IF;
            END $$;

            CREATE OR REPLACE FUNCTION prevent_closed_period_posting() RETURNS trigger AS $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM "AccountingPeriods" p
                    WHERE p."IsActive" = TRUE
                      AND p."Status" = 'Closed'
                      AND p."BranchId" = NEW."BranchId"
                      AND NEW."EntryDate" BETWEEN p."StartDate" AND p."EndDate"
                ) THEN
                    RAISE EXCEPTION 'Cannot post a journal entry to a closed accounting period';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            DROP TRIGGER IF EXISTS journal_entries_closed_period_guard ON "JournalEntries";
            CREATE TRIGGER journal_entries_closed_period_guard
                BEFORE INSERT ON "JournalEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_closed_period_posting();

            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'VaultTransfers'
                      AND column_name = 'Status'
                      AND data_type IN ('character varying', 'text')
                ) THEN
                    ALTER TABLE "VaultTransfers" ALTER COLUMN "Status" DROP DEFAULT;
                    ALTER TABLE "VaultTransfers" ALTER COLUMN "Status" TYPE integer USING CASE
                        WHEN "Status" IN ('Pending', '0') THEN 0
                        WHEN "Status" IN ('Approved', '1') THEN 1
                        WHEN "Status" IN ('Rejected', '2') THEN 2
                        ELSE 0
                    END;
                    ALTER TABLE "VaultTransfers" ALTER COLUMN "Status" SET DEFAULT 0;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Forward-only production repair. Existing financial data is preserved.
    }
}
