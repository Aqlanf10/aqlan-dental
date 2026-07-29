using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Adds Diagnosis and NextVisitPlan to Visits table,
/// and FileName, FileSize, MimeType, Notes, UploadedBy to Documents table.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260502000000_AddVisitsDocumentsFields")]
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

        // FIX (Replit migration recovery, 2026-07-29): "IX_Visits_PatientId" is already created by
        // InitialCreate, so the original non-idempotent CreateIndex here always failed with
        // "relation already exists" on a fresh database. Converted the four indexes below to
        // idempotent raw SQL (matching this codebase's established pattern); no index definitions
        // were changed.
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Visits_PatientId"" ON ""Visits"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Visits_VisitDate"" ON ""Visits"" (""VisitDate"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Documents_PatientId"" ON ""Documents"" (""PatientId"");");
        migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Documents_DocumentType"" ON ""Documents"" (""DocumentType"");");
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
