using System.Collections.Concurrent;

namespace AqlanDentalPro.API.Configuration;

/// <summary>
/// QA4-07: in-memory journal of startup DB-hotfix failures and pipeline
/// lifecycle. The hotfixes swallow their exceptions by design (non-fatal
/// boot), which historically left the ONLY evidence of a failing ALTER TABLE
/// inside Railway's log stream — invisible to remote diagnosis. Live example:
/// the production DB kept missing Contracts."Currency" (and friends) across
/// multiple boots of an image whose hotfixes should have added them, with no
/// way to see WHY from the outside. This journal is surfaced through the
/// admin-only diagnostic endpoint GET /api/finance-v3/diagnostic/startup-hotfixes.
/// </summary>
public static class StartupHotfixJournal
{
    public sealed record Entry(DateTime AtUtc, string Step, string ExceptionType, string Message);

    private const int MaxEntries = 100;
    private const int MaxMessageLength = 300;

    private static readonly ConcurrentQueue<Entry> Failures = new();

    public static DateTime? PipelineStartedAtUtc { get; private set; }
    public static DateTime? PipelineCompletedAtUtc { get; private set; }
    public static bool PipelineFaulted { get; private set; }
    public static bool BootBudgetExceeded { get; private set; }

    public static void MarkPipelineStarted()
    {
        PipelineStartedAtUtc = DateTime.UtcNow;
        PipelineCompletedAtUtc = null;
        PipelineFaulted = false;
        BootBudgetExceeded = false;
    }

    public static void MarkPipelineCompleted(bool faulted = false)
    {
        PipelineCompletedAtUtc = DateTime.UtcNow;
        PipelineFaulted = faulted;
    }

    public static void MarkBootBudgetExceeded() => BootBudgetExceeded = true;

    /// <summary>
    /// Records a swallowed hotfix failure. Only the exception type and the
    /// first line of the base exception's message are kept (e.g. Postgres
    /// "42501: must be owner of table ..." / "55P03: lock timeout") — enough
    /// to diagnose, without dumping stack traces or connection details.
    /// </summary>
    public static void RecordFailure(string step, Exception ex)
    {
        // Full unwrap (AggregateException.GetBaseException stops at the first
        // non-aggregate inner) — the innermost exception carries the SQL error.
        var baseEx = ex;
        while (baseEx.InnerException is not null) baseEx = baseEx.InnerException;
        var firstLine = (baseEx.Message ?? string.Empty).Split('\n')[0].Trim();
        if (firstLine.Length > MaxMessageLength)
            firstLine = firstLine[..MaxMessageLength];

        Failures.Enqueue(new Entry(DateTime.UtcNow, step, baseEx.GetType().Name, firstLine));
        while (Failures.Count > MaxEntries && Failures.TryDequeue(out _)) { }
    }

    public static IReadOnlyList<Entry> Snapshot() => Failures.ToArray();

    /// <summary>Test hook — the journal is process-global static state.</summary>
    internal static void ResetForTests()
    {
        while (Failures.TryDequeue(out _)) { }
        PipelineStartedAtUtc = null;
        PipelineCompletedAtUtc = null;
        PipelineFaulted = false;
        BootBudgetExceeded = false;
    }
}
