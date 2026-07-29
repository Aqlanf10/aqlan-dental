using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// TD-020 Phase B2: Formalizes schema changes that were previously applied
/// as raw SQL in DbSeeder.cs (S1-S4).
///
/// This migration is idempotent: safe to run on databases where the old
/// DbSeeder raw SQL already applied these changes (PasswordSalt column,
/// Phone/WhatsApp unique indexes). All operations use IF NOT EXISTS guards.
///
/// After this migration, the 4 ExecuteSqlRawAsync calls in DbSeeder.cs are
/// redundant and have been removed.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260521000000_AddPasswordSaltAndPatientPhoneIndexes")]
public partial class AddPasswordSaltAndPatientPhoneIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // S1: Add PasswordSalt column to Users (idempotent via DO block)
        // The old DbSeeder ran ALTER TABLE ... ADD COLUMN IF NOT EXISTS on every
        // maintenance startup. This migration reproduces that behavior safely.
        migrationBuilder.Sql(@"
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_name = 'Users'
                    AND column_name = 'PasswordSalt'
                ) THEN
                    ALTER TABLE ""Users"" ADD COLUMN ""PasswordSalt"" text NOT NULL DEFAULT '';
                END IF;
            END $$;
        ");

        // S2: Deduplicate Phone values before creating unique index.
        // Keeps the first occurrence (lowest Id) and blanks duplicates.
        // Safe to re-run: if no duplicates exist, UPDATE affects 0 rows.
        migrationBuilder.Sql(@"
            UPDATE ""Patients"" SET ""Phone"" = ''
            WHERE ""Id""::text NOT IN (
                SELECT MIN(p.""Id""::text) FROM ""Patients"" p
                WHERE p.""Phone"" IS NOT NULL AND p.""Phone"" != ''
                GROUP BY p.""Phone""
            ) AND ""Phone"" IS NOT NULL AND ""Phone"" != '';
        ");

        // S3: Create unique filtered index on Phone (idempotent via IF NOT EXISTS)
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Patients_Phone""
            ON ""Patients"" (""Phone"")
            WHERE ""Phone"" IS NOT NULL AND ""Phone"" != '';
        ");

        // S4a: Deduplicate WhatsApp values before creating unique index.
        // The old DbSeeder did NOT deduplicate WhatsApp, but we add it now
        // for safety — prevents CREATE INDEX failure if duplicates exist.
        migrationBuilder.Sql(@"
            UPDATE ""Patients"" SET ""WhatsApp"" = ''
            WHERE ""Id""::text NOT IN (
                SELECT MIN(p.""Id""::text) FROM ""Patients"" p
                WHERE p.""WhatsApp"" IS NOT NULL AND p.""WhatsApp"" != ''
                GROUP BY p.""WhatsApp""
            ) AND ""WhatsApp"" IS NOT NULL AND ""WhatsApp"" != '';
        ");

        // S4b: Create unique filtered index on WhatsApp (idempotent via IF NOT EXISTS)
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Patients_WhatsApp""
            ON ""Patients"" (""WhatsApp"")
            WHERE ""WhatsApp"" IS NOT NULL AND ""WhatsApp"" != '';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // DROP INDEX IF EXISTS is safe even if the index was already dropped
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Patients_WhatsApp"";");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Patients_Phone"";");
        migrationBuilder.Sql(@"ALTER TABLE ""Users"" DROP COLUMN IF EXISTS ""PasswordSalt"";");
    }
}
