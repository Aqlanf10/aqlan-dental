using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// إضافة دعم محادثات المرضى: ConversationType, PatientId, BranchId
/// </summary>
public partial class AddPatientConversationSupport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Add ConversationType column with default value
        migrationBuilder.AddColumn<string>(
            name: "ConversationType",
            table: "Conversations",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "StaffToStaff");

        // Add PatientId column
        migrationBuilder.AddColumn<Guid>(
            name: "PatientId",
            table: "Conversations",
            type: "uuid",
            nullable: true);

        // Add BranchId column
        migrationBuilder.AddColumn<Guid>(
            name: "BranchId",
            table: "Conversations",
            type: "uuid",
            nullable: true);

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_Conversations_PatientId",
            table: "Conversations",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_Conversations_ConversationType",
            table: "Conversations",
            column: "ConversationType");

        // Add foreign keys
        migrationBuilder.AddForeignKey(
            name: "FK_Conversations_Patients_PatientId",
            table: "Conversations",
            column: "PatientId",
            principalTable: "Patients",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Conversations_Branches_BranchId",
            table: "Conversations",
            column: "BranchId",
            principalTable: "Branches",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Conversations_Patients_PatientId", table: "Conversations");
        migrationBuilder.DropForeignKey(name: "FK_Conversations_Branches_BranchId", table: "Conversations");
        migrationBuilder.DropIndex(name: "IX_Conversations_PatientId", table: "Conversations");
        migrationBuilder.DropIndex(name: "IX_Conversations_ConversationType", table: "Conversations");
        migrationBuilder.DropColumn(name: "ConversationType", table: "Conversations");
        migrationBuilder.DropColumn(name: "PatientId", table: "Conversations");
        migrationBuilder.DropColumn(name: "BranchId", table: "Conversations");
    }
}
