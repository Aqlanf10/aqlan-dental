using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyAppointmentsAndLinkedRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegacyAppointmentArchives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceAppointmentId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LegacyFileNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppointmentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchiveType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyAppointmentArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyAppointmentArchives_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LegacyLinkedArchiveRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceTable = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Classification = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LegacyFileNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LegacyTypeId = table.Column<int>(type: "integer", nullable: true),
                    DateValue01 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateValue02 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NumberValue01 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AccountName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegacyLinkedArchiveRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LegacyLinkedArchiveRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegacyAppointmentArchives_PatientId",
                table: "LegacyAppointmentArchives",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyAppointmentArchives_SourceSystem_SourceAppointmentId",
                table: "LegacyAppointmentArchives",
                columns: new[] { "SourceSystem", "SourceAppointmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegacyLinkedArchiveRecords_PatientId",
                table: "LegacyLinkedArchiveRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LegacyLinkedArchiveRecords_SourceSystem_SourceTable_SourceR~",
                table: "LegacyLinkedArchiveRecords",
                columns: new[] { "SourceSystem", "SourceTable", "SourceRecordId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegacyAppointmentArchives");

            migrationBuilder.DropTable(
                name: "LegacyLinkedArchiveRecords");
        }
    }
}
