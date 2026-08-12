using System.Text.RegularExpressions;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace AqlanDentalPro.IntegrationTests;

/// <summary>
/// Every <c>DO $$ ... $$</c> block in the startup DDL must be valid PL/pgSQL.
/// <para>
/// PL/pgSQL parses a DO block in full before running any of it, so one syntax error anywhere
/// inside means <b>nothing</b> in that block executes. Combined with the startup path's habit
/// of catching, logging a warning and continuing, the result is a repair routine that appears
/// to be in place and has in fact never run once.
/// </para>
/// <para>
/// That is not hypothetical. The migration-history reconciliation — roughly 180 lines of
/// DELETE and INSERT statements whose entire purpose is to repair <c>__EFMigrationsHistory</c>
/// before <c>MigrateAsync</c> — carried two <c>IF NOT EXISTS (...)</c> statements with no
/// <c>THEN</c> and no <c>END IF</c>. PostgreSQL rejected the block with
/// <c>42601 missing "THEN"</c> on every single boot, so none of the reconciliation ever
/// happened, and the only trace was a warning line that predicted the consequence it could
/// not prevent.
/// </para>
/// <para>
/// Only syntax errors fail this test. A block that parses but then complains about a missing
/// table or a data conflict is a runtime concern and depends on which database it meets;
/// syntax is absolute, and syntax is what silently disables an entire block.
/// </para>
/// </summary>
public class StartupDdlSyntaxTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public StartupDdlSyntaxTests(TestWebAppFactory factory) => _factory = factory;

    /// <summary>PostgreSQL's syntax_error class.</summary>
    private const string SyntaxErrorSqlState = "42601";

    private static string StartupSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the test must be able to find the backend source tree");

        return File.ReadAllText(Path.Combine(
            dir!.FullName, "src", "AqlanDentalPro.API", "Configuration", "StartupDatabaseMaintenance.cs"));
    }

    /// <summary>
    /// Pulls out each <c>DO $$ BEGIN ... END $$</c> block.
    /// <para>
    /// The BEGIN is required, and that is not decoration: this file's C# comments contain
    /// prose like "DO $$ ... $$" when describing the SQL, and a regex without it happily
    /// matched those fragments and reported syntax errors in text that is not SQL at all.
    /// </para>
    /// </summary>
    private static List<string> DoBlocks(string source) =>
        Regex.Matches(source, @"DO \$\$\s*BEGIN\b.*?END \$\$", RegexOptions.Singleline)
            .Select(m => m.Value)
            .ToList();

    [Fact]
    public async Task EveryDoBlockInTheStartupDdlParses()
    {
        var blocks = DoBlocks(StartupSource());

        blocks.Should().NotBeEmpty(
            "the startup DDL is full of DO blocks — finding none means the extraction broke and " +
            "this test would pass while checking nothing");

        await using var conn = new NpgsqlConnection(_factory.ConnectionString);
        await conn.OpenAsync();

        var syntaxFailures = new List<string>();

        foreach (var block in blocks)
        {
            // Rolled back so a block that DOES parse cannot mutate the shared test database —
            // several of these delete migration-history rows.
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = block;
                await cmd.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex) when (ex.SqlState == SyntaxErrorSqlState)
            {
                var firstLine = block.Split('\n').FirstOrDefault()?.Trim() ?? "?";
                syntaxFailures.Add($"{ex.MessageText} — block starting: {firstLine}");
            }
            catch (PostgresException)
            {
                // Parsed fine, then objected to something about this particular database.
                // Not what this test is about.
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }

        syntaxFailures.Should().BeEmpty(
            "a DO block that does not parse is a block that never executes — and the startup " +
            "path swallows the error, so nothing else will tell you. Failures: {0}",
            string.Join(" | ", syntaxFailures));
    }
}
