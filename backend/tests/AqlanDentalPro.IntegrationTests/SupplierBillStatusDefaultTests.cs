using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// LAB-FINANCE-ROOT-CAUSE-1: migration <c>20260618000000_FixEnumColumnTypesPhase1</c>
/// converted <c>SupplierBills.Status</c> from an integer column to
/// <c>varchar(20)</c> via raw SQL but never re-added a DB-level <c>DEFAULT</c>, even
/// though the EF model (<see cref="AqlanDentalPro.Infrastructure.Data.Configurations.SupplierBillConfiguration"/>)
/// has always declared <c>.HasDefaultValue(BillStatus.Unpaid)</c>. Because
/// <see cref="BillStatus.Unpaid"/> is the enum's CLR default (0) — and also the value
/// every new bill actually gets — EF Core's SaveChanges omitted the column from the
/// INSERT entirely, trusting the (missing) DB default, so Postgres rejected every new
/// row as a NULL violation. This never surfaced in InMemory-provider unit tests
/// because that provider does not reproduce column-omission-on-insert behavior; it
/// only reproduces against a real Postgres engine, hence this integration test using
/// a Testcontainers-backed database via <see cref="TestWebAppFactory"/>.
///
/// This is exactly the class of bug that put a lab order into "sent" and crashed
/// with a 500 (23502: null value in column "Status") instead of creating a
/// LabPayable/SupplierBill/JournalEntry — the reason lab orders never reached
/// payables/finance-v3/profitability for any brand-new order in production.
///
/// Fixed forward via migration <c>20260729070000_FixSupplierBillStatusColumnDefault</c>
/// (never edits the historical migration) which adds
/// <c>ALTER TABLE "SupplierBills" ALTER COLUMN "Status" SET DEFAULT 'Unpaid'</c>.
/// </summary>
public class SupplierBillStatusDefaultTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;

    public SupplierBillStatusDefaultTests(TestWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Reproduces the exact failure mode: insert a SupplierBill via EF Core with
    /// Status left at its CLR default (BillStatus.Unpaid), against a real Postgres
    /// engine (not InMemory). Before the fix this threw
    /// PostgresException 23502 (null value in column "Status" violates not-null
    /// constraint) because EF omitted the column from the INSERT, trusting a DB
    /// default that did not exist.
    /// </summary>
    [Fact]
    public async Task InsertingSupplierBillWithDefaultStatus_DoesNotThrow_AndPersistsUnpaid()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var branch = new Branch { Name = "الفرع الرئيسي" };
        db.Branches.Add(branch);

        var supplier = new Supplier { Name = "مختبر الأسنان المتقدم", IsActive = true };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var bill = new SupplierBill
        {
            BillNumber = "BILL-TEST-0001",
            SupplierId = supplier.Id,
            Description = "تكلفة طلب مختبر - اختبار الانحدار",
            TotalAmount = 12_000m,
            BillDate = DateOnly.FromDateTime(DateTime.UtcNow),
            BranchId = branch.Id,
            CreatedBy = Guid.NewGuid(),
            // Status intentionally left unset — it must resolve to its CLR
            // default (BillStatus.Unpaid) and still round-trip through Postgres.
        };
        db.SupplierBills.Add(bill);

        var act = async () => await db.SaveChangesAsync();
        await act.Should().NotThrowAsync(
            "the Status column must have a DB-level default matching the EF model's " +
            "HasDefaultValue(BillStatus.Unpaid) — its absence is what crashed every " +
            "'sent' lab-order transition with a 23502 null-constraint violation");

        var reloaded = await db.SupplierBills.AsNoTracking().SingleAsync(b => b.Id == bill.Id);
        reloaded.Status.Should().Be(BillStatus.Unpaid);
    }

    /// <summary>
    /// Confirms the DB-level default itself is present on the column (not just that
    /// the ORM-level insert happens to work) — asserts against
    /// <c>information_schema.columns.column_default</c> directly, so a future
    /// migration that accidentally drops the default again is caught even before
    /// any row is inserted.
    /// </summary>
    [Fact]
    public async Task SupplierBillsStatusColumn_HasADatabaseLevelDefault()
    {
        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_default
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'SupplierBills' AND column_name = 'Status';
            """;
        var columnDefault = (string?)await cmd.ExecuteScalarAsync();

        columnDefault.Should().NotBeNullOrEmpty(
            "SupplierBills.Status must have a DB-level DEFAULT so EF's column-omission " +
            "optimization on inserts (when a value equals the CLR default) does not violate " +
            "the NOT NULL constraint");
        columnDefault.Should().Contain("Unpaid");
    }
}
