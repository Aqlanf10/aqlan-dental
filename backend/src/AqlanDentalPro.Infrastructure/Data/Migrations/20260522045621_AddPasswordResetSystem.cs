using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CenterShareAmount",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "CommissionApprovedAt",
                table: "InvoiceLineItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommissionApprovedBy",
                table: "InvoiceLineItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommissionBaseRule",
                table: "InvoiceLineItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CommissionNotes",
                table: "InvoiceLineItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CommissionStatus",
                table: "InvoiceLineItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DoctorCommissionAmount",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DoctorCommissionPercentage",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "DoctorId",
                table: "InvoiceLineItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LabCost",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "LabOrderId",
                table: "InvoiceLineItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LineDiscountAmount",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialCost",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetCommissionableAmount",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OtherDirectCost",
                table: "InvoiceLineItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CommissionBaseRule",
                table: "ClinicServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CommissionRecognitionMode",
                table: "ClinicServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultDoctorCommissionPercentage",
                table: "ClinicServices",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultLabCost",
                table: "ClinicServices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultMaterialCost",
                table: "ClinicServices",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DefaultMaterialCostType",
                table: "ClinicServices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DoctorCommissionPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PaymentMethod = table.Column<string>(type: "text", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PaidBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorCommissionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorCommissionPayments_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedIpHash = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_DoctorId",
                table: "InvoiceLineItems",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_LabOrderId",
                table: "InvoiceLineItems",
                column: "LabOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorCommissionPayments_DoctorId",
                table: "DoctorCommissionPayments",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ApprovedByUserId",
                table: "PasswordResetRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_Status",
                table: "PasswordResetRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserId",
                table: "PasswordResetRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_TokenHash",
                table: "PasswordResetTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_UserId",
                table: "PasswordResetTokens",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_Doctors_DoctorId",
                table: "InvoiceLineItems",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId",
                table: "InvoiceLineItems",
                column: "LabOrderId",
                principalTable: "LabOrders",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_Doctors_DoctorId",
                table: "InvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_LabOrders_LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropTable(
                name: "DoctorCommissionPayments");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_DoctorId",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CenterShareAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionApprovedAt",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionApprovedBy",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionBaseRule",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionNotes",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionStatus",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DoctorCommissionAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DoctorCommissionPercentage",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LabCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LabOrderId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LineDiscountAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "MaterialCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "NetCommissionableAmount",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "OtherDirectCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "CommissionBaseRule",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "CommissionRecognitionMode",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultDoctorCommissionPercentage",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultLabCost",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultMaterialCost",
                table: "ClinicServices");

            migrationBuilder.DropColumn(
                name: "DefaultMaterialCostType",
                table: "ClinicServices");
        }
    }
}
