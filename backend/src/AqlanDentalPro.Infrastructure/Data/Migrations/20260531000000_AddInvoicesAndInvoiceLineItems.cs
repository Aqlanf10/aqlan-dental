using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Add Invoices and InvoiceLineItems tables for draft invoice foundation.
/// Additive only — new tables, no destructive changes.
/// </summary>
public partial class AddInvoicesAndInvoiceLineItems : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Invoices table ──────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                VisitId = table.Column<Guid>(type: "uuid", nullable: true),
                AppointmentId = table.Column<Guid>(type: "uuid", nullable: true),
                InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                DiscountAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                TaxAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
                table.ForeignKey(
                    name: "FK_Invoices_Patients_PatientId",
                    column: x => x.PatientId,
                    principalTable: "Patients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Invoices_Visits_VisitId",
                    column: x => x.VisitId,
                    principalTable: "Visits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Invoices_Appointments_AppointmentId",
                    column: x => x.AppointmentId,
                    principalTable: "Appointments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_PatientId",
            table: "Invoices",
            column: "PatientId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_VisitId",
            table: "Invoices",
            column: "VisitId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_AppointmentId",
            table: "Invoices",
            column: "AppointmentId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_Status",
            table: "Invoices",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_InvoiceNumber",
            table: "Invoices",
            column: "InvoiceNumber",
            unique: true);

        // ── InvoiceLineItems table ──────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "InvoiceLineItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                ServiceNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                TotalPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                RelatedTreatmentPlanStepId = table.Column<Guid>(type: "uuid", nullable: true),
                RelatedVisitId = table.Column<Guid>(type: "uuid", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLineItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_InvoiceLineItems_Invoices_InvoiceId",
                    column: x => x.InvoiceId,
                    principalTable: "Invoices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_InvoiceLineItems_ClinicServices_ServiceId",
                    column: x => x.ServiceId,
                    principalTable: "ClinicServices",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_InvoiceLineItems_PatientTreatmentPlanSteps_RelatedTreatmentPlanStepId",
                    column: x => x.RelatedTreatmentPlanStepId,
                    principalTable: "PatientTreatmentPlanSteps",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_InvoiceLineItems_Visits_RelatedVisitId",
                    column: x => x.RelatedVisitId,
                    principalTable: "Visits",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_InvoiceId",
            table: "InvoiceLineItems",
            column: "InvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLineItems_ServiceId",
            table: "InvoiceLineItems",
            column: "ServiceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InvoiceLineItems");

        migrationBuilder.DropTable(
            name: "Invoices");
    }
}
