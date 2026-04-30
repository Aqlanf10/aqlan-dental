using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationPatientAndType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationType",
                table: "Conversations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PatientId",
                table: "Conversations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_PatientId",
                table: "Conversations",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Patients_PatientId",
                table: "Conversations",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Patients_PatientId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_PatientId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ConversationType",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "Conversations");
        }
    }
}
