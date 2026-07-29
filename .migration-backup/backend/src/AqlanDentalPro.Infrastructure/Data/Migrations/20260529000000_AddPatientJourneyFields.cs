using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Add nullable Patient Journey fields to Appointments and Visits tables.
/// Additive only — no drops, no destructive changes.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260529000000_AddPatientJourneyFields")]
public partial class AddPatientJourneyFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Appointments table: add ServiceId, ClinicRoomId ─────────────────
        migrationBuilder.AddColumn<Guid>(
            name: "ServiceId",
            table: "Appointments",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ClinicRoomId",
            table: "Appointments",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Appointments_ServiceId",
            table: "Appointments",
            column: "ServiceId");

        // ── Visits table: add ServiceId, CheckoutStatus, ReadyForCheckoutAt, AmountDueReference ──
        migrationBuilder.AddColumn<Guid>(
            name: "ServiceId",
            table: "Visits",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CheckoutStatus",
            table: "Visits",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ReadyForCheckoutAt",
            table: "Visits",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "AmountDueReference",
            table: "Visits",
            type: "numeric(12,2)",
            precision: 12,
            scale: 2,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Visits_CheckoutStatus",
            table: "Visits",
            column: "CheckoutStatus");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Appointments_ServiceId", table: "Appointments");
        migrationBuilder.DropIndex(name: "IX_Visits_CheckoutStatus", table: "Visits");

        migrationBuilder.DropColumn(name: "ServiceId", table: "Appointments");
        migrationBuilder.DropColumn(name: "ClinicRoomId", table: "Appointments");
        migrationBuilder.DropColumn(name: "ServiceId", table: "Visits");
        migrationBuilder.DropColumn(name: "CheckoutStatus", table: "Visits");
        migrationBuilder.DropColumn(name: "ReadyForCheckoutAt", table: "Visits");
        migrationBuilder.DropColumn(name: "AmountDueReference", table: "Visits");
    }
}
