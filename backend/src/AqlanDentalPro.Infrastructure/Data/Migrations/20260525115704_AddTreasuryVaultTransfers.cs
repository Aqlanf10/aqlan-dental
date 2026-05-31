using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreasuryVaultTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PlanLabel",
                table: "TreatmentPlans",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "A",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldNullable: true,
                oldDefaultValue: "A");

            migrationBuilder.CreateTable(
                name: "Treasuries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Treasuries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Treasuries_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaultTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNumber = table.Column<string>(type: "text", nullable: false),
                    SourceTreasuryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationTreasuryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    TransferDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PerformedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaultTransfers_CashierSessions_CashierSessionId",
                        column: x => x.CashierSessionId,
                        principalTable: "CashierSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VaultTransfers_Treasuries_DestinationTreasuryId",
                        column: x => x.DestinationTreasuryId,
                        principalTable: "Treasuries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VaultTransfers_Treasuries_SourceTreasuryId",
                        column: x => x.SourceTreasuryId,
                        principalTable: "Treasuries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VaultTransfers_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VaultTransfers_Users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Treasuries_BranchId",
                table: "Treasuries",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultTransfers_ApprovedByUserId",
                table: "VaultTransfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultTransfers_CashierSessionId",
                table: "VaultTransfers",
                column: "CashierSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultTransfers_DestinationTreasuryId",
                table: "VaultTransfers",
                column: "DestinationTreasuryId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultTransfers_PerformedByUserId",
                table: "VaultTransfers",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VaultTransfers_SourceTreasuryId",
                table: "VaultTransfers",
                column: "SourceTreasuryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultTransfers");

            migrationBuilder.DropTable(
                name: "Treasuries");

            migrationBuilder.AlterColumn<string>(
                name: "PlanLabel",
                table: "TreatmentPlans",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true,
                defaultValue: "A",
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5,
                oldDefaultValue: "A");
        }
    }
}
