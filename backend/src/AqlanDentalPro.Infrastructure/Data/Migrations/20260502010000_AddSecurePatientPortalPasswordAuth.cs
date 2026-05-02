using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

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

        // Backfill: set MustChangePassword for existing accounts that have InitialPassword
        migrationBuilder.Sql(@"
            UPDATE ""PatientAccounts""
            SET ""MustChangePassword"" = CASE WHEN ""InitialPassword"" IS NOT NULL AND ""InitialPassword"" != '' THEN true ELSE false END,
                ""PortalAccountActive"" = true
        ");

        // Drop the insecure InitialPassword column (no longer storing plain-text passwords)
        migrationBuilder.DropColumn(
            name: "InitialPassword",
            table: "PatientAccounts");

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
