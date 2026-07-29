using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddLabManagementFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. AddColumn LabId (uuid, NULL) on LabOrders
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'LabOrders' AND column_name = 'LabId') THEN
        ALTER TABLE ""LabOrders"" ADD COLUMN ""LabId"" uuid NULL;
    END IF;
END $$;
");

        // 2. CreateTable Labs
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Labs"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(200) NULL,
    ""Phone"" character varying(30) NULL,
    ""WhatsApp"" character varying(30) NULL,
    ""Address"" character varying(500) NULL,
    ""ContactPerson"" character varying(200) NULL,
    ""Email"" character varying(200) NULL,
    ""Notes"" text NULL,
    ""BranchId"" uuid NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_Labs"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Labs_Branches_BranchId"" FOREIGN KEY (""BranchId"") REFERENCES ""Branches"" (""Id"") ON DELETE SET NULL
);
");

        // 3. CreateTable LabWorkTypes
        migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""LabWorkTypes"" (
    ""Id"" uuid NOT NULL,
    ""Name"" character varying(100) NULL,
    ""NameAr"" character varying(100) NULL,
    ""Category"" character varying(50) NULL,
    ""SortOrder"" integer NOT NULL,
    ""CreatedAt"" timestamptz NOT NULL,
    ""UpdatedAt"" timestamptz NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""DeletedAt"" timestamptz NULL,
    ""DeletedBy"" uuid NULL,
    CONSTRAINT ""PK_LabWorkTypes"" PRIMARY KEY (""Id"")
);
");

        // 4. Create indexes
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabOrders_LabId"" ON ""LabOrders"" (""LabId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Labs_BranchId"" ON ""Labs"" (""BranchId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Labs_Name"" ON ""Labs"" (""Name"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabWorkTypes_Name"" ON ""LabWorkTypes"" (""Name"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_LabWorkTypes_SortOrder"" ON ""LabWorkTypes"" (""SortOrder"");");

        // 5. AddForeignKey FK_LabOrders_Labs_LabId
        migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_LabOrders_Labs_LabId') THEN
        ALTER TABLE ""LabOrders"" ADD CONSTRAINT ""FK_LabOrders_Labs_LabId""
            FOREIGN KEY (""LabId"") REFERENCES ""Labs"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_LabOrders_Labs_LabId",
            table: "LabOrders");

        migrationBuilder.DropTable(name: "LabWorkTypes");
        migrationBuilder.DropTable(name: "Labs");

        migrationBuilder.DropIndex(name: "IX_LabOrders_LabId", table: "LabOrders");

        migrationBuilder.DropColumn(name: "LabId", table: "LabOrders");
    }
}
