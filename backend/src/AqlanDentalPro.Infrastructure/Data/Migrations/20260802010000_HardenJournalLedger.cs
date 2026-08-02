using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Enforces the journal invariants in PostgreSQL so writes outside the API cannot
/// create invalid, duplicate, or closed-period ledger data.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260802010000_HardenJournalLedger")]
public partial class HardenJournalLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM "JournalLines"
                    WHERE NOT (("Debit" > 0 AND "Credit" = 0)
                            OR ("Credit" > 0 AND "Debit" = 0))
                ) THEN
                    RAISE EXCEPTION 'W11 preflight failed: JournalLines contain invalid debit/credit sides';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM "JournalEntries" entry
                    LEFT JOIN "JournalLines" line ON line."JournalEntryId" = entry."Id"
                    GROUP BY entry."Id"
                    HAVING COUNT(line."Id") < 2
                        OR COALESCE(SUM(line."Debit"), 0) <> COALESCE(SUM(line."Credit"), 0)
                ) THEN
                    RAISE EXCEPTION 'W11 preflight failed: JournalEntries contain fewer than two lines or are unbalanced';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM "JournalEntries"
                    WHERE "IsReversal" = FALSE
                      AND "FinancialDocumentType" <> 'YearEndClosing'
                    GROUP BY "BranchId", "FinancialDocumentType", "FinancialDocumentId"
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'W11 preflight failed: duplicate journal source identities exist';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM "JournalEntries"
                    WHERE "IsReversal" = FALSE
                      AND "FinancialDocumentType" = 'YearEndClosing'
                    GROUP BY "BranchId", "FinancialDocumentType", "FinancialDocumentId", "Currency"
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'W11 preflight failed: duplicate year-end source identities exist per currency';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM "JournalEntries"
                    WHERE "ReversalOfEntryId" IS NOT NULL
                    GROUP BY "ReversalOfEntryId"
                    HAVING COUNT(*) > 1
                ) THEN
                    RAISE EXCEPTION 'W11 preflight failed: an entry has more than one reversal';
                END IF;
            END $$;

            ALTER TABLE "JournalLines"
                DROP CONSTRAINT IF EXISTS "CK_JournalLines_DebitCreditMutual";
            ALTER TABLE "JournalLines"
                ADD CONSTRAINT "CK_JournalLines_DebitCreditMutual"
                CHECK (("Debit" > 0 AND "Credit" = 0)
                    OR ("Credit" > 0 AND "Debit" = 0));

            CREATE UNIQUE INDEX "IX_JournalEntries_BranchId_FinancialDocumentType_FinancialDocumentId"
                ON "JournalEntries" ("BranchId", "FinancialDocumentType", "FinancialDocumentId")
                WHERE "IsReversal" = FALSE AND "FinancialDocumentType" <> 'YearEndClosing';

            CREATE UNIQUE INDEX "UX_JournalEntries_YearEndSourceCurrency"
                ON "JournalEntries" ("BranchId", "FinancialDocumentType", "FinancialDocumentId", "Currency")
                WHERE "IsReversal" = FALSE AND "FinancialDocumentType" = 'YearEndClosing';

            DROP INDEX IF EXISTS "IX_JournalEntries_ReversalOfEntryId";
            CREATE UNIQUE INDEX "IX_JournalEntries_ReversalOfEntryId"
                ON "JournalEntries" ("ReversalOfEntryId")
                WHERE "ReversalOfEntryId" IS NOT NULL;

            CREATE TABLE "JournalEntryNumberSequences" (
                "EntryDate" date NOT NULL,
                "LastSequence" integer NOT NULL,
                CONSTRAINT "PK_JournalEntryNumberSequences" PRIMARY KEY ("EntryDate"),
                CONSTRAINT "CK_JournalEntryNumberSequences_Positive" CHECK ("LastSequence" > 0)
            );

            INSERT INTO "JournalEntryNumberSequences" ("EntryDate", "LastSequence")
            SELECT
                to_date(substring(entry."EntryNumber" from 4 for 8), 'YYYYMMDD'),
                MAX(substring(entry."EntryNumber" from '([0-9]+)$')::integer)
            FROM "JournalEntries" entry
            WHERE entry."EntryNumber" ~ '^JE-[0-9]{8}-[0-9]+$'
            GROUP BY to_date(substring(entry."EntryNumber" from 4 for 8), 'YYYYMMDD');

            CREATE OR REPLACE FUNCTION validate_journal_entry_balance(target_entry_id uuid)
            RETURNS void AS $$
            DECLARE
                line_count integer;
                debit_total numeric;
                credit_total numeric;
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM "JournalEntries" WHERE "Id" = target_entry_id) THEN
                    RETURN;
                END IF;

                SELECT COUNT(*), COALESCE(SUM("Debit"), 0), COALESCE(SUM("Credit"), 0)
                INTO line_count, debit_total, credit_total
                FROM "JournalLines"
                WHERE "JournalEntryId" = target_entry_id;

                IF line_count < 2 OR debit_total <> credit_total THEN
                    RAISE EXCEPTION
                        'Journal entry % must have at least two lines and balance (debit %, credit %)',
                        target_entry_id, debit_total, credit_total
                        USING ERRCODE = '23514';
                END IF;
            END;
            $$ LANGUAGE plpgsql;

            CREATE OR REPLACE FUNCTION enforce_journal_entry_balance_from_entry()
            RETURNS trigger AS $$
            BEGIN
                PERFORM validate_journal_entry_balance(CASE WHEN TG_OP = 'DELETE' THEN OLD."Id" ELSE NEW."Id" END);
                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE OR REPLACE FUNCTION enforce_journal_entry_balance_from_line()
            RETURNS trigger AS $$
            BEGIN
                IF TG_OP IN ('UPDATE', 'DELETE') THEN
                    PERFORM validate_journal_entry_balance(OLD."JournalEntryId");
                END IF;
                IF TG_OP IN ('INSERT', 'UPDATE')
                   AND (TG_OP <> 'UPDATE' OR NEW."JournalEntryId" IS DISTINCT FROM OLD."JournalEntryId") THEN
                    PERFORM validate_journal_entry_balance(NEW."JournalEntryId");
                END IF;
                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE CONSTRAINT TRIGGER journal_entries_balance_guard
                AFTER INSERT OR UPDATE ON "JournalEntries"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION enforce_journal_entry_balance_from_entry();

            CREATE CONSTRAINT TRIGGER journal_lines_balance_guard
                AFTER INSERT OR UPDATE OR DELETE ON "JournalLines"
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION enforce_journal_entry_balance_from_line();

            CREATE OR REPLACE FUNCTION journal_date_is_closed(target_branch_id uuid, target_date date)
            RETURNS boolean AS $$
            BEGIN
                RETURN EXISTS (
                    SELECT 1
                    FROM "AccountingPeriods" period
                    WHERE period."IsActive" = TRUE
                      AND period."Status" = 'Closed'
                      AND period."BranchId" = target_branch_id
                      AND target_date BETWEEN period."StartDate" AND period."EndDate"
                );
            END;
            $$ LANGUAGE plpgsql STABLE;

            CREATE OR REPLACE FUNCTION prevent_closed_period_journal_entry_change()
            RETURNS trigger AS $$
            BEGIN
                IF TG_OP = 'UPDATE'
                   AND OLD."ReversedByEntryId" IS NULL
                   AND NEW."ReversedByEntryId" IS NOT NULL
                   AND (to_jsonb(NEW) - 'ReversedByEntryId' - 'UpdatedAt')
                       = (to_jsonb(OLD) - 'ReversedByEntryId' - 'UpdatedAt')
                   AND EXISTS (
                       SELECT 1
                       FROM "JournalEntries" reversal
                       WHERE reversal."Id" = NEW."ReversedByEntryId"
                         AND reversal."ReversalOfEntryId" = OLD."Id"
                         AND reversal."IsReversal" = TRUE
                         AND NOT journal_date_is_closed(reversal."BranchId", reversal."EntryDate")
                   ) THEN
                    RETURN NEW;
                END IF;

                IF TG_OP IN ('UPDATE', 'DELETE')
                   AND journal_date_is_closed(OLD."BranchId", OLD."EntryDate") THEN
                    RAISE EXCEPTION 'Cannot modify or delete a journal entry in a closed accounting period'
                        USING ERRCODE = '23514';
                END IF;

                IF TG_OP IN ('INSERT', 'UPDATE')
                   AND journal_date_is_closed(NEW."BranchId", NEW."EntryDate") THEN
                    RAISE EXCEPTION 'Cannot insert or move a journal entry into a closed accounting period'
                        USING ERRCODE = '23514';
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE OR REPLACE FUNCTION prevent_closed_period_journal_line_change()
            RETURNS trigger AS $$
            DECLARE
                old_is_closed boolean := FALSE;
                new_is_closed boolean := FALSE;
            BEGIN
                IF TG_OP IN ('UPDATE', 'DELETE') THEN
                    SELECT journal_date_is_closed(entry."BranchId", entry."EntryDate")
                    INTO old_is_closed
                    FROM "JournalEntries" entry
                    WHERE entry."Id" = OLD."JournalEntryId";
                END IF;

                IF TG_OP IN ('INSERT', 'UPDATE') THEN
                    SELECT journal_date_is_closed(entry."BranchId", entry."EntryDate")
                    INTO new_is_closed
                    FROM "JournalEntries" entry
                    WHERE entry."Id" = NEW."JournalEntryId";
                END IF;

                IF COALESCE(old_is_closed, FALSE) OR COALESCE(new_is_closed, FALSE) THEN
                    RAISE EXCEPTION 'Cannot modify journal lines in a closed accounting period'
                        USING ERRCODE = '23514';
                END IF;

                IF TG_OP = 'DELETE' THEN
                    RETURN OLD;
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS journal_entries_closed_period_guard ON "JournalEntries";
            CREATE TRIGGER journal_entries_closed_period_guard
                BEFORE INSERT OR UPDATE OR DELETE ON "JournalEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_closed_period_journal_entry_change();

            CREATE TRIGGER journal_lines_closed_period_guard
                BEFORE INSERT OR UPDATE OR DELETE ON "JournalLines"
                FOR EACH ROW EXECUTE FUNCTION prevent_closed_period_journal_line_change();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS journal_lines_closed_period_guard ON "JournalLines";
            DROP TRIGGER IF EXISTS journal_entries_closed_period_guard ON "JournalEntries";
            DROP TRIGGER IF EXISTS journal_lines_balance_guard ON "JournalLines";
            DROP TRIGGER IF EXISTS journal_entries_balance_guard ON "JournalEntries";

            DROP FUNCTION IF EXISTS prevent_closed_period_journal_line_change();
            DROP FUNCTION IF EXISTS prevent_closed_period_journal_entry_change();
            DROP FUNCTION IF EXISTS journal_date_is_closed(uuid, date);
            DROP FUNCTION IF EXISTS enforce_journal_entry_balance_from_line();
            DROP FUNCTION IF EXISTS enforce_journal_entry_balance_from_entry();
            DROP FUNCTION IF EXISTS validate_journal_entry_balance(uuid);

            DROP TABLE IF EXISTS "JournalEntryNumberSequences";
            DROP INDEX IF EXISTS "UX_JournalEntries_YearEndSourceCurrency";
            DROP INDEX IF EXISTS "IX_JournalEntries_BranchId_FinancialDocumentType_FinancialDocumentId";
            DROP INDEX IF EXISTS "IX_JournalEntries_ReversalOfEntryId";
            CREATE INDEX "IX_JournalEntries_ReversalOfEntryId"
                ON "JournalEntries" ("ReversalOfEntryId");

            ALTER TABLE "JournalLines"
                DROP CONSTRAINT IF EXISTS "CK_JournalLines_DebitCreditMutual";
            ALTER TABLE "JournalLines"
                ADD CONSTRAINT "CK_JournalLines_DebitCreditMutual"
                CHECK ("Debit" >= 0 AND "Credit" >= 0 AND ("Debit" > 0 OR "Credit" > 0));

            CREATE OR REPLACE FUNCTION prevent_closed_period_posting() RETURNS trigger AS $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "AccountingPeriods" period
                    WHERE period."IsActive" = TRUE
                      AND period."Status" = 'Closed'
                      AND period."BranchId" = NEW."BranchId"
                      AND NEW."EntryDate" BETWEEN period."StartDate" AND period."EndDate"
                ) THEN
                    RAISE EXCEPTION 'Cannot post a journal entry to a closed accounting period';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER journal_entries_closed_period_guard
                BEFORE INSERT ON "JournalEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_closed_period_posting();
            """);
    }
}
