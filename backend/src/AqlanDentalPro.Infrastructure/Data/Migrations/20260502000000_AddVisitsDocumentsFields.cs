using Microsoft.EntityFrameworkCore.Migrations;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds Diagnosis and NextVisitPlan to Visits table,
/// and FileName, FileSize, MimeType, Notes, UploadedBy to Documents table.
/// </summary>
public partial class AddVisitsDocumentsFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Visit: add Diagnosis column
        migrationBuilder.AddColumn<string>(
            name: "Diagnosis",
            table: "Visits",
            type: "text",
            nullable: true);

        // Visit: add NextVisitPlan column
        migrationBuilder.AddColumn<string>(
            name: "NextVisitPlan",
            table: "Visits",
            type: "text",
            nullable: true);

        // Document: add FileName column
        migrationBuilder.AddColumn<string>(
            name: "FileName",
            table: "Documents",
            type: "text",
            nullable: true);

        // Document: add FileSize column
        migrationBuilder.AddColumn<long>(
            name: "FileSize",
            table: "Documents",
            type: "bigint",
            nullable: true);

        // Document: add MimeType column
        migrationBuilder.AddColumn<string>(
            name: "MimeType",
            table: "Documents",
            type: "text",
            nullable: true);

        // Document: add Notes column
        migrationBuilder.AddColumn<string>(
            name: "Notes",
            table: "Documents",
            type: "text",
            nullable: true);

        // Document: add UploadedBy column
        migrationBuilder.AddColumn<Guid>(
            name: "UploadedBy",
            table: "Documents",
            type: "uuid",
            nullable: true);

        // Add indexes for performance
        migrationBuilder.CreateIndex(
            name: "IX_Visits_PatientId",
            table: "Visits",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_Visits_VisitDate",
            table: "Visits",
            column: "VisitDate");

        migrationBuilder.CreateIndex(
            name: "IX_Documents_PatientId",
            table: "Documents",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_Documents_DocumentType",
            table: "Documents",
            column: "DocumentType");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Visits_PatientId", "Visits");
        migrationBuilder.DropIndex("IX_Visits_VisitDate", "Visits");
        migrationBuilder.DropIndex("IX_Documents_PatientId", "Documents");
        migrationBuilder.DropIndex("IX_Documents_DocumentType", "Documents");

        migrationBuilder.DropColumn("Diagnosis", "Visits");
        migrationBuilder.DropColumn("NextVisitPlan", "Visits");
        migrationBuilder.DropColumn("FileName", "Documents");
        migrationBuilder.DropColumn("FileSize", "Documents");
        migrationBuilder.DropColumn("MimeType", "Documents");
        migrationBuilder.DropColumn("Notes", "Documents");
        migrationBuilder.DropColumn("UploadedBy", "Documents");
    }
}
