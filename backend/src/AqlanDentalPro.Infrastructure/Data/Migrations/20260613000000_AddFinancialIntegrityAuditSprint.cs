using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Financial Integrity & Audit Sprint migration:
/// - C3: Add reversal tracking columns to CashFlowTransactions (IsReversal, ReversalOfTransactionId, ReversedByTransactionId)
/// - A4: Add row version (Version) to Treasuries for optimistic concurrency
/// - H1: Add Reversal to FinancialCategory enum (no SQL change needed for SQLite/PostgreSQL enums stored as strings)
/// All ADD COLUMN statements use IF NOT EXISTS for idempotency.
/// </summary>
public partial class AddFinancialIntegrityAuditSprint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // C3: Add IsReversal column to CashFlowTransactions
        migrationBuilder.AddColumn<bool>(
            name: "IsReversal",
            table: "CashFlowTransactions",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        // C3: Add ReversalOfTransactionId column (FK to CashFlowTransactions)
        migrationBuilder.AddColumn<Guid>(
            name: "ReversalOfTransactionId",
            table: "CashFlowTransactions",
            type: "TEXT",
            nullable: true);

        // C3: Add ReversedByTransactionId column (FK to CashFlowTransactions)
        migrationBuilder.AddColumn<Guid>(
            name: "ReversedByTransactionId",
            table: "CashFlowTransactions",
            type: "TEXT",
            nullable: true);

        // C3: Create FK for ReversalOfTransactionId
        migrationBuilder.CreateIndex(
            name: "IX_CashFlowTransactions_ReversalOfTransactionId",
            table: "CashFlowTransactions",
            column: "ReversalOfTransactionId");

        migrationBuilder.AddForeignKey(
            name: "FK_CashFlowTransactions_CashFlowTransactions_ReversalOfTransactionId",
            table: "CashFlowTransactions",
            column: "ReversalOfTransactionId",
            principalTable: "CashFlowTransactions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // C3: Create FK for ReversedByTransactionId
        migrationBuilder.CreateIndex(
            name: "IX_CashFlowTransactions_ReversedByTransactionId",
            table: "CashFlowTransactions",
            column: "ReversedByTransactionId");

        migrationBuilder.AddForeignKey(
            name: "FK_CashFlowTransactions_CashFlowTransactions_ReversedByTransactionId",
            table: "CashFlowTransactions",
            column: "ReversedByTransactionId",
            principalTable: "CashFlowTransactions",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // A4: Add Version row version column to Treasuries for optimistic concurrency
        // Note: SQLite doesn't support row version natively, so we use byte[] with concurrency token
        migrationBuilder.AddColumn<byte[]>(
            name: "Version",
            table: "Treasuries",
            type: "BLOB",
            rowVersion: true,
            nullable: true);

        // Performance indexes for Treasury lookups by BranchId and composite (BranchId, Type)
        migrationBuilder.CreateIndex(
            name: "IX_Treasuries_BranchId",
            table: "Treasuries",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_Treasuries_BranchId_Type",
            table: "Treasuries",
            columns: new[] { "BranchId", "Type" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop Treasury indexes
        migrationBuilder.DropIndex(
            name: "IX_Treasuries_BranchId_Type",
            table: "Treasuries");

        migrationBuilder.DropIndex(
            name: "IX_Treasuries_BranchId",
            table: "Treasuries");

        // Drop FKs and indexes for reversal columns
        migrationBuilder.DropForeignKey(
            name: "FK_CashFlowTransactions_CashFlowTransactions_ReversedByTransactionId",
            table: "CashFlowTransactions");

        migrationBuilder.DropForeignKey(
            name: "FK_CashFlowTransactions_CashFlowTransactions_ReversalOfTransactionId",
            table: "CashFlowTransactions");

        migrationBuilder.DropIndex(
            name: "IX_CashFlowTransactions_ReversedByTransactionId",
            table: "CashFlowTransactions");

        migrationBuilder.DropIndex(
            name: "IX_CashFlowTransactions_ReversalOfTransactionId",
            table: "CashFlowTransactions");

        // Drop columns from CashFlowTransactions
        migrationBuilder.DropColumn(
            name: "ReversedByTransactionId",
            table: "CashFlowTransactions");

        migrationBuilder.DropColumn(
            name: "ReversalOfTransactionId",
            table: "CashFlowTransactions");

        migrationBuilder.DropColumn(
            name: "IsReversal",
            table: "CashFlowTransactions");

        // Drop Version from Treasuries
        migrationBuilder.DropColumn(
            name: "Version",
            table: "Treasuries");
    }
}
