using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Finance multi-currency completion:
    /// - Keeps the received payment currency separate from the account currency.
    /// - Stores the exchange-rate snapshot and applied balance amount.
    /// - Splits treasuries and cashflow by currency so YER/SAR/USD balances are auditable.
    ///
    /// Production note: run after backup. Existing rows are backfilled as YER with
    /// AppliedAmount = Amount, preserving legacy balances without deleting data.
    /// </summary>
    public partial class AddFinanceAccountCurrencyAndTreasuryCurrency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Payments"
                    ADD COLUMN IF NOT EXISTS "AccountCurrency" character varying(3) NOT NULL DEFAULT 'YER',
                    ADD COLUMN IF NOT EXISTS "ExchangeRateToAccountCurrency" numeric(18,6) NOT NULL DEFAULT 1,
                    ADD COLUMN IF NOT EXISTS "AppliedAmount" numeric(12,2) NOT NULL DEFAULT 0,
                    ADD COLUMN IF NOT EXISTS "ExchangeRateSource" character varying(50);

                UPDATE "Payments"
                SET "Currency" = COALESCE("Currency", 'YER'),
                    "AccountCurrency" = COALESCE(NULLIF("AccountCurrency", ''), 'YER'),
                    "ExchangeRateToAccountCurrency" = CASE
                        WHEN "ExchangeRateToAccountCurrency" IS NULL OR "ExchangeRateToAccountCurrency" <= 0 THEN 1
                        ELSE "ExchangeRateToAccountCurrency"
                    END,
                    "AppliedAmount" = CASE
                        WHEN "AppliedAmount" IS NULL OR "AppliedAmount" = 0 THEN "Amount"
                        ELSE "AppliedAmount"
                    END;

                ALTER TABLE "Contracts"
                    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';

                ALTER TABLE "Invoices"
                    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';

                ALTER TABLE "Treasuries"
                    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';

                UPDATE "Treasuries"
                SET "Currency" = COALESCE(NULLIF("Currency", ''), 'YER');

                ALTER TABLE "CashFlowTransactions"
                    ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'YER';

                UPDATE "CashFlowTransactions"
                SET "Currency" = COALESCE(NULLIF("Currency", ''), 'YER');

                DROP INDEX IF EXISTS "IX_Treasuries_BranchId_Type_Name_Unique";

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type_Currency_Name_Unique"
                    ON "Treasuries" ("BranchId", "Type", "Currency", "Name")
                    WHERE "IsActive" = true;

                CREATE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type_Currency"
                    ON "Treasuries" ("BranchId", "Type", "Currency");

                CREATE INDEX IF NOT EXISTS "IX_CashFlowTransactions_BranchId_Currency_TransactionDate"
                    ON "CashFlowTransactions" ("BranchId", "Currency", "TransactionDate");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_CashFlowTransactions_BranchId_Currency_TransactionDate";
                DROP INDEX IF EXISTS "IX_Treasuries_BranchId_Type_Currency";
                DROP INDEX IF EXISTS "IX_Treasuries_BranchId_Type_Currency_Name_Unique";

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Treasuries_BranchId_Type_Name_Unique"
                    ON "Treasuries" ("BranchId", "Type", "Name")
                    WHERE "IsActive" = true;

                ALTER TABLE "CashFlowTransactions" DROP COLUMN IF EXISTS "Currency";
                ALTER TABLE "Treasuries" DROP COLUMN IF EXISTS "Currency";
                ALTER TABLE "Invoices" DROP COLUMN IF EXISTS "Currency";
                ALTER TABLE "Contracts" DROP COLUMN IF EXISTS "Currency";
                ALTER TABLE "Payments" DROP COLUMN IF EXISTS "ExchangeRateSource";
                ALTER TABLE "Payments" DROP COLUMN IF EXISTS "AppliedAmount";
                ALTER TABLE "Payments" DROP COLUMN IF EXISTS "ExchangeRateToAccountCurrency";
                ALTER TABLE "Payments" DROP COLUMN IF EXISTS "AccountCurrency";
                """);
        }
    }
}
