using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds AddedByUserId, CalledByUserId, Notes columns to ClinicQueueItems.
/// Also renames the old "CalledBy" column to "CalledByUserId".
/// Non-destructive: only adds new nullable columns and renames.
/// </summary>
public partial class AddClinicQueueItemTrackingFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add new columns (all nullable, non-destructive)
        migrationBuilder.AddColumn<Guid>(
            name: "AddedByUserId",
            table: "ClinicQueueItems",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "ClinicQueueItems",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CalledByUserId",
            table: "ClinicQueueItems",
            type: "uuid",
            nullable: true);

        // Migrate data from old CalledBy column to CalledByUserId if it exists
        migrationBuilder.Sql("""
            UPDATE "ClinicQueueItems" SET "CalledByUserId" = "CalledBy" WHERE "CalledBy" IS NOT NULL;
        """);

        // Drop the old CalledBy column
        migrationBuilder.DropColumn(
            name: "CalledBy",
            table: "ClinicQueueItems");

        // Add FK for AddedByUserId
        migrationBuilder.AddForeignKey(
            name: "FK_ClinicQueueItems_Users_AddedByUserId",
            table: "ClinicQueueItems",
            column: "AddedByUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // Add FK for CalledByUserId
        migrationBuilder.AddForeignKey(
            name: "FK_ClinicQueueItems_Users_CalledByUserId",
            table: "ClinicQueueItems",
            column: "CalledByUserId",
            principalTable: "Users",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Re-add the old CalledBy column
        migrationBuilder.AddColumn<Guid>(
            name: "CalledBy",
            table: "ClinicQueueItems",
            type: "uuid",
            nullable: true);

        // Migrate data back
        migrationBuilder.Sql("""
            UPDATE "ClinicQueueItems" SET "CalledBy" = "CalledByUserId" WHERE "CalledByUserId" IS NOT NULL;
        """);

        // Drop FKs
        migrationBuilder.DropForeignKey(
            name: "FK_ClinicQueueItems_Users_AddedByUserId",
            table: "ClinicQueueItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ClinicQueueItems_Users_CalledByUserId",
            table: "ClinicQueueItems");

        // Drop new columns
        migrationBuilder.DropColumn(
            name: "AddedByUserId",
            table: "ClinicQueueItems");

        migrationBuilder.DropColumn(
            name: "Notes",
            table: "ClinicQueueItems");

        migrationBuilder.DropColumn(
            name: "CalledByUserId",
            table: "ClinicQueueItems");
    }
}
