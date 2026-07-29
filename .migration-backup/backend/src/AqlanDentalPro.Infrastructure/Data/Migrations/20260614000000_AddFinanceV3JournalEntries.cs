using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Finance V3 — Double-Entry Bookkeeping migration:
/// 1. Create JournalEntries table
/// 2. Create JournalLines table
/// 3. Add DepositSource column to VaultTransfers
/// 4. Add TreasuryId column to CashierSessions
/// 5. Add TreasuryId column to CashFlowTransactions (if not already present)
/// 6. Add FK from CashFlowTransactions.TreasuryId → Treasuries.Id
/// 7. Add index on CashFlowTransactions.TreasuryId
/// 8. Deterministic backfill: CashFlowTransactions.TreasuryId ← CashierSessions.TreasuryId
///    NOTE: This is a BEST-EFFORT operation. It only backfills where
///    CashierSessions.TreasuryId is populated. Historical CashFlowTransactions
///    with null CashierSessionId or null session TreasuryId will remain null.
///    Do NOT claim full historical backfill coverage.
/// 9. Leave NULL where CashierSessions.TreasuryId is null
///
/// Preflight: Check how many CashFlowTransactions will have null TreasuryId after migration
/// SELECT COUNT(*) FROM "CashFlowTransactions" WHERE "TreasuryId" IS NULL;
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260614000000_AddFinanceV3JournalEntries")]
public partial class AddFinanceV3JournalEntries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ─── 1. Create JournalEntries table ───────────────────────────────────
        migrationBuilder.CreateTable(
            name: "JournalEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EntryNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                FinancialDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                FinancialDocumentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                CashierSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                TreasuryId = table.Column<Guid>(type: "uuid", nullable: true),
                PerformedBy = table.Column<Guid>(type: "uuid", nullable: false),
                IsReversal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ReversalOfEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                ReversedByEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                IsPosted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JournalEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_JournalEntries_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_JournalEntries_CashierSessions_CashierSessionId",
                    column: x => x.CashierSessionId,
                    principalTable: "CashierSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_JournalEntries_Treasuries_TreasuryId",
                    column: x => x.TreasuryId,
                    principalTable: "Treasuries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_JournalEntries_Users_PerformedBy",
                    column: x => x.PerformedBy,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_JournalEntries_JournalEntries_ReversalOfEntryId",
                    column: x => x.ReversalOfEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_JournalEntries_JournalEntries_ReversedByEntryId",
                    column: x => x.ReversedByEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_EntryNumber",
            table: "JournalEntries",
            column: "EntryNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_BranchId",
            table: "JournalEntries",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_EntryDate",
            table: "JournalEntries",
            column: "EntryDate");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_FinancialDocumentType",
            table: "JournalEntries",
            column: "FinancialDocumentType");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_IsPosted",
            table: "JournalEntries",
            column: "IsPosted");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_IsReversal",
            table: "JournalEntries",
            column: "IsReversal");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_CashierSessionId",
            table: "JournalEntries",
            column: "CashierSessionId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_TreasuryId",
            table: "JournalEntries",
            column: "TreasuryId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_PerformedBy",
            table: "JournalEntries",
            column: "PerformedBy");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_BranchId_EntryDate",
            table: "JournalEntries",
            columns: new[] { "BranchId", "EntryDate" });

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_ReversalOfEntryId",
            table: "JournalEntries",
            column: "ReversalOfEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_ReversedByEntryId",
            table: "JournalEntries",
            column: "ReversedByEntryId");

        // ─── 2. Create JournalLines table ─────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "JournalLines",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                Debit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                Credit = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JournalLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_JournalLines_JournalEntries_JournalEntryId",
                    column: x => x.JournalEntryId,
                    principalTable: "JournalEntries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_JournalLines_Branches_BranchId",
                    column: x => x.BranchId,
                    principalTable: "Branches",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint(
                    "CK_JournalLines_DebitCreditMutual",
                    "\"Debit\" >= 0 AND \"Credit\" >= 0 AND (\"Debit\" > 0 OR \"Credit\" > 0)");
            });

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_JournalEntryId",
            table: "JournalLines",
            column: "JournalEntryId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_AccountType",
            table: "JournalLines",
            column: "AccountType");

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_AccountId",
            table: "JournalLines",
            column: "AccountId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_BranchId",
            table: "JournalLines",
            column: "BranchId");

        migrationBuilder.CreateIndex(
            name: "IX_JournalLines_AccountType_AccountId",
            table: "JournalLines",
            columns: new[] { "AccountType", "AccountId" });

        // ─── 3. Add DepositSource column to VaultTransfers ────────────────────
        migrationBuilder.AddColumn<string>(
            name: "DepositSource",
            table: "VaultTransfers",
            type: "text",
            nullable: true);

        // ─── 4. Add TreasuryId column to CashierSessions ──────────────────────
        migrationBuilder.AddColumn<Guid>(
            name: "TreasuryId",
            table: "CashierSessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CashierSessions_TreasuryId",
            table: "CashierSessions",
            column: "TreasuryId");

        migrationBuilder.AddForeignKey(
            name: "FK_CashierSessions_Treasuries_TreasuryId",
            table: "CashierSessions",
            column: "TreasuryId",
            principalTable: "Treasuries",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // ─── 5. Add TreasuryId column to CashFlowTransactions (idempotent) ─────
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" ADD COLUMN IF NOT EXISTS ""TreasuryId"" uuid NULL");

        // ─── 6 & 7. Add index and FK for CashFlowTransactions.TreasuryId ──────
        migrationBuilder.Sql(
            @"CREATE INDEX IF NOT EXISTS ""IX_CashFlowTransactions_TreasuryId"" ON ""CashFlowTransactions"" (""TreasuryId"")");

        migrationBuilder.Sql(
            @"DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint WHERE conname = 'FK_CashFlowTransactions_Treasuries_TreasuryId'
                ) THEN
                    ALTER TABLE ""CashFlowTransactions""
                        ADD CONSTRAINT ""FK_CashFlowTransactions_Treasuries_TreasuryId""
                        FOREIGN KEY (""TreasuryId"") REFERENCES ""Treasuries""(""Id"")
                        ON DELETE SET NULL;
                END IF;
            END $$");

        // ─── 8 & 9. Best-effort backfill: CashFlowTransactions.TreasuryId ← CashierSessions.TreasuryId
        //     Only update rows where TreasuryId is still NULL and the session has a TreasuryId.
        //     Leave NULL where CashierSession.TreasuryId is null (or CashierSessionId is null).
        //     This is a BEST-EFFORT operation — it only backfills where CashierSessions.TreasuryId
        //     is populated. Do NOT claim full historical backfill coverage. ──
        migrationBuilder.Sql(
            @"UPDATE ""CashFlowTransactions"" cft
              SET ""TreasuryId"" = cs.""TreasuryId""
              FROM ""CashierSessions"" cs
              WHERE cft.""CashierSessionId"" = cs.""Id""
                AND cs.""TreasuryId"" IS NOT NULL
                AND cft.""TreasuryId"" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ─── Drop JournalLines ─────────────────────────────────────────────────
        migrationBuilder.DropTable(
            name: "JournalLines");

        // ─── Drop JournalEntries ───────────────────────────────────────────────
        migrationBuilder.DropTable(
            name: "JournalEntries");

        // ─── Drop DepositSource from VaultTransfers ────────────────────────────
        migrationBuilder.DropColumn(
            name: "DepositSource",
            table: "VaultTransfers");

        // ─── Drop TreasuryId from CashierSessions ──────────────────────────────
        migrationBuilder.DropForeignKey(
            name: "FK_CashierSessions_Treasuries_TreasuryId",
            table: "CashierSessions");

        migrationBuilder.DropIndex(
            name: "IX_CashierSessions_TreasuryId",
            table: "CashierSessions");

        migrationBuilder.DropColumn(
            name: "TreasuryId",
            table: "CashierSessions");

        // ─── Drop TreasuryId from CashFlowTransactions ─────────────────────────
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP CONSTRAINT IF EXISTS ""FK_CashFlowTransactions_Treasuries_TreasuryId""");
        migrationBuilder.Sql(
            @"DROP INDEX IF EXISTS ""IX_CashFlowTransactions_TreasuryId""");
        migrationBuilder.Sql(
            @"ALTER TABLE ""CashFlowTransactions"" DROP COLUMN IF EXISTS ""TreasuryId""");
    }
}
