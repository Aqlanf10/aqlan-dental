using AqlanDentalPro.Infrastructure.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace AqlanDentalPro.UnitTests.Finance;

public sealed class CommissionPaymentMigrationTests
{
    private sealed class TestableMigration : AddAtomicCommissionAllocations
    {
        public void BuildUp(MigrationBuilder builder) => Up(builder);
        public void BuildDown(MigrationBuilder builder) => Down(builder);
    }

    [Fact]
    public void W07Migration_DeclaresIdempotencyAllocationAndAppendOnlyGuards()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildUp(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));

        sql.Should().Contain("IX_DoctorCommissionPayments_BranchId_IdempotencyKey");
        sql.Should().Contain("CK_DoctorCommissionPaymentAllocations_ExactlyOneTarget");
        sql.Should().Contain("commission allocation exceeds payment amount");
        sql.Should().Contain("IS DISTINCT FROM target_doctor");
        sql.Should().Contain("BEFORE UPDATE OR DELETE ON \"DoctorCommissionPaymentAllocations\"");
        sql.Should().Contain("DEFERRABLE INITIALLY DEFERRED");
        sql.Should().Contain("commission payment must be fully allocated");
    }

    [Fact]
    public void W07Migration_DownRemovesEveryTriggerAndFunctionBeforeTheTable()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildDown(builder);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));

        sql.Should().Contain("DROP TRIGGER IF EXISTS \"TR_DoctorCommissionPaymentAllocations_Ceiling\"");
        sql.Should().Contain("DROP TRIGGER IF EXISTS \"TR_DoctorCommissionPayments_FullyAllocated\"");
        sql.Should().Contain("DROP FUNCTION IF EXISTS enforce_commission_allocation_ceiling()");
        sql.Should().Contain("DROP TABLE IF EXISTS \"DoctorCommissionPaymentAllocations\"");
        sql.IndexOf("DROP FUNCTION", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("DROP TABLE", StringComparison.Ordinal));
    }
}
