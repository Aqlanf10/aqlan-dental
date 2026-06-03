using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sprint2_LabOrderAndPaymentMethodSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Sprint 2: LabOrder extended fields ──────────────────────────
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "LabOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DeliveredDate",
                table: "LabOrders",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestorationType",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shade",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitId",
                table: "LabOrders",
                type: "uuid",
                nullable: true);

            // ── Sprint 2: PaymentMethodSettings table ───────────────────────
            migrationBuilder.CreateTable(
                name: "PaymentMethodSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequiresReferenceNumber = table.Column<bool>(type: "boolean", nullable: false),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethodSettings", x => x.Id);
                });

            // ── Sprint 2: Indexes ────────────────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_BranchId",
                table: "LabOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_VisitId",
                table: "LabOrders",
                column: "VisitId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodSettings_BranchId",
                table: "PaymentMethodSettings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethodSettings_Code",
                table: "PaymentMethodSettings",
                column: "Code",
                unique: true);

            // ── Sprint 2: FK ────────────────────────────────────────────────
            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_Visits_VisitId",
                table: "LabOrders",
                column: "VisitId",
                principalTable: "Visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_Visits_VisitId",
                table: "LabOrders");

            migrationBuilder.DropTable(
                name: "PaymentMethodSettings");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_BranchId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_VisitId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "DeliveredDate",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "RestorationType",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "Shade",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "VisitId",
                table: "LabOrders");
        }
    }
}
