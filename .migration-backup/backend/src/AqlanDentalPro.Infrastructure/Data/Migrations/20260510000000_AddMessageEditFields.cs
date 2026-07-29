using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds IsEdited and EditedAt columns to Messages table.
/// Converted to idempotent raw SQL with IF NOT EXISTS guards.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260510000000_AddMessageEditFields")]
public partial class AddMessageEditFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages') THEN
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
            ALTER TABLE ""Messages"" ADD COLUMN ""IsEdited"" boolean NOT NULL DEFAULT false;
        END IF;
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
            ALTER TABLE ""Messages"" ADD COLUMN ""EditedAt"" timestamp with time zone NULL;
        END IF;
    END IF;
END $$;
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Messages') THEN
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
            ALTER TABLE ""Messages"" DROP COLUMN ""EditedAt"";
        END IF;
        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
            ALTER TABLE ""Messages"" DROP COLUMN ""IsEdited"";
        END IF;
    END IF;
END $$;
");
    }
}
