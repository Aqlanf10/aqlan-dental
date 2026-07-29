using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddTreasuryVaultTransfers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. AlterColumn PlanLabel on TreatmentPlans: only alter if column is currently nullable
        //    FIX: Reorder — UPDATE NULLs first, then SET DEFAULT, then SET NOT NULL
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'TreatmentPlans' AND column_name = 'PlanLabel' AND is_nullable = 'YES'
    ) THEN
        UPDATE ""TreatmentPlans"" SET ""PlanLabel"" = 'A' WHERE ""PlanLabel"" IS NULL;
        ALTER TABLE ""TreatmentPlans"" ALTER COLUMN ""PlanLabel"" SET DEFAULT 'A';
        ALTER TABLE ""TreatmentPlans"" ALTER COLUMN ""PlanLabel"" SET NOT NULL;
    END IF;
END $$;
");

        // 2. CreateTable Treasuries (no FK inline — added separately below)
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Treasuries"" (
    ""Id"" uuid NOT NULL,
    ""Name"" text NULL,
    ""Type"" integer NOT NULL,
    ""Balance"" numeric NOT NULL,
    ""BranchId"" uuid NULL,
    ""IsActive"" boolean NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_Treasuries"" PRIMARY KEY (""Id"")
);
");

        // 3. CreateTable VaultTransfers (no FK inline — added separately below)
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""VaultTransfers"" (
    ""Id"" uuid NOT NULL,
    ""TransferNumber"" text NULL,
    ""SourceTreasuryId"" uuid NULL,
    ""DestinationTreasuryId"" uuid NULL,
    ""CashierSessionId"" uuid NULL,
    ""Amount"" numeric NOT NULL,
    ""TransferDate"" timestamptz NOT NULL,
    ""PerformedBy"" uuid NULL,
    ""PerformedByUserId"" uuid NULL,
    ""ApprovedBy"" uuid NULL,
    ""ApprovedByUserId"" uuid NULL,
    ""ApprovalDate"" timestamptz NULL,
    ""Status"" integer NOT NULL,
    ""Notes"" text NULL,
    ""IsActive"" boolean NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_VaultTransfers"" PRIMARY KEY (""Id"")
);
");

        // 4. Add missing columns to VaultTransfers (if table already existed from StartupDB
        //    and is missing PerformedByUserId / ApprovedByUserId)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'PerformedByUserId') THEN
        ALTER TABLE ""VaultTransfers"" ADD COLUMN ""PerformedByUserId"" uuid NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'ApprovedByUserId') THEN
        ALTER TABLE ""VaultTransfers"" ADD COLUMN ""ApprovedByUserId"" uuid NULL;
    END IF;
END $$;
");

        // 5. Add FK constraints (idempotent — IF NOT EXISTS per constraint)
        // Treasuries -> Branches
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Treasuries_Branches_BranchId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Branches') THEN
            ALTER TABLE ""Treasuries"" ADD CONSTRAINT ""FK_Treasuries_Branches_BranchId""
                FOREIGN KEY (""BranchId"") REFERENCES ""Branches""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        // VaultTransfers -> Treasuries (Source)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_VaultTransfers_Treasuries_SourceTreasuryId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Treasuries') THEN
            ALTER TABLE ""VaultTransfers"" ADD CONSTRAINT ""FK_VaultTransfers_Treasuries_SourceTreasuryId""
                FOREIGN KEY (""SourceTreasuryId"") REFERENCES ""Treasuries""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
");

        // VaultTransfers -> Treasuries (Destination)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_VaultTransfers_Treasuries_DestinationTreasuryId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Treasuries') THEN
            ALTER TABLE ""VaultTransfers"" ADD CONSTRAINT ""FK_VaultTransfers_Treasuries_DestinationTreasuryId""
                FOREIGN KEY (""DestinationTreasuryId"") REFERENCES ""Treasuries""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        // VaultTransfers -> CashierSessions
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_VaultTransfers_CashierSessions_CashierSessionId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'CashierSessionId')
           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'CashierSessions') THEN
            ALTER TABLE ""VaultTransfers"" ADD CONSTRAINT ""FK_VaultTransfers_CashierSessions_CashierSessionId""
                FOREIGN KEY (""CashierSessionId"") REFERENCES ""CashierSessions""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
");

        // VaultTransfers -> Users (PerformedByUserId)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_VaultTransfers_Users_PerformedByUserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'PerformedByUserId')
           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""VaultTransfers"" ADD CONSTRAINT ""FK_VaultTransfers_Users_PerformedByUserId""
                FOREIGN KEY (""PerformedByUserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE;
        END IF;
    END IF;
END $$;
");

        // VaultTransfers -> Users (ApprovedByUserId)
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_VaultTransfers_Users_ApprovedByUserId') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'ApprovedByUserId')
           AND EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Users') THEN
            ALTER TABLE ""VaultTransfers"" ADD CONSTRAINT ""FK_VaultTransfers_Users_ApprovedByUserId""
                FOREIGN KEY (""ApprovedByUserId"") REFERENCES ""Users""(""Id"") ON DELETE RESTRICT;
        END IF;
    END IF;
END $$;
");

        // 6. Create indexes (IF NOT EXISTS — safe even if columns don't exist, but we
        //    added missing columns above so they will exist now)
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Treasuries_BranchId"" ON ""Treasuries"" (""BranchId"");");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'ApprovedByUserId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_ApprovedByUserId"" ON ""VaultTransfers"" (""ApprovedByUserId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'CashierSessionId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_CashierSessionId"" ON ""VaultTransfers"" (""CashierSessionId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_DestinationTreasuryId"" ON ""VaultTransfers"" (""DestinationTreasuryId"");");
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'VaultTransfers' AND column_name = 'PerformedByUserId') THEN
        CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_PerformedByUserId"" ON ""VaultTransfers"" (""PerformedByUserId"");
    END IF;
END $$;
");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_SourceTreasuryId"" ON ""VaultTransfers"" (""SourceTreasuryId"");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VaultTransfers");
        migrationBuilder.DropTable(name: "Treasuries");

        migrationBuilder.AlterColumn<string>(
            name: "PlanLabel",
            table: "TreatmentPlans",
            type: "character varying(5)",
            nullable: true,
            oldType: "character varying(5)",
            oldDefaultValue: "A");
    }
}
