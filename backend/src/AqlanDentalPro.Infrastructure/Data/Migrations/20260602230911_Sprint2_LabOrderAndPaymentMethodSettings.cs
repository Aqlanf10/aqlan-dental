using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class Sprint2_LabOrderAndPaymentMethodSettings : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1-6. AddColumn nullable columns on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'BranchId') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""BranchId"" uuid NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'CancellationReason') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""CancellationReason"" text NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'DeliveredDate') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""DeliveredDate"" date NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RestorationType') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""RestorationType"" text NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'Shade') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""Shade"" text NULL;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'VisitId') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""VisitId"" uuid NULL;
    END IF;
END $$;
");

        // 7. CreateTable PaymentMethodSettings
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""PaymentMethodSettings"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(100) NULL,
    ""Code"" character varying(50) NULL,
    ""RequiresReferenceNumber"" boolean NOT NULL,
    ""AccountName"" character varying(200) NULL,
    ""AccountNumber"" character varying(100) NULL,
    ""Notes"" character varying(500) NULL,
    ""SortOrder"" integer NOT NULL,
    ""BranchId"" uuid NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_PaymentMethodSettings"" PRIMARY KEY (""Id"")
);
");

        // 8. Create indexes
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrders_BranchId"" ON ""LabOrders"" (""BranchId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrders_VisitId"" ON ""LabOrders"" (""VisitId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_PaymentMethodSettings_BranchId"" ON ""PaymentMethodSettings"" (""BranchId"");");
        migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PaymentMethodSettings_Code"" ON ""PaymentMethodSettings"" (""Code"");");

        // 9. AddForeignKey FK_LabOrders_Visits_VisitId
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrders_Visits_VisitId') THEN
        ALTER TABLE ""LabOrders"" ADD CONSTRAINT ""FK_LabOrders_Visits_VisitId""
            FOREIGN KEY (""VisitId"") REFERENCES ""Visits"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_LabOrders_Visits_VisitId",
            table: "LabOrders");

        migrationBuilder.DropTable(name: "PaymentMethodSettings");

        migrationBuilder.DropIndex(name: "IX_LabOrders_VisitId", table: "LabOrders");
        migrationBuilder.DropIndex(name: "IX_LabOrders_BranchId", table: "LabOrders");

        migrationBuilder.DropColumn(name: "VisitId", table: "LabOrders");
        migrationBuilder.DropColumn(name: "Shade", table: "LabOrders");
        migrationBuilder.DropColumn(name: "RestorationType", table: "LabOrders");
        migrationBuilder.DropColumn(name: "DeliveredDate", table: "LabOrders");
        migrationBuilder.DropColumn(name: "CancellationReason", table: "LabOrders");
        migrationBuilder.DropColumn(name: "BranchId", table: "LabOrders");
    }
}
