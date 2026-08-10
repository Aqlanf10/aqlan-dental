using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Infrastructure;

/// <summary>
/// CORE-F-002, enforced instead of merely documented.
/// <para>
/// A brand-new database in this repository is not built by replaying the migration chain. The
/// chain is frozen and partly unattributed, so <c>StartupDatabaseMaintenance</c> builds the
/// schema from <c>GenerateCreateScript()</c> over the EF model and then stamps the migrations as
/// applied. That works for anything EF can express — tables, columns, indexes, check
/// constraints — and silently loses everything it cannot: PL/pgSQL functions and triggers.
/// </para>
/// <para>
/// The consequence is not hypothetical. It has now happened three times:
/// </para>
/// <list type="bullet">
///   <item>the journal balance and closed-period triggers were missing from every fresh
///   database, so it would accept an unbalanced entry or a posting backdated into a closed
///   period (found and fixed in PR #812);</item>
///   <item><c>JournalEntryNumberSequences</c> was missing entirely, so a fresh production
///   database could not post any journal entry at all (same PR);</item>
///   <item>the commission allocation guards were missing, so nothing at the database level
///   stopped a commission payment being allocated beyond its own amount.</item>
/// </list>
/// <para>
/// Each was invisible because the startup DDL runs inside a catch that logs a warning and
/// continues. This test is the visibility: it fails the build the moment a migration creates a
/// function or trigger that a fresh database would never get.
/// </para>
/// <para>
/// It compares NAMES, not bodies. A guard whose implementation drifts between the migration and
/// the startup copy would still pass here — that is a real limit, and worth stating rather than
/// implying a stronger guarantee than the test provides.
/// </para>
/// </summary>
public sealed class MigrationDdlReachesFreshDatabaseTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the test must be able to locate the backend source tree");
        return dir!.FullName;
    }

    private static string MigrationsText()
    {
        var dir = Path.Combine(RepoRoot(), "src", "AqlanDentalPro.Infrastructure", "Data", "Migrations");
        var files = Directory.GetFiles(dir, "*.cs")
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal)
                     && !f.EndsWith("ModelSnapshot.cs", StringComparison.Ordinal));
        return string.Join("\n", files.Select(File.ReadAllText));
    }

    private static string StartupDdlText() =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.API", "Configuration", "StartupDatabaseMaintenance.cs"));

    /// <summary>
    /// Names created by a migration. Only CREATE is matched — a DROP in a Down() method names
    /// the same object and would otherwise look like a creation.
    /// </summary>
    private static HashSet<string> Created(string text, string pattern) =>
        Regex.Matches(text, pattern, RegexOptions.IgnoreCase)
            .Select(m => m.Groups["name"].Value.Trim('"'))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Objects a fresh database is deliberately allowed to lack, with the reason.
    /// <para>
    /// An exemption list is honest where a weaker regex would not be: every entry has to be
    /// justified in writing, and anything new still fails.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> KnownUnreachable = new(StringComparer.Ordinal)
    {
        ["prevent_closed_period_posting"] =
            "Superseded by prevent_closed_period_journal_entry_change in migration " +
            "20260802010000_HardenJournalLedger, which rebinds journal_entries_closed_period_guard " +
            "to the new function but never drops the old one. Upgraded databases therefore keep an " +
            "orphan function that nothing calls; a fresh database simply never creates it. The two " +
            "schemas differ, but only by dead code, so recreating it at startup would spread the " +
            "leftover rather than fix anything.",
    };

    private const string FunctionPattern =
        @"CREATE\s+(OR\s+REPLACE\s+)?FUNCTION\s+(?<name>""?[A-Za-z_][A-Za-z0-9_]*""?)\s*\(";

    private const string TriggerPattern =
        @"CREATE\s+(CONSTRAINT\s+)?TRIGGER\s+(?<name>""?[A-Za-z_][A-Za-z0-9_]*""?)";

    [Fact]
    public void EveryFunctionAMigrationCreatesIsAlsoCreatedAtStartup()
    {
        var inMigrations = Created(MigrationsText(), FunctionPattern);
        var inStartup = Created(StartupDdlText(), FunctionPattern);

        inMigrations.Should().NotBeEmpty(
            "if migrations stop declaring functions this test would pass while proving nothing");

        var unreachable = inMigrations.Except(inStartup)
            .Where(name => !KnownUnreachable.ContainsKey(name))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        unreachable.Should().BeEmpty(
            "a function created only by a migration never reaches a fresh database, because a " +
            "fresh database is built from the EF model and EF cannot express PL/pgSQL. Add it to " +
            "StartupDatabaseMaintenance as an idempotent hotfix. Unreachable: {0}",
            string.Join(", ", unreachable));
    }

    [Fact]
    public void EveryTriggerAMigrationCreatesIsAlsoCreatedAtStartup()
    {
        var inMigrations = Created(MigrationsText(), TriggerPattern);
        var inStartup = Created(StartupDdlText(), TriggerPattern);

        inMigrations.Should().NotBeEmpty();

        var unreachable = inMigrations.Except(inStartup)
            .Where(name => !KnownUnreachable.ContainsKey(name))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();

        unreachable.Should().BeEmpty(
            "a trigger created only by a migration never reaches a fresh database. Unreachable: {0}",
            string.Join(", ", unreachable));
    }

    [Fact]
    public void EveryExemptionIsStillNecessary()
    {
        // An allowlist nobody prunes becomes a place to hide failures. If an exempted object
        // has since been added to the startup DDL, the entry must go rather than sit there
        // quietly excusing something that no longer needs excusing.
        var migrations = MigrationsText();
        var startup = StartupDdlText();
        var declared = Created(migrations, FunctionPattern).Concat(Created(migrations, TriggerPattern)).ToHashSet(StringComparer.Ordinal);
        var reachable = Created(startup, FunctionPattern).Concat(Created(startup, TriggerPattern)).ToHashSet(StringComparer.Ordinal);

        foreach (var (name, reason) in KnownUnreachable)
        {
            declared.Should().Contain(name,
                "exemption '{0}' names an object no migration creates any more — delete the entry", name);
            reachable.Should().NotContain(name,
                "'{0}' is now created at startup, so the exemption is obsolete — delete it. Reason given was: {1}",
                name, reason);
        }
    }

    [Fact]
    public void TheStartupDdlNeverUsesABraceQuantifierInRawSql()
    {
        // ExecuteSqlRawAsync puts the statement through String.Format, so a regex quantifier
        // like [0-9]{8} is read as a format placeholder and throws before PostgreSQL sees the
        // SQL. That is precisely how the fresh-database bootstrap failed for months: the
        // exception was swallowed by the surrounding catch and the database was left empty.
        var startup = StartupDdlText();

        var offenders = Regex.Matches(startup, @"\[0-9\]\{\d+\}")
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "write the quantifier out digit by digit instead. Offending patterns: {0}",
            string.Join(", ", offenders));
    }
}
