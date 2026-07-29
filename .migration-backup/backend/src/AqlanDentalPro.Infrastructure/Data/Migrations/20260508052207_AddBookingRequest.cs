using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // FIX (Replit migration recovery, 2026-07-29): the earlier migration
            // AddBookingRequests already creates "BookingRequests" with this exact column set,
            // so the original non-idempotent CreateTable here always failed with "relation
            // already exists" on a fresh database. Converted to idempotent raw SQL with the same
            // columns/types/constraint; no schema change from what this file originally specified.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""BookingRequests"" (
                    ""Id"" uuid NOT NULL,
                    ""PatientName"" text NOT NULL,
                    ""PhoneNumber"" text NOT NULL,
                    ""Email"" text NULL,
                    ""ServiceType"" text NULL,
                    ""PreferredDate"" date NULL,
                    ""PreferredTime"" text NULL,
                    ""Notes"" text NULL,
                    ""Status"" text NOT NULL,
                    ""StaffNotes"" text NULL,
                    ""CreatedAt"" timestamp with time zone NOT NULL,
                    ""UpdatedAt"" timestamp with time zone NULL,
                    CONSTRAINT ""PK_BookingRequests"" PRIMARY KEY (""Id"")
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingRequests");
        }
    }
}
