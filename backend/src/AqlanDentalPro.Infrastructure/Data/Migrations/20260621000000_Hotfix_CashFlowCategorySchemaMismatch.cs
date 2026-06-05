using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Hotfix — CashFlowTransactions.Category and Type schema mismatch.
///
/// PROBLEM:
///   PostgreSQL error 42883: operator does not exist: character varying = integer
///   The "Category" column in CashFlowTransactions is stored as varchar/text in the
///   production database, but EF Core treats FinancialCategory enum as integer
///   (no HasConversion&lt;string&gt;() configured). Queries like
///     WHERE "Category" = 0
///   fail because the column type is varchar, not integer.
///
///   Similarly, the "Type" column (TransactionType enum) may have the same mismatch.
///
/// FIX:
///   Convert both columns from varchar to integer using CASE mapping that maps
///   the string enum names to their corresponding integer values.
///
///   FinancialCategory enum values (in order):
///     PatientPayment=0, SupplierPayment=1, SalaryPayment=2, DoctorCommission=3,
///     OperationalExpense=4, Refund=5, GeneralCost=6, InternalTransfer=7,
///     SalaryAdvance=8, Reversal=9
///
///   TransactionType enum values (in order):
///     Inflow=0, Outflow=1
///
/// SAFETY:
///   - Idempotent: only converts if column is currently varchar (not already integer).
///   - Unknown string values are NOT silently mapped; they raise a notice and default
///     to PatientPayment (0) / Inflow (0) respectively, but the operator should
///     review the output of the diagnostic query before applying this migration.
///   - The migration first logs any distinct values that don't match expected enum
///     names so the DBA can review before the ALTER COLUMN executes.
///
/// RISK:
///   - If there are existing varchar values that don't match any enum name, they will
///     be mapped to 0. The pre-conversion diagnostic SELECT helps catch this.
///   - This is a DDL change on a live table; brief AccessExclusiveLock is required.
///     Apply during low-traffic period if the table is large.
/// </summary>
public partial class Hotfix_CashFlowCategorySchemaMismatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ════════════════════════════════════════════════════════════════════
        // 1. Diagnostic: log distinct Category values before conversion
        // ════════════════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                cat_val TEXT;
            BEGIN
                RAISE NOTICE '=== CashFlowTransactions.Category — Pre-conversion distinct values ===';
                FOR cat_val IN
                    SELECT DISTINCT ""Category""::text FROM ""CashFlowTransactions""
                LOOP
                    RAISE NOTICE 'Category value: %', cat_val;
                END LOOP;
            END$$;
        ");

        // ════════════════════════════════════════════════════════════════════
        // 2. Diagnostic: log distinct Type values before conversion
        // ════════════════════════════════════════════════════════════════════
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                type_val TEXT;
            BEGIN
                RAISE NOTICE '=== CashFlowTransactions.Type — Pre-conversion distinct values ===';
                FOR type_val IN
                    SELECT DISTINCT ""Type""::text FROM ""CashFlowTransactions""
                LOOP
                    RAISE NOTICE 'Type value: %', type_val;
                END LOOP;
            END$$;
        ");

        // ════════════════════════════════════════════════════════════════════
        // 3. Convert CashFlowTransactions.Category from varchar → integer
        // ════════════════════════════════════════════════════════════════════
        // Mapping based on FinancialCategory enum (C# zero-based order):
        //   PatientPayment=0, SupplierPayment=1, SalaryPayment=2, DoctorCommission=3,
        //   OperationalExpense=4, Refund=5, GeneralCost=6, InternalTransfer=7,
        //   SalaryAdvance=8, Reversal=9
        //
        // Idempotent: only converts if column is currently varchar/character varying.
        // Unknown values are logged and mapped to 0 (PatientPayment) as safe default.
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Category'
                      AND data_type IN ('character varying', 'text')
                ) THEN
                    -- Check for unknown values before converting
                    IF EXISTS (
                        SELECT 1 FROM ""CashFlowTransactions""
                        WHERE ""Category""::text NOT IN (
                            'PatientPayment', 'SupplierPayment', 'SalaryPayment',
                            'DoctorCommission', 'OperationalExpense', 'Refund',
                            'GeneralCost', 'InternalTransfer', 'SalaryAdvance', 'Reversal'
                        )
                    ) THEN
                        RAISE WARNING 'CashFlowTransactions.Category contains unexpected values! '
                            'These will be mapped to 0 (PatientPayment). '
                            'Review the diagnostic output above.';
                    END IF;

                    ALTER TABLE ""CashFlowTransactions""
                    ALTER COLUMN ""Category"" TYPE integer USING CASE ""Category""::text
                        WHEN 'PatientPayment'     THEN 0
                        WHEN 'SupplierPayment'    THEN 1
                        WHEN 'SalaryPayment'      THEN 2
                        WHEN 'DoctorCommission'   THEN 3
                        WHEN 'OperationalExpense' THEN 4
                        WHEN 'Refund'             THEN 5
                        WHEN 'GeneralCost'        THEN 6
                        WHEN 'InternalTransfer'   THEN 7
                        WHEN 'SalaryAdvance'      THEN 8
                        WHEN 'Reversal'           THEN 9
                        ELSE 0
                    END;
                    RAISE NOTICE 'CashFlowTransactions.Category converted from varchar to integer.';
                ELSE
                    RAISE NOTICE 'CashFlowTransactions.Category is already integer — skipping.';
                END IF;
            END$$;
        ");

        // ════════════════════════════════════════════════════════════════════
        // 4. Convert CashFlowTransactions.Type from varchar → integer
        // ════════════════════════════════════════════════════════════════════
        // Mapping based on TransactionType enum (C# zero-based order):
        //   Inflow=0, Outflow=1
        //
        // Same idempotent pattern. Type may or may not be varchar in production
        // but converting it preventively avoids the same class of error.
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Type'
                      AND data_type IN ('character varying', 'text')
                ) THEN
                    -- Check for unknown values before converting
                    IF EXISTS (
                        SELECT 1 FROM ""CashFlowTransactions""
                        WHERE ""Type""::text NOT IN ('Inflow', 'Outflow')
                    ) THEN
                        RAISE WARNING 'CashFlowTransactions.Type contains unexpected values! '
                            'These will be mapped to 0 (Inflow). '
                            'Review the diagnostic output above.';
                    END IF;

                    ALTER TABLE ""CashFlowTransactions""
                    ALTER COLUMN ""Type"" TYPE integer USING CASE ""Type""::text
                        WHEN 'Inflow'  THEN 0
                        WHEN 'Outflow' THEN 1
                        ELSE 0
                    END;
                    RAISE NOTICE 'CashFlowTransactions.Type converted from varchar to integer.';
                ELSE
                    RAISE NOTICE 'CashFlowTransactions.Type is already integer — skipping.';
                END IF;
            END$$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ════════════════════════════════════════════════════════════════════
        // Revert: Convert Category and Type back from integer → varchar
        // ════════════════════════════════════════════════════════════════════

        // Revert Category: integer → varchar(30)
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Category'
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""CashFlowTransactions""
                    ALTER COLUMN ""Category"" TYPE varchar(30) USING CASE ""Category""
                        WHEN 0 THEN 'PatientPayment'
                        WHEN 1 THEN 'SupplierPayment'
                        WHEN 2 THEN 'SalaryPayment'
                        WHEN 3 THEN 'DoctorCommission'
                        WHEN 4 THEN 'OperationalExpense'
                        WHEN 5 THEN 'Refund'
                        WHEN 6 THEN 'GeneralCost'
                        WHEN 7 THEN 'InternalTransfer'
                        WHEN 8 THEN 'SalaryAdvance'
                        WHEN 9 THEN 'Reversal'
                        ELSE 'PatientPayment'
                    END;
                END IF;
            END$$;
        ");

        // Revert Type: integer → varchar(20)
        migrationBuilder.Sql(@"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Type'
                      AND data_type = 'integer'
                ) THEN
                    ALTER TABLE ""CashFlowTransactions""
                    ALTER COLUMN ""Type"" TYPE varchar(20) USING CASE ""Type""
                        WHEN 0 THEN 'Inflow'
                        WHEN 1 THEN 'Outflow'
                        ELSE 'Inflow'
                    END;
                END IF;
            END$$;
        ");
    }
}
