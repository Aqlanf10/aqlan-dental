using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// W07: idempotent doctor disbursements and append-only allocation evidence.
/// Existing payments receive stable legacy keys; no historical allocation is guessed.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260806010000_AddAtomicCommissionAllocations")]
public partial class AddAtomicCommissionAllocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "DoctorCommissionPayments"
                ADD COLUMN "IdempotencyKey" character varying(100);

            UPDATE "DoctorCommissionPayments"
            SET "IdempotencyKey" = 'legacy-' || "Id"::text
            WHERE "IdempotencyKey" IS NULL;

            ALTER TABLE "DoctorCommissionPayments"
                ALTER COLUMN "IdempotencyKey" SET NOT NULL;

            CREATE UNIQUE INDEX "IX_DoctorCommissionPayments_BranchId_IdempotencyKey"
                ON "DoctorCommissionPayments" ("BranchId", "IdempotencyKey");

            CREATE TABLE "DoctorCommissionPaymentAllocations" (
                "Id" uuid NOT NULL,
                "PaymentId" uuid NOT NULL,
                "InvoiceLineItemId" uuid NULL,
                "CommissionAdjustmentId" uuid NULL,
                "Amount" numeric(18,2) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "IsActive" boolean NOT NULL,
                "DeletedAt" timestamp with time zone NULL,
                "DeletedBy" uuid NULL,
                CONSTRAINT "PK_DoctorCommissionPaymentAllocations" PRIMARY KEY ("Id"),
                CONSTRAINT "CK_DoctorCommissionPaymentAllocations_PositiveAmount" CHECK ("Amount" > 0),
                CONSTRAINT "CK_DoctorCommissionPaymentAllocations_ExactlyOneTarget"
                    CHECK (("InvoiceLineItemId" IS NOT NULL) <> ("CommissionAdjustmentId" IS NOT NULL)),
                CONSTRAINT "FK_DoctorCommissionPaymentAllocations_DoctorCommissionPayments_PaymentId"
                    FOREIGN KEY ("PaymentId") REFERENCES "DoctorCommissionPayments" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_DoctorCommissionPaymentAllocations_InvoiceLineItems_InvoiceLineItemId"
                    FOREIGN KEY ("InvoiceLineItemId") REFERENCES "InvoiceLineItems" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_DoctorCommissionPaymentAllocations_DoctorCommissionAdjustments_CommissionAdjustmentId"
                    FOREIGN KEY ("CommissionAdjustmentId") REFERENCES "DoctorCommissionAdjustments" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX "IX_DoctorCommissionPaymentAllocations_InvoiceLineItemId"
                ON "DoctorCommissionPaymentAllocations" ("InvoiceLineItemId");
            CREATE INDEX "IX_DoctorCommissionPaymentAllocations_CommissionAdjustmentId"
                ON "DoctorCommissionPaymentAllocations" ("CommissionAdjustmentId");
            CREATE UNIQUE INDEX "IX_DoctorCommissionPaymentAllocations_PaymentId_InvoiceLineItemId"
                ON "DoctorCommissionPaymentAllocations" ("PaymentId", "InvoiceLineItemId")
                WHERE "InvoiceLineItemId" IS NOT NULL;
            CREATE UNIQUE INDEX "IX_DoctorCommissionPaymentAllocations_PaymentId_CommissionAdjustmentId"
                ON "DoctorCommissionPaymentAllocations" ("PaymentId", "CommissionAdjustmentId")
                WHERE "CommissionAdjustmentId" IS NOT NULL;

            CREATE OR REPLACE FUNCTION enforce_commission_allocation_ceiling()
            RETURNS trigger AS $$
            DECLARE
                target_total numeric(18,2);
                allocated_total numeric(18,2);
                payment_total numeric(18,2);
                payment_doctor uuid;
                payment_branch uuid;
                payment_currency character varying(3);
                target_doctor uuid;
                target_branch uuid;
                target_currency character varying(3);
            BEGIN
                SELECT "Amount", "DoctorId", "BranchId", "Currency"
                INTO payment_total, payment_doctor, payment_branch, payment_currency
                FROM "DoctorCommissionPayments"
                WHERE "Id" = NEW."PaymentId"
                FOR UPDATE;
                IF payment_total IS NULL THEN
                    RAISE EXCEPTION 'commission allocation payment does not exist';
                END IF;

                SELECT COALESCE(SUM("Amount"), 0) INTO allocated_total
                FROM "DoctorCommissionPaymentAllocations"
                WHERE "PaymentId" = NEW."PaymentId"
                  AND "IsActive" = TRUE
                  AND "Id" <> NEW."Id";
                IF allocated_total + NEW."Amount" > payment_total THEN
                    RAISE EXCEPTION 'commission allocation exceeds payment amount';
                END IF;

                IF NEW."InvoiceLineItemId" IS NOT NULL THEN
                    SELECT line."DoctorCommissionAmount", line."DoctorId", patient."BranchId", invoice."Currency"
                    INTO target_total, target_doctor, target_branch, target_currency
                    FROM "InvoiceLineItems" line
                    JOIN "Invoices" invoice ON invoice."Id" = line."InvoiceId"
                    JOIN "Patients" patient ON patient."Id" = invoice."PatientId"
                    WHERE line."Id" = NEW."InvoiceLineItemId"
                      AND line."IsActive" = TRUE
                      AND line."CommissionStatus" IN (2, 3)
                    FOR UPDATE;
                    SELECT COALESCE(SUM("Amount"), 0) INTO allocated_total
                    FROM "DoctorCommissionPaymentAllocations"
                    WHERE "InvoiceLineItemId" = NEW."InvoiceLineItemId"
                      AND "IsActive" = TRUE
                      AND "Id" <> NEW."Id";
                ELSE
                    SELECT GREATEST("AdjustmentAmount", 0), "DoctorId", "BranchId", "Currency"
                    INTO target_total, target_doctor, target_branch, target_currency
                    FROM "DoctorCommissionAdjustments"
                    WHERE "Id" = NEW."CommissionAdjustmentId"
                      AND "IsActive" = TRUE
                      AND "Status" IN ('Pending', 'Settled')
                      AND "AdjustmentAmount" > 0
                    FOR UPDATE;
                    SELECT COALESCE(SUM("Amount"), 0) INTO allocated_total
                    FROM "DoctorCommissionPaymentAllocations"
                    WHERE "CommissionAdjustmentId" = NEW."CommissionAdjustmentId"
                      AND "IsActive" = TRUE
                      AND "Id" <> NEW."Id";
                END IF;

                IF target_total IS NULL OR allocated_total + NEW."Amount" > target_total THEN
                    RAISE EXCEPTION 'commission allocation exceeds target outstanding amount';
                END IF;
                IF payment_doctor IS DISTINCT FROM target_doctor
                   OR payment_branch IS DISTINCT FROM target_branch
                   OR payment_currency IS DISTINCT FROM target_currency THEN
                    RAISE EXCEPTION 'commission allocation target is outside payment doctor/branch/currency scope';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER "TR_DoctorCommissionPaymentAllocations_Ceiling"
            BEFORE INSERT OR UPDATE ON "DoctorCommissionPaymentAllocations"
            FOR EACH ROW EXECUTE FUNCTION enforce_commission_allocation_ceiling();

            CREATE OR REPLACE FUNCTION prevent_commission_allocation_mutation()
            RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'commission payment allocations are append-only';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER "TR_DoctorCommissionPaymentAllocations_Immutable"
            BEFORE UPDATE OR DELETE ON "DoctorCommissionPaymentAllocations"
            FOR EACH ROW EXECUTE FUNCTION prevent_commission_allocation_mutation();

            CREATE OR REPLACE FUNCTION enforce_commission_payment_fully_allocated()
            RETURNS trigger AS $$
            DECLARE
                allocated_total numeric(18,2);
            BEGIN
                SELECT COALESCE(SUM("Amount"), 0) INTO allocated_total
                FROM "DoctorCommissionPaymentAllocations"
                WHERE "PaymentId" = NEW."Id" AND "IsActive" = TRUE;
                IF allocated_total <> NEW."Amount" THEN
                    RAISE EXCEPTION 'commission payment must be fully allocated';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE CONSTRAINT TRIGGER "TR_DoctorCommissionPayments_FullyAllocated"
            AFTER INSERT OR UPDATE OF "Amount" ON "DoctorCommissionPayments"
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION enforce_commission_payment_fully_allocated();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS "TR_DoctorCommissionPaymentAllocations_Ceiling"
                ON "DoctorCommissionPaymentAllocations";
            DROP TRIGGER IF EXISTS "TR_DoctorCommissionPaymentAllocations_Immutable"
                ON "DoctorCommissionPaymentAllocations";
            DROP TRIGGER IF EXISTS "TR_DoctorCommissionPayments_FullyAllocated"
                ON "DoctorCommissionPayments";
            DROP FUNCTION IF EXISTS enforce_commission_allocation_ceiling();
            DROP FUNCTION IF EXISTS prevent_commission_allocation_mutation();
            DROP FUNCTION IF EXISTS enforce_commission_payment_fully_allocated();
            DROP TABLE IF EXISTS "DoctorCommissionPaymentAllocations";
            DROP INDEX IF EXISTS "IX_DoctorCommissionPayments_BranchId_IdempotencyKey";
            ALTER TABLE "DoctorCommissionPayments" DROP COLUMN IF EXISTS "IdempotencyKey";
            """);
    }
}
