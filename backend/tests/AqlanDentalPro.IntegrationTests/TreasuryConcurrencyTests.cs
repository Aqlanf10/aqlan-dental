using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// FIN-CONCURRENCY (audit §5.1/§11): proves that <see cref="ITreasuryResolutionService"/>
/// applies treasury balance changes atomically at the database level, so concurrent
/// outflows/inflows on the SAME treasury cannot lose updates or fail on the xmin
/// concurrency token.
///
/// These run against a REAL PostgreSQL container (Testcontainers) because the fix relies
/// on a set-based <c>UPDATE Balance = Balance + delta</c> executing under a row-level lock —
/// behavior the EF InMemory provider (used by the unit-test suite) cannot reproduce.
///
/// With the previous read-modify-write (<c>treasury.Balance += delta; SaveChanges()</c>),
/// N parallel operations each load the same starting balance and the final value is wrong
/// (lost update) or the operation throws DbUpdateConcurrencyException (xmin). The set-based
/// UPDATE makes the read-and-write a single atomic statement, so the final balance is exact.
///
/// NOTE: the CI pipeline currently runs only the unit-test project, so this proof executes
/// locally / wherever Docker is available. Treat the production guarantee on Railway as
/// "verified by this test where Docker is present; needs runtime verification in the CI gate
/// until the IntegrationTests project is added to CI".
/// </summary>
public class TreasuryConcurrencyTests : IClassFixture<TestWebAppFactory>, IAsyncLifetime
{
    private readonly TestWebAppFactory _factory;
    private Guid _branchId;
    private Guid _treasuryId;

    private const decimal OpeningBalance = 100_000m;
    private const decimal OpAmount = 100m;
    private const int Operations = 20;

    public TreasuryConcurrencyTests(TestWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        await using var db = await _factory.CreateDbContextAsync();
        var branch = new Branch { Name = "فرع اختبار التزامن" };
        db.Branches.Add(branch);
        var treasury = new Treasury
        {
            Name = "درج كاشير اختبار التزامن",
            Type = TreasuryType.Vault,
            Balance = OpeningBalance,
            BranchId = branch.Id,
            IsActive = true,
        };
        db.Treasuries.Add(treasury);
        await db.SaveChangesAsync();

        _branchId = branch.Id;
        _treasuryId = treasury.Id;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentDecrements_OnSameTreasury_DoNotLoseUpdates()
    {
        // Fire N decrements concurrently, each through its own DI scope / DbContext /
        // service instance — exactly as N concurrent HTTP requests would.
        var tasks = Enumerable.Range(0, Operations).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITreasuryResolutionService>();
            await svc.DecrementTreasuryBalanceAsync(_branchId, "cash", OpAmount);
        });

        await Task.WhenAll(tasks);

        await using var db = await _factory.CreateDbContextAsync();
        var finalBalance = await db.Treasuries
            .Where(t => t.Id == _treasuryId)
            .Select(t => t.Balance)
            .SingleAsync();

        finalBalance.Should().Be(OpeningBalance - Operations * OpAmount,
            "each atomic UPDATE must apply exactly once with no lost updates under concurrency");
    }

    [Fact]
    public async Task ConcurrentMixedInflowOutflow_OnSameTreasury_NetsExactly()
    {
        // Interleave increments and decrements concurrently; the net change must be exact.
        var tasks = new List<Task>();
        for (var i = 0; i < Operations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ITreasuryResolutionService>();
                await svc.DecrementTreasuryBalanceAsync(_branchId, "cash", OpAmount);
            }));
            tasks.Add(Task.Run(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ITreasuryResolutionService>();
                await svc.IncrementTreasuryBalanceAsync(_branchId, "cash", OpAmount);
            }));
        }

        await Task.WhenAll(tasks);

        await using var db = await _factory.CreateDbContextAsync();
        var finalBalance = await db.Treasuries
            .Where(t => t.Id == _treasuryId)
            .Select(t => t.Balance)
            .SingleAsync();

        finalBalance.Should().Be(OpeningBalance,
            "equal concurrent inflows and outflows must net to the opening balance exactly");
    }

    [Fact]
    public async Task ConcurrentDecrements_WithBlockingDefault_NeverGoNegative()
    {
        // Block-by-default (Sprint 2): fire more concurrent outflows than the balance can cover.
        // The atomic conditional UPDATE must let exactly OpeningBalance/amount succeed and reject
        // the rest — the balance must never go below zero even under contention.
        const decimal amount = 10_000m;
        var attempts = (int)(OpeningBalance / amount) + 5; // 5 more than can be funded

        var outcomes = await Task.WhenAll(Enumerable.Range(0, attempts).Select(_ => Task.Run(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<ITreasuryResolutionService>();
            try
            {
                await svc.DecrementTreasuryBalanceAsync(_branchId, "cash", amount);
                return true; // succeeded
            }
            catch (ArgumentException)
            {
                return false; // blocked — insufficient balance
            }
        })));

        await using var db = await _factory.CreateDbContextAsync();
        var finalBalance = await db.Treasuries
            .Where(t => t.Id == _treasuryId)
            .Select(t => t.Balance)
            .SingleAsync();

        finalBalance.Should().BeGreaterThanOrEqualTo(0m, "the atomic block must never allow a negative balance");
        finalBalance.Should().Be(OpeningBalance - outcomes.Count(ok => ok) * amount,
            "the balance must reflect exactly the operations that were allowed");
        outcomes.Count(ok => ok).Should().Be((int)(OpeningBalance / amount),
            "exactly the affordable number of outflows may succeed; the rest are blocked");
    }
}
