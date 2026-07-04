using AqlanDentalPro.API.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// QA4-02: the startup DB maintenance boot budget. Boot must never wait on
/// the hotfix pipeline longer than the budget — otherwise a lock pileup on
/// production (old instance still serving while ALTER TABLE statements queue)
/// pushes boot past Railway's 120s health check and every deploy fails,
/// leaving the old image running forever (the documented deploy stall).
/// </summary>
public class StartupBootBudgetTests
{
    private static IConfiguration ConfigWith(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build();

    // ── GetBootBudget ──────────────────────────────────────────────────

    [Fact]
    public void GetBootBudget_Default_Is90Seconds_InsideRailwayHealthCheckWindow()
    {
        var budget = StartupDatabaseMaintenance.GetBootBudget(ConfigWith());

        Assert.Equal(TimeSpan.FromSeconds(90), budget);
        // Guard the invariant that motivated the fix: default budget must sit
        // inside the 120s healthcheckTimeout configured in backend/railway.toml.
        Assert.True(budget < TimeSpan.FromSeconds(120));
    }

    [Fact]
    public void GetBootBudget_ReadsConfiguredSeconds()
    {
        var budget = StartupDatabaseMaintenance.GetBootBudget(
            ConfigWith(("STARTUP_MAINTENANCE_BOOT_BUDGET_SECONDS", "30")));

        Assert.Equal(TimeSpan.FromSeconds(30), budget);
    }

    [Fact]
    public void GetBootBudget_ZeroOrNegative_DisablesTheBudget()
    {
        Assert.Equal(Timeout.InfiniteTimeSpan, StartupDatabaseMaintenance.GetBootBudget(
            ConfigWith(("STARTUP_MAINTENANCE_BOOT_BUDGET_SECONDS", "0"))));
        Assert.Equal(Timeout.InfiniteTimeSpan, StartupDatabaseMaintenance.GetBootBudget(
            ConfigWith(("STARTUP_MAINTENANCE_BOOT_BUDGET_SECONDS", "-5"))));
    }

    [Fact]
    public void GetBootBudget_Garbage_FallsBackToDefault()
    {
        var budget = StartupDatabaseMaintenance.GetBootBudget(
            ConfigWith(("STARTUP_MAINTENANCE_BOOT_BUDGET_SECONDS", "not-a-number")));

        Assert.Equal(TimeSpan.FromSeconds(90), budget);
    }

    // ── WaitWithBootBudgetAsync ────────────────────────────────────────

    [Fact]
    public async Task Wait_PipelineFinishesInTime_ReturnsTrue()
    {
        var result = await StartupDatabaseMaintenance.WaitWithBootBudgetAsync(
            Task.CompletedTask, TimeSpan.FromSeconds(5));

        Assert.True(result);
    }

    [Fact]
    public async Task Wait_PipelineExceedsBudget_ReturnsFalse_WithoutThrowing()
    {
        var never = new TaskCompletionSource();

        var result = await StartupDatabaseMaintenance.WaitWithBootBudgetAsync(
            never.Task, TimeSpan.FromMilliseconds(50));

        Assert.False(result);
        never.SetResult(); // let the pipeline finish so nothing dangles
    }

    [Fact]
    public async Task Wait_PipelineFaultsInTime_PropagatesTheException()
    {
        // A crash during a timely bootstrap must still fail boot loudly —
        // the budget must not swallow real startup failures.
        var faulted = Task.FromException(new InvalidOperationException("bootstrap crashed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartupDatabaseMaintenance.WaitWithBootBudgetAsync(faulted, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task Wait_PipelineFaultsAfterBudget_DoesNotThrowFromTheWait()
    {
        // Once the budget expired the caller owns the pipeline: the wait
        // itself must have already returned false without observing it.
        var tcs = new TaskCompletionSource();

        var result = await StartupDatabaseMaintenance.WaitWithBootBudgetAsync(
            tcs.Task, TimeSpan.FromMilliseconds(50));

        Assert.False(result);
        tcs.SetException(new InvalidOperationException("late failure"));
        // Observe it here so the test run has no unobserved task exception.
        await Assert.ThrowsAsync<InvalidOperationException>(() => tcs.Task);
    }

    [Fact]
    public async Task Wait_InfiniteBudget_WaitsForThePipeline()
    {
        var tcs = new TaskCompletionSource();
        var wait = StartupDatabaseMaintenance.WaitWithBootBudgetAsync(tcs.Task, Timeout.InfiniteTimeSpan);

        Assert.False(wait.IsCompleted);
        tcs.SetResult();
        Assert.True(await wait);
    }
}
