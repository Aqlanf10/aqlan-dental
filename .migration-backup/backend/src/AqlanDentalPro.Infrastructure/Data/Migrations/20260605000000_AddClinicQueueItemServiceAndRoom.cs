using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Add ServiceId and ClinicRoomId columns to ClinicQueueItems with FK constraints.
/// Additive only — no drops, no destructive changes.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260605000000_AddClinicQueueItemServiceAndRoom")]
public partial class AddClinicQueueItemServiceAndRoom : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── ClinicQueueItems: add ServiceId column ──────────────────────────
        migrationBuilder.AddColumn<Guid>(
            name: "ServiceId",
            table: "ClinicQueueItems",
            type: "uuid",
            nullable: true);

        // ── ClinicQueueItems: add ClinicRoomId column ───────────────────────
        migrationBuilder.AddColumn<Guid>(
            name: "ClinicRoomId",
            table: "ClinicQueueItems",
            type: "uuid",
            nullable: true);

        // ── Indexes ─────────────────────────────────────────────────────────
        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_ServiceId",
            table: "ClinicQueueItems",
            column: "ServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_ClinicQueueItems_ClinicRoomId",
            table: "ClinicQueueItems",
            column: "ClinicRoomId");

        // ── Foreign Keys (ON DELETE SET NULL) ───────────────────────────────
        migrationBuilder.AddForeignKey(
            name: "FK_ClinicQueueItems_ClinicServices_ServiceId",
            table: "ClinicQueueItems",
            column: "ServiceId",
            principalTable: "ClinicServices",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_ClinicQueueItems_ClinicRooms_ClinicRoomId",
            table: "ClinicQueueItems",
            column: "ClinicRoomId",
            principalTable: "ClinicRooms",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Drop FKs
        migrationBuilder.DropForeignKey(
            name: "FK_ClinicQueueItems_ClinicServices_ServiceId",
            table: "ClinicQueueItems");

        migrationBuilder.DropForeignKey(
            name: "FK_ClinicQueueItems_ClinicRooms_ClinicRoomId",
            table: "ClinicQueueItems");

        // Drop indexes
        migrationBuilder.DropIndex(
            name: "IX_ClinicQueueItems_ServiceId",
            table: "ClinicQueueItems");

        migrationBuilder.DropIndex(
            name: "IX_ClinicQueueItems_ClinicRoomId",
            table: "ClinicQueueItems");

        // Drop columns
        migrationBuilder.DropColumn(name: "ServiceId", table: "ClinicQueueItems");
        migrationBuilder.DropColumn(name: "ClinicRoomId", table: "ClinicQueueItems");
    }
}
