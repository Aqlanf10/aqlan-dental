using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using LabEntity = AqlanDentalPro.Domain.Entities.Lab;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// CORE-LAB-005 — closes the verification gap this repository has carried since the advisory
/// lock was written.
/// <para>
/// The lock exists because <c>LabOrderFinanceSyncService</c> does a check-then-create: it asks
/// "does this order already have a supplier bill?" and creates one if not. Two concurrent
/// requests can both answer "no" and both insert, leaving the clinic owing the same lab twice
/// for one crown. The <c>pg_advisory_xact_lock</c> serialises that whole decision.
/// </para>
/// <para>
/// Until now that was <b>proven by reading the code</b>, not by racing it: advisory locks are a
/// PostgreSQL feature and the entire unit-test suite runs on the EF InMemory provider, which
/// has no such thing. These tests race it against a real PostgreSQL container. Without the
/// lock they fail with two bills; with it, exactly one.
/// </para>
/// </summary>
public class LabOrderFinanceSyncConcurrencyTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private Guid _branchId;
    private Guid _orderId;
    private Guid _userId;

    private const decimal Cost = 60_000m;
    private const int Racers = 8;

    public LabOrderFinanceSyncConcurrencyTests(TestWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        await using var db = await _factory.CreateDbContextAsync();

        var branch = new Branch { Name = "فرع اختبار تزامن المعامل" };
        db.Branches.Add(branch);

        var patient = new Patient
        {
            PatientNumber = $"P-{Guid.NewGuid():N}"[..12],
            FirstName = "سالم",
            LastName = "المريض",
            IsActive = true,
        };
        db.Patients.Add(patient);

        var supplier = new Supplier { Name = "معمل التزامن", Type = SupplierType.DentalLab, IsActive = true };
        db.Suppliers.Add(supplier);

        var lab = new LabEntity { Name = "معمل التزامن", SupplierId = supplier.Id, IsActive = true };
        db.Labs.Add(lab);

        var order = new LabOrder
        {
            PatientId = patient.Id,
            LabId = lab.Id,
            OrderNumber = $"LO-{Guid.NewGuid():N}"[..14],
            ApplianceType = "تاج زيركون",
            Status = "sent",
            Priority = "normal",
            Cost = Cost,
            TotalCost = Cost,
            Currency = "YER",
            ExchangeRateToYer = 1m,
            BranchId = branch.Id,
            SentDate = new DateOnly(2026, 7, 1),
            IsActive = true,
        };
        db.LabOrders.Add(order);

        await db.SaveChangesAsync();

        _branchId = branch.Id;
        _orderId = order.Id;
        _userId = Guid.NewGuid();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Runs one sync in its own scope, DbContext and transaction — the shape of one HTTP
    /// request. The transaction matters: <c>pg_advisory_xact_lock</c> is held until the
    /// transaction ends, so a caller without one would release the lock immediately and the
    /// race would be back.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentBag<string> _racerErrors = new();

    private async Task<bool> SyncOnceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<LabOrderFinanceSyncService>();

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var order = await db.LabOrders.FirstAsync(o => o.Id == _orderId);
            var result = await sync.SyncAsync(order, _branchId, _userId);
            if (!result.Ok)
            {
                _racerErrors.Add($"sync refused: {result.Error}");
                await tx.RollbackAsync();
                return false;
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            // A racer losing to a unique constraint is an acceptable outcome; the assertion
            // that matters is how many bills exist at the end, not how each racer fared. The
            // message is still recorded, so "every racer failed" reports WHY instead of just
            // saying no one won.
            _racerErrors.Add($"{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    [Fact]
    public async Task ConcurrentSyncs_OnTheSameOrder_CreateExactlyOneBillAndOnePayable()
    {
        var results = await Task.WhenAll(
            Enumerable.Range(0, Racers).Select(_ => Task.Run(SyncOnceAsync)));

        results.Should().Contain(true,
            "at least one racer must succeed or nothing was tested. Racer failures: {0}",
            string.Join(" | ", _racerErrors.Distinct().Take(3)));

        await using var db = await _factory.CreateDbContextAsync();

        var bills = await db.SupplierBills.IgnoreQueryFilters()
            .Where(b => b.LabOrderId == _orderId)
            .ToListAsync();

        bills.Should().ContainSingle(
            "the advisory lock serialises check-then-create, so eight concurrent syncs owe the lab once");
        bills[0].TotalAmount.Should().Be(Cost);

        var payables = await db.LabPayables.IgnoreQueryFilters()
            .Where(p => p.LabOrderId == _orderId)
            .ToListAsync();

        payables.Should().ContainSingle("a duplicate payable would double the lab's debt on the payables screen");
        payables[0].Amount.Should().Be(Cost);
    }

    [Fact]
    public async Task ConcurrentSyncs_DoNotDoubleCountTheSupplierBalance()
    {
        // The YER scalar is incremented inside the same transaction as the bill. If the lock
        // failed, the balance would be a multiple of the cost rather than the cost.
        var supplierId = await (await _factory.CreateDbContextAsync()).Suppliers
            .Where(s => s.Type == SupplierType.DentalLab)
            .Select(s => s.Id)
            .FirstAsync();

        await Task.WhenAll(Enumerable.Range(0, Racers).Select(_ => Task.Run(SyncOnceAsync)));

        await using var db = await _factory.CreateDbContextAsync();
        var balance = await db.Suppliers.Where(s => s.Id == supplierId).Select(s => s.Balance).SingleAsync();

        balance.Should().Be(Cost, "the lab is owed the cost once, not once per concurrent request");
    }

    [Fact]
    public async Task ConcurrentSyncs_PostExactlyOneUnreversedJournalEntry()
    {
        // A duplicate posting would unbalance the ledger by the whole cost, which no screen
        // would show until the books were reconciled.
        await Task.WhenAll(Enumerable.Range(0, Racers).Select(_ => Task.Run(SyncOnceAsync)));

        await using var db = await _factory.CreateDbContextAsync();

        var billId = await db.SupplierBills.IgnoreQueryFilters()
            .Where(b => b.LabOrderId == _orderId)
            .Select(b => b.Id)
            .SingleAsync();

        var entries = await db.JournalEntries
            .Where(e => e.FinancialDocumentType == FinancialDocumentType.SupplierBill
                     && e.FinancialDocumentId == billId
                     && !e.IsReversal
                     && e.ReversedByEntryId == null)
            .CountAsync();

        entries.Should().Be(1);
    }

    [Fact]
    public async Task RepeatedSequentialSyncs_ConvergeRatherThanAccumulate()
    {
        // The same guarantee without contention: idempotence is a property of the service,
        // not an accident of the lock. Running it five times must leave one bill.
        for (var i = 0; i < 5; i++)
            (await SyncOnceAsync()).Should().BeTrue();

        await using var db = await _factory.CreateDbContextAsync();

        (await db.SupplierBills.IgnoreQueryFilters().CountAsync(b => b.LabOrderId == _orderId))
            .Should().Be(1);
        (await db.LabPayables.IgnoreQueryFilters().CountAsync(p => p.LabOrderId == _orderId))
            .Should().Be(1);
    }
}
