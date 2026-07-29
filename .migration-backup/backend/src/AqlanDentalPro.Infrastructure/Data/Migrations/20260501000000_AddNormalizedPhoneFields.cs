using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// إضافة حقول NormalizedPhone و NormalizedWhatsApp مع unique indexes
/// وتعبئة البيانات الموجودة بالقيم الموحدة
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260501000000_AddNormalizedPhoneFields")]
public partial class AddNormalizedPhoneFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // FIX (Replit migration recovery, 2026-07-29): the earlier migration
        // AddPhoneNormalizationAndArchive already adds "NormalizedPhone" (idempotently) to
        // "Patients", so the original non-idempotent AddColumn calls here always failed with
        // "column already exists" on a fresh database. Made idempotent via raw SQL guards,
        // matching this codebase's own established pattern; no columns, types, or later logic
        // in this migration were changed.
        migrationBuilder.Sql(@"
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'Patients' AND column_name = 'NormalizedPhone') THEN
                    ALTER TABLE ""Patients"" ADD COLUMN ""NormalizedPhone"" character varying(20) NULL;
                END IF;
            END $$;
        ");

        migrationBuilder.Sql(@"
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_name = 'Patients' AND column_name = 'NormalizedWhatsApp') THEN
                    ALTER TABLE ""Patients"" ADD COLUMN ""NormalizedWhatsApp"" character varying(20) NULL;
                END IF;
            END $$;
        ");

        // Backfill existing data: normalize existing phone numbers
        // Yemen numbers: strip spaces/dashes, remove leading +/00, add 967 prefix
        migrationBuilder.Sql(@"
            UPDATE ""Patients"" SET ""NormalizedPhone"" = 
                CASE 
                    WHEN ""Phone"" IS NULL OR ""Phone"" = '' THEN NULL
                    ELSE 
                        LTRIM(RTRIM(
                            CASE 
                                WHEN REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                                    '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                                WHEN REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                                    '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                                WHEN REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                                    '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                                WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '')) = 9 AND REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                                    '967' || REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '')
                                ELSE REPLACE(REPLACE(REPLACE(REPLACE(""Phone"", ' ', ''), '-', ''), '(', ''), ')', '')
                            END
                        ))
                END
        ");

        migrationBuilder.Sql(@"
            UPDATE ""Patients"" SET ""NormalizedWhatsApp"" = 
                CASE 
                    WHEN ""WhatsApp"" IS NULL OR ""WhatsApp"" = '' THEN NULL
                    ELSE 
                        LTRIM(RTRIM(
                            CASE 
                                WHEN REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '+%' THEN
                                    '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                                WHEN REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '00%' THEN
                                    '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', ''), 5)
                                WHEN REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '0%' THEN
                                    '967' || SUBSTRING(REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', ''), 2)
                                WHEN LENGTH(REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '')) = 9 AND REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '') LIKE '7%' THEN
                                    '967' || REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '')
                                ELSE REPLACE(REPLACE(REPLACE(REPLACE(""WhatsApp"", ' ', ''), '-', ''), '(', ''), ')', '')
                            END
                        ))
                END
        ");

        // Handle duplicates: if two patients have the same NormalizedPhone, set NULL for the newer ones
        // This ensures the unique index can be created
        migrationBuilder.Sql(@"
            WITH duplicates AS (
                SELECT ""Id"", ""NormalizedPhone"", 
                       ROW_NUMBER() OVER (PARTITION BY ""NormalizedPhone"" ORDER BY ""CreatedAt"" ASC) as rn
                FROM ""Patients"" 
                WHERE ""NormalizedPhone"" IS NOT NULL AND ""NormalizedPhone"" != ''
            )
            UPDATE ""Patients"" SET ""NormalizedPhone"" = NULL
            FROM duplicates
            WHERE ""Patients"".""Id"" = duplicates.""Id"" AND duplicates.rn > 1;
        ");

        migrationBuilder.Sql(@"
            WITH duplicates AS (
                SELECT ""Id"", ""NormalizedWhatsApp"", 
                       ROW_NUMBER() OVER (PARTITION BY ""NormalizedWhatsApp"" ORDER BY ""CreatedAt"" ASC) as rn
                FROM ""Patients"" 
                WHERE ""NormalizedWhatsApp"" IS NOT NULL AND ""NormalizedWhatsApp"" != ''
            )
            UPDATE ""Patients"" SET ""NormalizedWhatsApp"" = NULL
            FROM duplicates
            WHERE ""Patients"".""Id"" = duplicates.""Id"" AND duplicates.rn > 1;
        ");

        // FIX (Replit migration recovery, 2026-07-29): AddPhoneNormalizationAndArchive already
        // creates "IX_Patients_NormalizedPhone" idempotently (CREATE UNIQUE INDEX IF NOT EXISTS),
        // so the original non-idempotent CreateIndex calls here always failed with
        // "relation already exists" on a fresh database. Converted to idempotent raw SQL.
        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Patients_NormalizedPhone""
                ON ""Patients"" (""NormalizedPhone"")
                WHERE ""NormalizedPhone"" IS NOT NULL AND ""NormalizedPhone"" != '';
        ");

        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Patients_NormalizedWhatsApp""
                ON ""Patients"" (""NormalizedWhatsApp"")
                WHERE ""NormalizedWhatsApp"" IS NOT NULL AND ""NormalizedWhatsApp"" != '';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Patients_NormalizedPhone", table: "Patients");
        migrationBuilder.DropIndex(name: "IX_Patients_NormalizedWhatsApp", table: "Patients");
        migrationBuilder.DropColumn(name: "NormalizedPhone", table: "Patients");
        migrationBuilder.DropColumn(name: "NormalizedWhatsApp", table: "Patients");
    }
}
