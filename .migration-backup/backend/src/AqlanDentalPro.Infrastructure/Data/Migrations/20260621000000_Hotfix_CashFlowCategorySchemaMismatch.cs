using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Hotfix - CashFlowTransactions.Category and Type schema mismatch.
///
/// PROBLEM:
///   PostgreSQL error 42883: operator does not exist: character varying = integer
///   The "Category" and "Type" columns in CashFlowTransactions are stored as
///   varchar/text in the production database, but EF Core treats the enums
///   (FinancialCategory, TransactionType) as integers. Queries like
///     WHERE "Category" = 0
///   fail because the column type is varchar, not integer.
///
///   EF Core sends integer parameter values for INSERT. PostgreSQL implicitly
///   casts integer 0 to text '0' when inserting into a varchar column. So the
///   stored values may be numeric strings ('0','1','2',...) OR enum names
///   ('PatientPayment','SupplierPayment',...) depending on the provider behavior
///   and any prior HasConversion configuration.
///
/// FIX:
///   Convert both columns from varchar to integer. The CASE mapping handles
///   BOTH possible varchar formats:
///     - Enum name strings: 'PatientPayment', 'Inflow', etc.
///     - Numeric strings: '0', '1', etc. (from PostgreSQL implicit int-to-text cast)
///
///   FinancialCategory enum values:
///     PatientPayment=0, SupplierPayment=1, SalaryPayment=2, DoctorCommission=3,
///     OperationalExpense=4, Refund=5, GeneralCost=6, InternalTransfer=7,
///     SalaryAdvance=8, Reversal=9
///
///   TransactionType enum values:
///     Inflow=0, Outflow=1
///
/// SAFETY (STRICT - no silent financial recoding):
///   - Idempotent: only converts if column is currently varchar (not already integer).
///   - Pre-conversion validation: if ANY unknown string value exists in Category or Type
///     (not a known enum name AND not a valid numeric string in range), the migration
///     RAISES EXCEPTION and FAILS immediately. No fallback, no ELSE 0.
///   - This prevents silent misclassification of financial transactions.
///   - Diagnostic queries log all distinct values before the validation check.
///
/// RISK:
///   - If there are existing varchar values that match neither enum names nor numeric
///     strings in valid range, the migration will FAIL. The DBA must manually resolve
///     the unknown values first, then re-run the migration.
///   - This is a DDL change on a live table; brief AccessExclusiveLock is required.
///     Apply during low-traffic period if the table is large.
/// </summary>
public partial class Hotfix_CashFlowCategorySchemaMismatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ====================================================================
        // 1. Diagnostic: log distinct Category values before conversion
        // ====================================================================
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                cat_val TEXT;
            BEGIN
                RAISE NOTICE '=== CashFlowTransactions.Category - Pre-conversion distinct values ===';
                FOR cat_val IN
                    SELECT DISTINCT ""Category""::text FROM ""CashFlowTransactions""
                LOOP
                    RAISE NOTICE 'Category value: %', cat_val;
                END LOOP;
            END$$;
        ");

        // ====================================================================
        // 2. Diagnostic: log distinct Type values before conversion
        // ====================================================================
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                type_val TEXT;
            BEGIN
                RAISE NOTICE '=== CashFlowTransactions.Type - Pre-conversion distinct values ===';
                FOR type_val IN
                    SELECT DISTINCT ""Type""::text FROM ""CashFlowTransactions""
                LOOP
                    RAISE NOTICE 'Type value: %', type_val;
                END LOOP;
            END$$;
        ");

        // ====================================================================
        // 3. Convert CashFlowTransactions.Category from varchar to integer
        // ====================================================================
        // Handles BOTH formats: enum name strings ('PatientPayment') AND
        // numeric strings ('0','1',...) from PostgreSQL implicit int-to-text cast.
        // STRICT: Any unknown value causes RAISE EXCEPTION. No silent fallback.
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                unknown_cat TEXT;
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Category'
                      AND data_type IN ('character varying', 'text')
                ) THEN
                    -- STRICT VALIDATION: find first value that is neither a known
                    -- enum name, nor a valid numeric string (0-9), nor a legacy alias
                    SELECT ""Category""::text INTO unknown_cat
                    FROM ""CashFlowTransactions""
                    WHERE ""Category""::text NOT IN (
                        'PatientPayment', 'SupplierPayment', 'SalaryPayment',
                        'DoctorCommission', 'OperationalExpense', 'Refund',
                        'GeneralCost', 'InternalTransfer', 'SalaryAdvance', 'Reversal',
                        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                        'Installment'
                    )
                    LIMIT 1;

                    IF unknown_cat IS NOT NULL THEN
                        RAISE EXCEPTION 'Unknown CashFlowTransactions.Category value: %', unknown_cat;
                    END IF;

                    -- All values validated; safe to convert.
                    -- CASE handles enum name strings, numeric strings, and legacy aliases.
                    -- 'Installment' is a legacy alias for PatientPayment (contract installments
                    -- are patient payments toward a treatment plan).
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
                        WHEN 'Installment'        THEN 0
                        WHEN '0' THEN 0
                        WHEN '1' THEN 1
                        WHEN '2' THEN 2
                        WHEN '3' THEN 3
                        WHEN '4' THEN 4
                        WHEN '5' THEN 5
                        WHEN '6' THEN 6
                        WHEN '7' THEN 7
                        WHEN '8' THEN 8
                        WHEN '9' THEN 9
                    END;
                    RAISE NOTICE 'CashFlowTransactions.Category converted from varchar to integer.';
                ELSE
                    RAISE NOTICE 'CashFlowTransactions.Category is already integer; skipping.';
                END IF;
            END$$;
        ");

        // ====================================================================
        // 4. Convert CashFlowTransactions.Type from varchar to integer
        // ====================================================================
        // Handles BOTH formats: enum name strings ('Inflow','Outflow') AND
        // numeric strings ('0','1') from PostgreSQL implicit int-to-text cast.
        // STRICT: Any unknown value causes RAISE EXCEPTION. No silent fallback.
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                unknown_type TEXT;
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Type'
                      AND data_type IN ('character varying', 'text')
                ) THEN
                    -- STRICT VALIDATION: find first value that is neither a known
                    -- enum name nor a valid numeric string (0-1)
                    SELECT ""Type""::text INTO unknown_type
                    FROM ""CashFlowTransactions""
                    WHERE ""Type""::text NOT IN ('Inflow', 'Outflow', '0', '1')
                    LIMIT 1;

                    IF unknown_type IS NOT NULL THEN
                        RAISE EXCEPTION 'Unknown CashFlowTransactions.Type value: %', unknown_type;
                    END IF;

                    -- All values validated; safe to convert.
                    -- CASE handles both enum name strings and numeric strings.
                    ALTER TABLE ""CashFlowTransactions""
                    ALTER COLUMN ""Type"" TYPE integer USING CASE ""Type""::text
                        WHEN 'Inflow'  THEN 0
                        WHEN 'Outflow' THEN 1
                        WHEN '0' THEN 0
                        WHEN '1' THEN 1
                    END;
                    RAISE NOTICE 'CashFlowTransactions.Type converted from varchar to integer.';
                ELSE
                    RAISE NOTICE 'CashFlowTransactions.Type is already integer; skipping.';
                END IF;
            END$$;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ====================================================================
        // Revert: Convert Category and Type back from integer to varchar
        // STRICT: Any unknown integer causes RAISE EXCEPTION and rollback FAILS.
        // ====================================================================

        // Revert Category: integer -> varchar(30)
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                unknown_cat_int INTEGER;
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Category'
                      AND data_type = 'integer'
                ) THEN
                    -- STRICT VALIDATION: find first unknown integer
                    SELECT ""Category"" INTO unknown_cat_int
                    FROM ""CashFlowTransactions""
                    WHERE ""Category"" NOT IN (0, 1, 2, 3, 4, 5, 6, 7, 8, 9)
                    LIMIT 1;

                    IF unknown_cat_int IS NOT NULL THEN
                        RAISE EXCEPTION 'Unknown CashFlowTransactions.Category integer value: %', unknown_cat_int;
                    END IF;

                    -- All values validated; safe to convert (no ELSE needed in CASE)
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
                    END;
                END IF;
            END$$;
        ");

        // Revert Type: integer -> varchar(20)
        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                unknown_type_int INTEGER;
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'CashFlowTransactions'
                      AND column_name = 'Type'
                      AND data_type = 'integer'
                ) THEN
                    -- STRICT VALIDATION: find first unknown integer
                    SELECT ""Type"" INTO unknown_type_int
                    FROM ""CashFlowTransactions""
                    WHERE ""Type"" NOT IN (0, 1)
                    LIMIT 1;

                    IF unknown_type_int IS NOT NULL THEN
                        RAISE EXCEPTION 'Unknown CashFlowTransactions.Type integer value: %', unknown_type_int;
                    END IF;

                    -- All values validated; safe to convert (no ELSE needed in CASE)
                    ALTER TABLE ""CashFlowTransactions""
                    ALTER COLUMN ""Type"" TYPE varchar(20) USING CASE ""Type""
                        WHEN 0 THEN 'Inflow'
                        WHEN 1 THEN 'Outflow'
                    END;
                END IF;
            END$$;
        ");
    }
}
