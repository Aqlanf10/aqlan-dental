using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AqlanDentalPro.Infrastructure.Data;

#nullable disable

namespace AqlanDentalPro.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// SEC-23: Make the AuditLogs table append-only at the database level.
    ///
    /// A privileged user (or an attacker who compromised an Admin account, or a bug in
    /// application code) could otherwise UPDATE or DELETE rows from "AuditLogs" to erase
    /// the trail of a fraudulent operation (cashier close override, invoice cancellation,
    /// permission grant, password reset, etc.). The application never updates or deletes
    /// audit rows, so a DB-level guard is safe.
    ///
    /// This migration installs a PostgreSQL trigger function that raises an exception on
    /// any UPDATE or DELETE against "AuditLogs", and a BEFORE UPDATE OR DELETE trigger
    /// that fires it. INSERTs and TRUNCATE-via-Superuser still work (PostgreSQL does not
    /// support blocking TRUNCATE via triggers, so that path is out of scope; only the
    /// table owner / superuser can TRUNCATE, and that role is trusted).
    ///
    /// The trigger function is SECURITY DEFINER owned by the table owner so it cannot be
    /// bypassed by revoking permissions on the function. Idempotent: re-running the Up
    /// drops the trigger and function first if they exist.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260707000000_AddAuditLogsAppendOnlyTrigger")]
    public partial class AddAuditLogsAppendOnlyTrigger : Migration
    {
        // Use string.Concat / string.Join to avoid verbatim-string issues with PostgreSQL
        // dollar-quoting ($$ ... $$). Each fragment is a single line so there are no
        // newline-in-constant parser surprises.

        private const string DropExistingSql = @"
DROP TRIGGER IF EXISTS ""audit_logs_no_update_or_delete"" ON ""AuditLogs"";
DROP FUNCTION IF EXISTS ""audit_logs_block_update_delete"";
";

        private const string CreateFunctionSql = @"
CREATE OR REPLACE FUNCTION ""audit_logs_block_update_delete""()
RETURNS trigger AS
$func$
BEGIN
    RAISE EXCEPTION 'SEC-23: AuditLogs is append-only (INSERT allowed; UPDATE and DELETE are blocked). لا يمكن تعديل أو حذف سجلات التدقيق.';
END;
$func$
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public, pg_temp;
";

        private const string CreateTriggerSql = @"
CREATE TRIGGER ""audit_logs_no_update_or_delete""
BEFORE UPDATE OR DELETE ON ""AuditLogs""
FOR EACH ROW
EXECUTE FUNCTION ""audit_logs_block_update_delete""();
";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropExistingSql);
            migrationBuilder.Sql(CreateFunctionSql);
            migrationBuilder.Sql(CreateTriggerSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(DropExistingSql);
        }
    }
}
