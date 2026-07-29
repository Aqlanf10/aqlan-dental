using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations;

/// <summary>
/// Repairs visit-generated invoice lines that already have calculated commission
/// values but were missing the performing doctor link.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260728090000_BackfillInvoiceLineItemDoctors")]
public sealed class BackfillInvoiceLineItemDoctors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "InvoiceLineItems" AS line
            SET "DoctorId" = COALESCE(
                (
                    SELECT visit."DoctorId"
                    FROM "Visits" AS visit
                    WHERE visit."Id" = line."RelatedVisitId"
                ),
                (
                    SELECT appointment."DoctorId"
                    FROM "Visits" AS visit
                    JOIN "Appointments" AS appointment
                      ON appointment."Id" = visit."AppointmentId"
                    WHERE visit."Id" = line."RelatedVisitId"
                ),
                (
                    SELECT visit."DoctorId"
                    FROM "Invoices" AS invoice
                    JOIN "Visits" AS visit
                      ON visit."Id" = invoice."VisitId"
                    WHERE invoice."Id" = line."InvoiceId"
                ),
                (
                    SELECT appointment."DoctorId"
                    FROM "Invoices" AS invoice
                    JOIN "Appointments" AS appointment
                      ON appointment."Id" = invoice."AppointmentId"
                    WHERE invoice."Id" = line."InvoiceId"
                )
            )
            WHERE line."DoctorId" IS NULL
              AND COALESCE(
                (
                    SELECT visit."DoctorId"
                    FROM "Visits" AS visit
                    WHERE visit."Id" = line."RelatedVisitId"
                ),
                (
                    SELECT appointment."DoctorId"
                    FROM "Visits" AS visit
                    JOIN "Appointments" AS appointment
                      ON appointment."Id" = visit."AppointmentId"
                    WHERE visit."Id" = line."RelatedVisitId"
                ),
                (
                    SELECT visit."DoctorId"
                    FROM "Invoices" AS invoice
                    JOIN "Visits" AS visit
                      ON visit."Id" = invoice."VisitId"
                    WHERE invoice."Id" = line."InvoiceId"
                ),
                (
                    SELECT appointment."DoctorId"
                    FROM "Invoices" AS invoice
                    JOIN "Appointments" AS appointment
                      ON appointment."Id" = invoice."AppointmentId"
                    WHERE invoice."Id" = line."InvoiceId"
                )
              ) IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not erase valid doctor attribution if this data-only repair is rolled back.
    }
}
