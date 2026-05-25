using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyPatientArchiveImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyFileNumber",
                table: "Patients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyFullName",
                table: "Patients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyMobile",
                table: "Patients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyPhone",
                table: "Patients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyPhone2",
                table: "Patients",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacySourceId",
                table: "Patients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LegacyFinancialArchiveEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceEntryId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegacyFileNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DebitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceDocumentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReconciliationStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyFinancialArchiveEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyFinancialArchiveEntries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyTreatmentArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SourceLineId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceDocumentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LegacyFileNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TreatmentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ServiceName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DoctorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsOrthodonticService = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyTreatmentArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyTreatmentArchives_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LegacyFileNumber",
                table: "Patients",
                column: "LegacyFileNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LegacySourceId",
                table: "Patients",
                column: "LegacySourceId",
                unique: true,
                filter: "\"LegacySourceId\" IS NOT NULL AND \"LegacySourceId\" != ''");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyFinancialArchiveEntries_PatientId",
                table: "LegacyFinancialArchiveEntries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyFinancialArchiveEntries_SourceSystem_SourceEntryId",
                table: "LegacyFinancialArchiveEntries",
                columns: new[] { "SourceSystem", "SourceEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyTreatmentArchives_PatientId",
                table: "LegacyTreatmentArchives",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyTreatmentArchives_SourceSystem_SourceLineId",
                table: "LegacyTreatmentArchives",
                columns: new[] { "SourceSystem", "SourceLineId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyFinancialArchiveEntries");

            migrationBuilder.DropTable(
                name: "LegacyTreatmentArchives");

            migrationBuilder.DropIndex(
                name: "IX_Patients_LegacyFileNumber",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_Patients_LegacySourceId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacyFileNumber",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacyFullName",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacyMobile",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacyPhone",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacyPhone2",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "LegacySourceId",
                table: "Patients");
        }
    }
}
