using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// W06: gives every commission payment and correction an explicit branch and
/// currency. Existing rows are backfilled only from traceable finance/invoice
/// records; ambiguous history stops the migration instead of being guessed.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260802020000_AddCommissionCurrencyScope")]
public partial class AddCommissionCurrencyScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DoctorCommissionPayments"
                ADD COLUMN "BranchId" uuid NULL,
                ADD COLUMN "Currency" character varying(3) NULL;

            ALTER TABLE "DoctorCommissionAdjustments"
                ADD COLUMN "BranchId" uuid NULL,
                ADD COLUMN "Currency" character varying(3) NULL;

            UPDATE "DoctorCommissionPayments" payment
            SET
                "BranchId" = COALESCE(
                    (
                        SELECT flow."BranchId"
                        FROM "CashFlowTransactions" flow
                        WHERE flow."ReferenceId" = payment."Id"
                          AND flow."IsActive" = TRUE
                        ORDER BY flow."CreatedAt" DESC
                        LIMIT 1
                    ),
                    (
                        SELECT doctor."BranchId"
                        FROM "Doctors" doctor
                        WHERE doctor."Id" = payment."DoctorId"
                    ),
                    (
                        SELECT app_user."BranchId"
                        FROM "Doctors" doctor
                        JOIN "Users" app_user ON app_user."Id" = doctor."UserId"
                        WHERE doctor."Id" = payment."DoctorId"
                    ),
                    (
                        SELECT MIN(patient."BranchId")
                        FROM "InvoiceLineItems" line
                        JOIN "Invoices" invoice ON invoice."Id" = line."InvoiceId"
                        JOIN "Patients" patient ON patient."Id" = invoice."PatientId"
                        WHERE line."DoctorId" = payment."DoctorId"
                        HAVING COUNT(DISTINCT patient."BranchId") = 1
                    )
                ),
                "Currency" = COALESCE(
                    (
                        SELECT UPPER(treasury."Currency")
                        FROM "CashFlowTransactions" flow
                        JOIN "Treasuries" treasury ON treasury."Id" = flow."TreasuryId"
                        WHERE flow."ReferenceId" = payment."Id"
                          AND flow."IsActive" = TRUE
                        ORDER BY flow."CreatedAt" DESC
                        LIMIT 1
                    ),
                    (
                        SELECT MIN(UPPER(invoice."Currency"))
                        FROM "InvoiceLineItems" line
                        JOIN "Invoices" invoice ON invoice."Id" = line."InvoiceId"
                        WHERE line."DoctorId" = payment."DoctorId"
                        HAVING COUNT(DISTINCT UPPER(invoice."Currency")) = 1
                    )
                );

            UPDATE "DoctorCommissionAdjustments" adjustment
            SET
                "BranchId" = patient."BranchId",
                "Currency" = UPPER(invoice."Currency")
            FROM "Invoices" invoice
            JOIN "Patients" patient ON patient."Id" = invoice."PatientId"
            WHERE invoice."Id" = adjustment."InvoiceId";

            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM "DoctorCommissionPayments"
                    WHERE "BranchId" IS NULL
                       OR "BranchId" = '00000000-0000-0000-0000-000000000000'::uuid
                       OR "Currency" IS NULL
                       OR "Currency" NOT IN ('YER', 'SAR', 'USD')
                ) THEN
                    RAISE EXCEPTION 'W06 preflight failed: commission payment branch/currency cannot be derived safely';
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM "DoctorCommissionAdjustments"
                    WHERE "BranchId" IS NULL
                       OR "BranchId" = '00000000-0000-0000-0000-000000000000'::uuid
                       OR "Currency" IS NULL
                       OR "Currency" NOT IN ('YER', 'SAR', 'USD')
                ) THEN
                    RAISE EXCEPTION 'W06 preflight failed: commission adjustment branch/currency cannot be derived safely';
                END IF;
            END $$;

            ALTER TABLE "DoctorCommissionPayments"
                ALTER COLUMN "BranchId" SET NOT NULL,
                ALTER COLUMN "Currency" SET NOT NULL;

            ALTER TABLE "DoctorCommissionAdjustments"
                ALTER COLUMN "BranchId" SET NOT NULL,
                ALTER COLUMN "Currency" SET NOT NULL;

            ALTER TABLE "DoctorCommissionPayments"
                ADD CONSTRAINT "CK_DoctorCommissionPayments_Currency"
                CHECK ("Currency" IN ('YER', 'SAR', 'USD')),
                ADD CONSTRAINT "FK_DoctorCommissionPayments_Branches_BranchId"
                FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT;

            ALTER TABLE "DoctorCommissionAdjustments"
                ADD CONSTRAINT "CK_DoctorCommissionAdjustments_Currency"
                CHECK ("Currency" IN ('YER', 'SAR', 'USD')),
                ADD CONSTRAINT "FK_DoctorCommissionAdjustments_Branches_BranchId"
                FOREIGN KEY ("BranchId") REFERENCES "Branches" ("Id") ON DELETE RESTRICT;

            CREATE INDEX "IX_DoctorCommissionPayments_DoctorId_BranchId_Currency_PaymentDate"
                ON "DoctorCommissionPayments" ("DoctorId", "BranchId", "Currency", "PaymentDate");

            DROP INDEX "IX_DoctorCommissionPayments_DoctorId";

            CREATE INDEX "IX_DoctorCommissionAdjustments_DoctorId_BranchId_Currency_Status"
                ON "DoctorCommissionAdjustments" ("DoctorId", "BranchId", "Currency", "Status");

            CREATE INDEX "IX_DoctorCommissionAdjustments_BranchId"
                ON "DoctorCommissionAdjustments" ("BranchId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS "IX_DoctorCommissionAdjustments_DoctorId_BranchId_Currency_Status";
            DROP INDEX IF EXISTS "IX_DoctorCommissionAdjustments_BranchId";
            DROP INDEX IF EXISTS "IX_DoctorCommissionPayments_DoctorId_BranchId_Currency_PaymentDate";
            CREATE INDEX "IX_DoctorCommissionPayments_DoctorId"
                ON "DoctorCommissionPayments" ("DoctorId");
            ALTER TABLE "DoctorCommissionAdjustments"
                DROP CONSTRAINT IF EXISTS "FK_DoctorCommissionAdjustments_Branches_BranchId",
                DROP CONSTRAINT IF EXISTS "CK_DoctorCommissionAdjustments_Currency",
                DROP COLUMN IF EXISTS "BranchId",
                DROP COLUMN IF EXISTS "Currency";
            ALTER TABLE "DoctorCommissionPayments"
                DROP CONSTRAINT IF EXISTS "FK_DoctorCommissionPayments_Branches_BranchId",
                DROP CONSTRAINT IF EXISTS "CK_DoctorCommissionPayments_Currency",
                DROP COLUMN IF EXISTS "BranchId",
                DROP COLUMN IF EXISTS "Currency";
            """);
    }
}
