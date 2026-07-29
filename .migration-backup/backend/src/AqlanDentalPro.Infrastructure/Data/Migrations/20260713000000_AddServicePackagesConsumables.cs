using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// YOLO-S2: Service catalog packages + consumables.
///
/// Adds:
///   - ClinicServices.Color (nullable hex string for calendar/queue display)
///   - Contracts.PackageId (nullable FK → TreatmentPackages.Id, ON DELETE SET NULL)
///   - TreatmentPackageServices table (PackageId, ClinicServiceId, Quantity=1, OverridePrice?)
///   - ServiceConsumables table (ClinicServiceId, InventoryItemId, Quantity=1, Notes?)
///
/// Rules honored:
///   - All new columns are nullable → no behavior change for existing rows.
///   - Idempotent DDL (ADD COLUMN IF NOT EXISTS / CREATE TABLE IF NOT EXISTS) so the
///     migration is safe to apply on databases where the C-08 startup hotfix already
///     added the columns. Mirrors the existing 20260712000000_AddAppointmentEnhancements
///     pattern (also raw SQL because the EF migration chain is historically broken —
///     see CLAUDE.md "Migration chain" pitfall).
///   - No installment auto-generation. No commission change. Pure catalog binding.
///
/// Down: drops the FKs + tables + columns. Safe to roll back because all new fields
/// are optional and have no production data dependencies yet.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260713000000_AddServicePackagesConsumables")]
public partial class AddServicePackagesConsumables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ClinicServices.Color ─────────────────────────────────────────────
        migrationBuilder.Sql("""
            ALTER TABLE "ClinicServices"
                ADD COLUMN IF NOT EXISTS "Color" character varying(20) NULL;
            """);

        // ── Contracts.PackageId (FK → TreatmentPackages, ON DELETE SET NULL) ─
        migrationBuilder.Sql("""
            ALTER TABLE "Contracts"
                ADD COLUMN IF NOT EXISTS "PackageId" uuid NULL;

            CREATE INDEX IF NOT EXISTS "IX_Contracts_PackageId"
                ON "Contracts" ("PackageId");

            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_Contracts_TreatmentPackages_PackageId'
                ) THEN
                    ALTER TABLE "Contracts"
                        ADD CONSTRAINT "FK_Contracts_TreatmentPackages_PackageId"
                        FOREIGN KEY ("PackageId") REFERENCES "TreatmentPackages"("Id")
                        ON DELETE SET NULL;
                END IF;
            END $$;
            """);

        // ── TreatmentPackageServices table ───────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "TreatmentPackageServices" (
                "Id"              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                "PackageId"       uuid                     NOT NULL,
                "ClinicServiceId" uuid                     NOT NULL,
                "Quantity"        integer                  NOT NULL DEFAULT 1,
                "OverridePrice"   numeric(12,2)            NULL,
                "CreatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                "IsActive"        boolean                  NOT NULL DEFAULT true,
                "DeletedAt"       timestamp with time zone NULL,
                "DeletedBy"       uuid                     NULL,
                CONSTRAINT "PK_TreatmentPackageServices" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_TreatmentPackageServices_TreatmentPackages_PackageId"
                    FOREIGN KEY ("PackageId") REFERENCES "TreatmentPackages"("Id")
                    ON DELETE CASCADE,
                CONSTRAINT "FK_TreatmentPackageServices_ClinicServices_ClinicServiceId"
                    FOREIGN KEY ("ClinicServiceId") REFERENCES "ClinicServices"("Id")
                    ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_TreatmentPackageServices_PackageId_ClinicServiceId"
                ON "TreatmentPackageServices" ("PackageId", "ClinicServiceId")
                WHERE "IsActive" = true;

            CREATE INDEX IF NOT EXISTS "IX_TreatmentPackageServices_PackageId"
                ON "TreatmentPackageServices" ("PackageId");

            CREATE INDEX IF NOT EXISTS "IX_TreatmentPackageServices_ClinicServiceId"
                ON "TreatmentPackageServices" ("ClinicServiceId");
            """);

        // ── ServiceConsumables table ─────────────────────────────────────────
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "ServiceConsumables" (
                "Id"              uuid                     NOT NULL DEFAULT gen_random_uuid(),
                "ClinicServiceId" uuid                     NOT NULL,
                "InventoryItemId" uuid                     NOT NULL,
                "Quantity"        integer                  NOT NULL DEFAULT 1,
                "Notes"           character varying(500)   NULL,
                "CreatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                "UpdatedAt"       timestamp with time zone NOT NULL DEFAULT now(),
                "IsActive"        boolean                  NOT NULL DEFAULT true,
                "DeletedAt"       timestamp with time zone NULL,
                "DeletedBy"       uuid                     NULL,
                CONSTRAINT "PK_ServiceConsumables" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ServiceConsumables_ClinicServices_ClinicServiceId"
                    FOREIGN KEY ("ClinicServiceId") REFERENCES "ClinicServices"("Id")
                    ON DELETE CASCADE,
                CONSTRAINT "FK_ServiceConsumables_Inventory_InventoryItemId"
                    FOREIGN KEY ("InventoryItemId") REFERENCES "Inventory"("Id")
                    ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServiceConsumables_ClinicServiceId_InventoryItemId"
                ON "ServiceConsumables" ("ClinicServiceId", "InventoryItemId")
                WHERE "IsActive" = true;

            CREATE INDEX IF NOT EXISTS "IX_ServiceConsumables_ClinicServiceId"
                ON "ServiceConsumables" ("ClinicServiceId");

            CREATE INDEX IF NOT EXISTS "IX_ServiceConsumables_InventoryItemId"
                ON "ServiceConsumables" ("InventoryItemId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "ServiceConsumables";
            DROP TABLE IF EXISTS "TreatmentPackageServices";

            DO $$ BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_Contracts_TreatmentPackages_PackageId'
                ) THEN
                    ALTER TABLE "Contracts"
                        DROP CONSTRAINT "FK_Contracts_TreatmentPackages_PackageId";
                END IF;
            END $$;

            DROP INDEX IF EXISTS "IX_Contracts_PackageId";

            ALTER TABLE "Contracts"
                DROP COLUMN IF EXISTS "PackageId";

            ALTER TABLE "ClinicServices"
                DROP COLUMN IF EXISTS "Color";
            """);
    }
}
