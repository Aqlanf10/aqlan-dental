using AqlanDentalPro.API.Configuration;
using Xunit;

namespace AqlanDentalPro.UnitTests.Services;

/// <summary>
/// QA4-07: the startup hotfix journal — swallowed DDL-hotfix failures must be
/// observable remotely (via the admin diagnostic endpoint), not only in the
/// host's log stream. The journal is process-global static state, so these
/// tests reset it and must not run in parallel with themselves (xUnit runs
/// tests in one class sequentially, which is enough).
/// </summary>
public class StartupHotfixJournalTests
{
    [Fact]
    public void RecordFailure_KeepsStepTypeAndFirstMessageLine()
    {
        StartupHotfixJournal.ResetForTests();

        StartupHotfixJournal.RecordFailure("MultiCurrency:Contracts.Currency",
            new InvalidOperationException("42501: must be owner of table \"Contracts\"\nDETAIL: something long"));

        var entry = Assert.Single(StartupHotfixJournal.Snapshot());
        Assert.Equal("MultiCurrency:Contracts.Currency", entry.Step);
        Assert.Equal(nameof(InvalidOperationException), entry.ExceptionType);
        Assert.Equal("42501: must be owner of table \"Contracts\"", entry.Message);
    }

    [Fact]
    public void RecordFailure_UnwrapsToTheBaseException()
    {
        StartupHotfixJournal.ResetForTests();

        var inner = new TimeoutException("57014: canceling statement due to statement timeout");
        StartupHotfixJournal.RecordFailure("VisitOrthoFields:Visits ortho fields",
            new AggregateException(new InvalidOperationException("outer", inner)));

        var entry = Assert.Single(StartupHotfixJournal.Snapshot());
        Assert.Equal(nameof(TimeoutException), entry.ExceptionType);
        Assert.StartsWith("57014", entry.Message);
    }

    [Fact]
    public void RecordFailure_TruncatesVeryLongMessages()
    {
        StartupHotfixJournal.ResetForTests();

        StartupHotfixJournal.RecordFailure("step", new Exception(new string('x', 1000)));

        Assert.Equal(300, Assert.Single(StartupHotfixJournal.Snapshot()).Message.Length);
    }

    [Fact]
    public void Journal_IsCappedAt100Entries()
    {
        StartupHotfixJournal.ResetForTests();

        for (var i = 0; i < 150; i++)
            StartupHotfixJournal.RecordFailure($"step-{i}", new Exception("boom"));

        var snapshot = StartupHotfixJournal.Snapshot();
        Assert.Equal(100, snapshot.Count);
        Assert.Equal("step-149", snapshot[^1].Step); // newest kept, oldest dropped
    }

    [Fact]
    public void PipelineLifecycle_TracksStartCompleteAndBudget()
    {
        StartupHotfixJournal.ResetForTests();
        Assert.Null(StartupHotfixJournal.PipelineStartedAtUtc);

        StartupHotfixJournal.MarkPipelineStarted();
        Assert.NotNull(StartupHotfixJournal.PipelineStartedAtUtc);
        Assert.Null(StartupHotfixJournal.PipelineCompletedAtUtc);
        Assert.False(StartupHotfixJournal.BootBudgetExceeded);

        StartupHotfixJournal.MarkBootBudgetExceeded();
        StartupHotfixJournal.MarkPipelineCompleted();

        Assert.True(StartupHotfixJournal.BootBudgetExceeded);
        Assert.NotNull(StartupHotfixJournal.PipelineCompletedAtUtc);
        Assert.False(StartupHotfixJournal.PipelineFaulted);
    }

    [Fact]
    public void PipelineLifecycle_FaultedCompletionIsRecorded()
    {
        StartupHotfixJournal.ResetForTests();
        StartupHotfixJournal.MarkPipelineStarted();
        StartupHotfixJournal.MarkPipelineCompleted(faulted: true);

        Assert.True(StartupHotfixJournal.PipelineFaulted);
    }
}
