using AqlanDentalPro.Infrastructure.Data.Migrations;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Security;

/// <summary>
/// SEC-23: verify the AuditLogs append-only trigger migration declares the correct
/// PostgreSQL trigger function and BEFORE UPDATE OR DELETE trigger, and that it is
/// safely idempotent (drops existing trigger/function before recreating).
///
/// These tests inspect the migration's private SQL constants via reflection — no live
/// PostgreSQL connection required. The constants are the actual SQL fragments emitted
/// by Up() and Down() at runtime.
/// </summary>
public class AuditLogsAppendOnlyTriggerMigrationTests
{
    private static string GetPrivateSqlField(string fieldName)
    {
        var t = typeof(AddAuditLogsAppendOnlyTrigger);
        var field = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull($"migration must declare a '{fieldName}' SQL constant");
        return (string)field!.GetValue(null)!;
    }

    [Fact]
    public void DropExistingSql_Drops_Trigger_And_Function_Idempotently()
    {
        var sql = GetPrivateSqlField("DropExistingSql");

        sql.Should().Contain("DROP TRIGGER IF EXISTS \"audit_logs_no_update_or_delete\"");
        sql.Should().Contain("DROP FUNCTION IF EXISTS \"audit_logs_block_update_delete\"");
    }

    [Fact]
    public void CreateFunctionSql_Installs_Security_Definer_Trigger_Function()
    {
        var sql = GetPrivateSqlField("CreateFunctionSql");

        sql.Should().Contain("CREATE OR REPLACE FUNCTION \"audit_logs_block_update_delete\"");
        sql.Should().Contain("LANGUAGE plpgsql");
        sql.Should().Contain("SECURITY DEFINER");
        sql.Should().Contain("RAISE EXCEPTION");
    }

    [Fact]
    public void CreateFunctionSql_Rejects_Both_Update_And_Delete_With_Arabic_And_English_Message()
    {
        var sql = GetPrivateSqlField("CreateFunctionSql");

        sql.Should().Contain("UPDATE");
        sql.Should().Contain("DELETE");
        sql.Should().Contain("append-only");
        // Arabic confirmation that audit records cannot be modified or deleted.
        sql.Should().Contain("لا يمكن تعديل أو حذف سجلات التدقيق");
    }

    [Fact]
    public void CreateTriggerSql_Installs_Before_Update_Or_Delete_Trigger_On_AuditLogs()
    {
        var sql = GetPrivateSqlField("CreateTriggerSql");

        sql.Should().Contain("CREATE TRIGGER \"audit_logs_no_update_or_delete\"");
        sql.Should().Contain("BEFORE UPDATE OR DELETE ON \"AuditLogs\"");
        sql.Should().Contain("FOR EACH ROW");
        sql.Should().Contain("EXECUTE FUNCTION \"audit_logs_block_update_delete\"");
    }

    [Fact]
    public void Migration_Class_Name_Follows_Convention()
    {
        // Migration class name must match the file name suffix for EF Core to discover it.
        typeof(AddAuditLogsAppendOnlyTrigger).Name
            .Should().Be("AddAuditLogsAppendOnlyTrigger");
    }
}
