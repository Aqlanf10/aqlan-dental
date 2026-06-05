using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLabOrderItemsAndPricing : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. DropForeignKey FK_InvoiceLineItems_LabOrders_LabOrderId (conditional)
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId') THEN
        ALTER TABLE ""InvoiceLineItems"" DROP CONSTRAINT ""FK_InvoiceLineItems_LabOrders_LabOrderId"";
    END IF;
END $$;
");

        // 2. DropIndex IX_InvoiceLineItems_LabOrderId (conditional)
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_InvoiceLineItems_LabOrderId') THEN
        DROP INDEX ""IX_InvoiceLineItems_LabOrderId"";
    END IF;
END $$;
");

        // 3. AddColumn InvoiceLineItemId (uuid, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'InvoiceLineItemId') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""InvoiceLineItemId"" uuid NULL;
    END IF;
END $$;
");

        // 4. AddColumn TotalCost (numeric, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'TotalCost') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""TotalCost"" numeric NULL;
    END IF;
END $$;
");

        // 5. AddColumn LabOrderId1 (uuid, NULL) on InvoiceLineItems
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'InvoiceLineItems' AND column_name = 'LabOrderId1') THEN
        ALTER TABLE ""InvoiceLineItems"" ADD COLUMN ""LabOrderId1"" uuid NULL;
    END IF;
END $$;
");

        // 6. CreateTable LabOrderItems
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LabOrderItems"" (
    ""Id"" uuid NOT NULL,
    ""LabOrderId"" uuid NULL,
    ""WorkTypeId"" uuid NULL,
    ""ToothNumber"" character varying(50) NULL,
    ""Arch"" character varying(10) NULL,
    ""Shade"" character varying(50) NULL,
    ""RestorationType"" character varying(100) NULL,
    ""UnitsCount"" integer NOT NULL,
    ""UnitPrice"" numeric NOT NULL,
    ""TotalPrice"" numeric NOT NULL,
    ""Instructions"" character varying(2000) NULL,
    ""SortOrder"" integer NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_LabOrderItems"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_LabOrderItems_LabOrders_LabOrderId"" FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_LabOrderItems_LabWorkTypes_WorkTypeId"" FOREIGN KEY (""WorkTypeId"") REFERENCES ""LabWorkTypes"" (""Id"") ON DELETE RESTRICT
);
");

        // 7. CreateTable LabWorkPrices
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LabWorkPrices"" (
    ""Id"" uuid NOT NULL,
    ""LabId"" uuid NULL,
    ""WorkTypeId"" uuid NULL,
    ""UnitPrice"" numeric NOT NULL,
    ""UrgentSurcharge"" numeric NULL,
    ""UrgentSurchargeType"" character varying(20) NULL,
    ""EstimatedDays"" integer NULL,
    ""Notes"" character varying(1000) NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_LabWorkPrices"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_LabWorkPrices_Labs_LabId"" FOREIGN KEY (""LabId"") REFERENCES ""Labs"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_LabWorkPrices_LabWorkTypes_WorkTypeId"" FOREIGN KEY (""WorkTypeId"") REFERENCES ""LabWorkTypes"" (""Id"") ON DELETE CASCADE
);
");

        // 8. Create indexes
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_InvoiceLineItems_LabOrderId1"" ON ""InvoiceLineItems"" (""LabOrderId1"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrderItems_LabOrderId"" ON ""LabOrderItems"" (""LabOrderId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrderItems_WorkTypeId"" ON ""LabOrderItems"" (""WorkTypeId"");");
        migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_LabWorkPrices_LabId_WorkTypeId"" ON ""LabWorkPrices"" (""LabId"", ""WorkTypeId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabWorkPrices_WorkTypeId"" ON ""LabWorkPrices"" (""WorkTypeId"");");

        // 9. AddForeignKey FK_InvoiceLineItems_LabOrders_LabOrderId1
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_InvoiceLineItems_LabOrders_LabOrderId1') THEN
        ALTER TABLE ""InvoiceLineItems"" ADD CONSTRAINT ""FK_InvoiceLineItems_LabOrders_LabOrderId1""
            FOREIGN KEY (""LabOrderId1"") REFERENCES ""LabOrders"" (""Id"") ON DELETE RESTRICT;
    END IF;
END $$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_InvoiceLineItems_LabOrders_LabOrderId1",
            table: "InvoiceLineItems");

        migrationBuilder.DropTable(name: "LabWorkPrices");
        migrationBuilder.DropTable(name: "LabOrderItems");

        migrationBuilder.DropIndex(name: "IX_LabWorkPrices_WorkTypeId", table: "LabWorkPrices");
        migrationBuilder.DropIndex(name: "IX_LabOrderItems_WorkTypeId", table: "LabOrderItems");
        migrationBuilder.DropIndex(name: "IX_LabOrderItems_LabOrderId", table: "LabOrderItems");
        migrationBuilder.DropIndex(name: "IX_InvoiceLineItems_LabOrderId1", table: "InvoiceLineItems");

        migrationBuilder.DropColumn(name: "LabOrderId1", table: "InvoiceLineItems");
        migrationBuilder.DropColumn(name: "TotalCost", table: "LabOrders");
        migrationBuilder.DropColumn(name: "InvoiceLineItemId", table: "LabOrders");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_LabOrderId",
            table: "InvoiceLineItems",
            column: "LabOrderId");

        migrationBuilder.AddForeignKey(
            name: "FK_InvoiceLineItems_LabOrders_LabOrderId",
            table: "InvoiceLineItems",
            column: "LabOrderId",
            principalTable: "LabOrders",
            principalColumn: "Id");
    }
}
