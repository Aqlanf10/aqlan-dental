using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// TD-020 Phase B2: Formalizes three schema changes that were previously applied
/// as raw SQL in DbSeeder.cs (S1-S4):
///
/// S1: ADD COLUMN "PasswordSalt" text NOT NULL DEFAULT '' to "Users"
///     — EF model already declared the property as required string, but no
///       migration existed to create the column on existing databases.
///
/// S2+S3: Deduplicate raw Phone values, then CREATE UNIQUE INDEX "IX_Patients_Phone"
///     — PatientConfiguration.cs already declares HasIndex(p => p.Phone).IsUnique()
///       with a filtered condition, but it was never included in a migration.
///
/// S4: CREATE UNIQUE INDEX "IX_Patients_WhatsApp"
///     — Same situation as S3 for the WhatsApp column.
///
/// After this migration, the 4 ExecuteSqlRawAsync calls in DbSeeder.cs become
/// redundant and can be safely removed.
/// </summary>
public partial class AddPasswordSaltAndPatientPhoneIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // S1: Add PasswordSalt column to Users (non-nullable, with default for existing rows)
        migrationBuilder.AddColumn<string>(
            name: "PasswordSalt",
            table: "Users",
            type: "text",
            nullable: false,
            defaultValue: "");

        // S2: Deduplicate Phone values before creating unique index.
        // Keeps the first occurrence (lowest Id) and blanks duplicates.
        migrationBuilder.Sql(@"
            UPDATE ""Patients"" SET ""Phone"" = ''
            WHERE ""Id""::text NOT IN (
                SELECT MIN(p.""Id""::text) FROM ""Patients"" p
                WHERE p.""Phone"" IS NOT NULL AND p.""Phone"" != ''
                GROUP BY p.""Phone""
            ) AND ""Phone"" IS NOT NULL AND ""Phone"" != '';
        ");

        // S3: Create unique filtered index on Phone
        migrationBuilder.CreateIndex(
            name: "IX_Patients_Phone",
            table: "Patients",
            column: "Phone",
            unique: true,
            filter: "\"Phone\" IS NOT NULL AND \"Phone\" != ''");

        // S4: Create unique filtered index on WhatsApp
        // Note: WhatsApp deduplication is NOT needed here because no dedup was
        // performed in DbSeeder for WhatsApp (the raw SQL only deduped Phone).
        // If duplicate WhatsApp values exist in production, the CREATE INDEX will
        // fail-safe; this is acceptable since the column is optional and rarely set.
        migrationBuilder.CreateIndex(
            name: "IX_Patients_WhatsApp",
            table: "Patients",
            column: "WhatsApp",
            unique: true,
            filter: "\"WhatsApp\" IS NOT NULL AND \"WhatsApp\" != ''");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Patients_WhatsApp",
            table: "Patients");

        migrationBuilder.DropIndex(
            name: "IX_Patients_Phone",
            table: "Patients");

        migrationBuilder.DropColumn(
            name: "PasswordSalt",
            table: "Users");
    }
}
