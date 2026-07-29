using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Financial Integrity &amp; Audit Sprint migration:
/// - C3: Add reversal tracking columns to CashFlowTransactions (IsReversal, ReversalOfTransactionId, ReversedByTransactionId)
/// - A4: Add row version (Version) to Treasuries for optimistic concurrency
/// - H1: Add Reversal to FinancialCategory enum (no SQL change needed for PostgreSQL enums stored as strings)
/// All ADD COLUMN statements use IF NOT EXISTS for idempotency on PostgreSQL.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260613000000_AddFinancialIntegrityAuditSprint")]
public partial class AddFinancialIntegrityAuditSprint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // C3: Add IsReversal column to CashFlowTransactions
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" ADD COLUMN IF NOT EXISTS ""IsReversal"" boolean NOT NULL DEFAULT false");

        // C3: Add ReversalOfTransactionId column (FK to CashFlowTransactions)
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" ADD COLUMN IF NOT EXISTS ""ReversalOfTransactionId"" uuid NULL");

        // C3: Add ReversedByTransactionId column (FK to CashFlowTransactions)
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" ADD COLUMN IF NOT EXISTS ""ReversedByTransactionId"" uuid NULL");

        // C3: Create indexes for reversal FKs (idempotent)
        migrationBuilder.Sql(
            @"CREATE INDEX IF NOT EXISTS ""IX_CashFlowTransactions_ReversalOfTransactionId"" ON ""CashFlowTransactions"" (""ReversalOfTransactionId"")");

        migrationBuilder.Sql(
            @"CREATE INDEX IF NOT EXISTS ""IX_CashFlowTransactions_ReversedByTransactionId"" ON ""CashFlowTransactions"" (""ReversedByTransactionId"")");

        // C3: Create FKs for reversal columns (idempotent)
        migrationBuilder.Sql(
            @"DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_CashFlowTransactions_ReversalOfTransactionId'
                ) THEN
                    ALTER TABLE ""CashFlowTransactions""
                        ADD CONSTRAINT ""FK_CashFlowTransactions_CashFlowTransactions_ReversalOfTransactionId""
                        FOREIGN KEY (""ReversalOfTransactionId"") REFERENCES ""CashFlowTransactions""(""Id"")
                        ON DELETE RESTRICT;
                END IF;
            END $$");

        migrationBuilder.Sql(
            @"DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_CashFlowTransactions_ReversedByTransactionId'
                ) THEN
                    ALTER TABLE ""CashFlowTransactions""
                        ADD CONSTRAINT ""FK_CashFlowTransactions_CashFlowTransactions_ReversedByTransactionId""
                        FOREIGN KEY (""ReversedByTransactionId"") REFERENCES ""CashFlowTransactions""(""Id"")
                        ON DELETE RESTRICT;
                END IF;
            END $$");

        // A4: Optimistic concurrency on Treasuries is handled via PostgreSQL xmin
        // system column (UseXminAsConcurrencyToken). No explicit Version column needed.

        // Performance indexes for Treasury lookups by BranchId and composite (BranchId, Type)
        migrationBuilder.Sql(
            @"CREATE INDEX IF NOT EXISTS ""IX_Treasuries_BranchId"" ON ""Treasuries"" (""BranchId"")");

        migrationBuilder.Sql(
            @"CREATE INDEX IF NOT EXISTS ""IX_Treasuries_BranchId_Type"" ON ""Treasuries"" (""BranchId"", ""Type"")");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop Treasury indexes
        migrationBuilder.Sql(
            @"DROP INDEX IF EXISTS ""IX_Treasuries_BranchId_Type""");
        migrationBuilder.Sql(
            @"DROP INDEX IF EXISTS ""IX_Treasuries_BranchId""");

        // Drop FKs for reversal columns
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP CONSTRAINT IF EXISTS ""FK_CashFlowTransactions_CashFlowTransactions_ReversedByTransactionId""");
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP CONSTRAINT IF EXISTS ""FK_CashFlowTransactions_CashFlowTransactions_ReversalOfTransactionId""");

        // Drop reversal indexes
        migrationBuilder.Sql(
            @"DROP INDEX IF EXISTS ""IX_CashFlowTransactions_ReversedByTransactionId""");
        migrationBuilder.Sql(
            @"DROP INDEX IF EXISTS ""IX_CashFlowTransactions_ReversalOfTransactionId""");

        // Drop columns from CashFlowTransactions
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP COLUMN IF EXISTS ""ReversedByTransactionId""");
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP COLUMN IF EXISTS ""ReversalOfTransactionId""");
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP COLUMN IF EXISTS ""IsReversal""");
    }
}
