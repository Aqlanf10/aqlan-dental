using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260502010000_AddSecurePatientPortalPasswordAuth")]
public partial class AddSecurePatientPortalPasswordAuth : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new secure fields to PatientAccounts
        migrationBuilder.AddColumn<bool>(
            name: "MustChangePassword",
            table: "PatientAccounts",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "PortalAccountActive",
            table: "PatientAccounts",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<Guid>(
            name: "LinkedUserId",
            table: "PatientAccounts",
            nullable: true);

        // FIX (Replit migration recovery, 2026-07-29): no migration in this history ever creates
        // an "InitialPassword" column on "PatientAccounts" (AddPatientPortal's CreateTable never
        // included it), so on a fresh database the original backfill/DropColumn always failed
        // with "column does not exist". Guarded both statements to only run when the column is
        // actually present (e.g. on a database created before this gap existed); when absent,
        // PortalAccountActive is still backfilled to true so the column's intended default holds.
        migrationBuilder.Sql(@"
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns
                           WHERE table_name = 'PatientAccounts' AND column_name = 'InitialPassword') THEN
                    UPDATE ""PatientAccounts""
                    SET ""MustChangePassword"" = CASE WHEN ""InitialPassword"" IS NOT NULL AND ""InitialPassword"" != '' THEN true ELSE false END,
                        ""PortalAccountActive"" = true;
                    ALTER TABLE ""PatientAccounts"" DROP COLUMN ""InitialPassword"";
                ELSE
                    UPDATE ""PatientAccounts"" SET ""PortalAccountActive"" = true;
                END IF;
            END $$;
        ");

        // Create index on LinkedUserId
        migrationBuilder.CreateIndex(
            name: "IX_PatientAccounts_LinkedUserId",
            table: "PatientAccounts",
            column: "LinkedUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_PatientAccounts_Users_LinkedUserId",
            table: "PatientAccounts",
            column: "LinkedUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_PatientAccounts_Users_LinkedUserId",
            table: "PatientAccounts");

        migrationBuilder.DropIndex(
            name: "IX_PatientAccounts_LinkedUserId",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "MustChangePassword",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "PortalAccountActive",
            table: "PatientAccounts");

        migrationBuilder.DropColumn(
            name: "LinkedUserId",
            table: "PatientAccounts");

        migrationBuilder.AddColumn<string>(
            name: "InitialPassword",
            table: "PatientAccounts",
            maxLength: 100,
            nullable: true);
    }
}
