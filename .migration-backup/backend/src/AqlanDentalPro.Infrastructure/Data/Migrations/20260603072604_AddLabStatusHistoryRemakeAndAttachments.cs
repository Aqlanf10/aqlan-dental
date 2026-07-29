using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLabStatusHistoryRemakeAndAttachments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. AddColumn IsFreeRemake (boolean, NOT NULL, default false) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'IsFreeRemake') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""IsFreeRemake"" boolean NOT NULL DEFAULT false;
    END IF;
END $$;
");

        // 2. AddColumn OriginalOrderId (uuid, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'OriginalOrderId') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""OriginalOrderId"" uuid NULL;
    END IF;
END $$;
");

        // 3. AddColumn RemakeCost (numeric, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RemakeCost') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""RemakeCost"" numeric NULL;
    END IF;
END $$;
");

        // 4. AddColumn RemakeCount (integer, NOT NULL, default 0) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RemakeCount') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""RemakeCount"" integer NOT NULL DEFAULT 0;
    END IF;
END $$;
");

        // 5. AddColumn RemakeReason (text, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'RemakeReason') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""RemakeReason"" text NULL;
    END IF;
END $$;
");

        // 6. AddColumn ReturnReason (text, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'ReturnReason') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""ReturnReason"" text NULL;
    END IF;
END $$;
");

        // 7. CreateTable LabOrderAttachments (no inline FKs — added separately below)
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LabOrderAttachments"" (
    ""Id"" uuid NOT NULL,
    ""LabOrderId"" uuid NULL,
    ""LabOrderItemId"" uuid NULL,
    ""FileName"" character varying(500) NULL,
    ""ContentType"" character varying(100) NULL,
    ""FileSize"" bigint NOT NULL,
    ""Category"" character varying(50) NULL,
    ""StoragePath"" character varying(1000) NULL,
    ""UploadedBy"" uuid NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_LabOrderAttachments"" PRIMARY KEY (""Id"")
);
");

        // 8. CreateTable LabOrderStatusHistories (no inline FKs — added separately below)
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LabOrderStatusHistories"" (
    ""Id"" uuid NOT NULL,
    ""LabOrderId"" uuid NULL,
    ""FromStatus"" character varying(50) NULL,
    ""ToStatus"" character varying(50) NULL,
    ""ChangedByUserId"" uuid NULL,
    ""Reason"" character varying(1000) NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_LabOrderStatusHistories"" PRIMARY KEY (""Id"")
);
");

        // 9. Create indexes (all IF NOT EXISTS, with column existence guards where needed)
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrders_OriginalOrderId"" ON ""LabOrders"" (""OriginalOrderId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrderAttachments_LabOrderId"" ON ""LabOrderAttachments"" (""LabOrderId"");");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrderAttachments' AND column_name = 'LabOrderItemId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_LabOrderAttachments_LabOrderItemId"" ON ""LabOrderAttachments"" (""LabOrderItemId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrderAttachments' AND column_name = 'UploadedBy') THEN
        CREATE INDEX IF NOT EXISTS ""IX_LabOrderAttachments_UploadedBy"" ON ""LabOrderAttachments"" (""UploadedBy"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrderStatusHistories' AND column_name = 'ChangedByUserId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_LabOrderStatusHistories_ChangedByUserId"" ON ""LabOrderStatusHistories"" (""ChangedByUserId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrderStatusHistories_LabOrderId"" ON ""LabOrderStatusHistories"" (""LabOrderId"");");

        // 10. AddForeignKey — LabOrderAttachments (idempotent)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderAttachments_LabOrders_LabOrderId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
            ALTER TABLE ""LabOrderAttachments"" ADD CONSTRAINT ""FK_LabOrderAttachments_LabOrders_LabOrderId""
                FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderAttachments_LabOrderItems_LabOrderItemId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrderItems') THEN
            ALTER TABLE ""LabOrderAttachments"" ADD CONSTRAINT ""FK_LabOrderAttachments_LabOrderItems_LabOrderItemId""
                FOREIGN KEY (""LabOrderItemId"") REFERENCES ""LabOrderItems""(""Id"") ON DELETE SET NULL;
        END IF;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderAttachments_Users_UploadedBy') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""LabOrderAttachments"" ADD CONSTRAINT ""FK_LabOrderAttachments_Users_UploadedBy""
                FOREIGN KEY (""UploadedBy"") REFERENCES ""Users""(""Id"") ON DELETE SET NULL;
        END IF;
    END IF;
END $$;
");

        // 11. AddForeignKey — LabOrderStatusHistories (idempotent)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderStatusHistories_LabOrders_LabOrderId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'LabOrders') THEN
            ALTER TABLE ""LabOrderStatusHistories"" ADD CONSTRAINT ""FK_LabOrderStatusHistories_LabOrders_LabOrderId""
                FOREIGN KEY (""LabOrderId"") REFERENCES ""LabOrders""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrderStatusHistories_Users_ChangedByUserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""LabOrderStatusHistories"" ADD CONSTRAINT ""FK_LabOrderStatusHistories_Users_ChangedByUserId""
                FOREIGN KEY (""ChangedByUserId"") REFERENCES ""Users""(""Id"") ON DELETE SET NULL;
        END IF;
    END IF;
END $$;
");

        // 12. AddForeignKey FK_LabOrders_LabOrders_OriginalOrderId (idempotent)
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrders_LabOrders_OriginalOrderId') THEN
        ALTER TABLE ""LabOrders"" ADD CONSTRAINT ""FK_LabOrders_LabOrders_OriginalOrderId""
            FOREIGN KEY (""OriginalOrderId"") REFERENCES ""LabOrders"" (""Id"") ON DELETE RESTRICT;
    END IF;
END $$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_LabOrders_LabOrders_OriginalOrderId",
            table: "LabOrders");

        migrationBuilder.DropTable(name: "LabOrderStatusHistories");
        migrationBuilder.DropTable(name: "LabOrderAttachments");

        migrationBuilder.DropIndex(name: "IX_LabOrders_OriginalOrderId", table: "LabOrders");

        migrationBuilder.DropColumn(name: "ReturnReason", table: "LabOrders");
        migrationBuilder.DropColumn(name: "RemakeReason", table: "LabOrders");
        migrationBuilder.DropColumn(name: "RemakeCount", table: "LabOrders");
        migrationBuilder.DropColumn(name: "RemakeCost", table: "LabOrders");
        migrationBuilder.DropColumn(name: "OriginalOrderId", table: "LabOrders");
        migrationBuilder.DropColumn(name: "IsFreeRemake", table: "LabOrders");
    }
}
