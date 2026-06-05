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
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'TreatmentPlans' AND column_name = 'PlanLabel' AND is_nullable = 'YES'
    ) THEN
        ALTER TABLE ""TreatmentPlans"" ALTER COLUMN ""PlanLabel"" SET NOT NULL;
        ALTER TABLE ""TreatmentPlans"" ALTER COLUMN ""PlanLabel"" SET DEFAULT 'A';
        UPDATE ""TreatmentPlans"" SET ""PlanLabel"" = 'A' WHERE ""PlanLabel"" IS NULL;
    END IF;
END $$;
");

        // 2. CreateTable Treasuries
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
    CONSTRAINT ""PK_Treasuries"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Treasuries_Branches_BranchId"" FOREIGN KEY (""BranchId"") REFERENCES ""Branches"" (""Id"") ON DELETE CASCADE
);
");

        // 3. CreateTable VaultTransfers
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
    CONSTRAINT ""PK_VaultTransfers"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_VaultTransfers_Treasuries_SourceTreasuryId"" FOREIGN KEY (""SourceTreasuryId"") REFERENCES ""Treasuries"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_VaultTransfers_Treasuries_DestinationTreasuryId"" FOREIGN KEY (""DestinationTreasuryId"") REFERENCES ""Treasuries"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_VaultTransfers_CashierSessions_CashierSessionId"" FOREIGN KEY (""CashierSessionId"") REFERENCES ""CashierSessions"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_VaultTransfers_Users_PerformedByUserId"" FOREIGN KEY (""PerformedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_VaultTransfers_Users_ApprovedByUserId"" FOREIGN KEY (""ApprovedByUserId"") REFERENCES ""Users"" (""Id"") ON DELETE RESTRICT
);
");

        // 4. Create indexes
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Treasuries_BranchId"" ON ""Treasuries"" (""BranchId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_ApprovedByUserId"" ON ""VaultTransfers"" (""ApprovedByUserId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_CashierSessionId"" ON ""VaultTransfers"" (""CashierSessionId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_DestinationTreasuryId"" ON ""VaultTransfers"" (""DestinationTreasuryId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_VaultTransfers_PerformedByUserId"" ON ""VaultTransfers"" (""PerformedByUserId"");");
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
