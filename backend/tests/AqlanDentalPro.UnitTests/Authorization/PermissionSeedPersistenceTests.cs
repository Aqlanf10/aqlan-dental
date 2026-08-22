using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// GOLIVE-PERM-001 — a permission the owner changes must survive the next deploy.
///
/// <para>
/// The role matrix in <c>DbSeeder.SeedPermissionsAsync</c> used to re-assert the code defaults
/// over every existing row on every startup. Anything changed in Settings → Roles was reverted
/// the next time Railway restarted the API. That was merely untidy while nothing read these
/// switches; now that they decide requests, a switch that silently resets itself is worse than
/// one that never worked — the owner grants a permission, the next deploy takes it back, and
/// nothing anywhere says so.
/// </para>
///
/// <para>
/// Deliberate default changes reach existing databases through the one-time backfill in
/// StartupDatabaseMaintenance instead, which only ever grants.
/// </para>
/// </summary>
public class PermissionSeedPersistenceTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static string SeederSource() => File.ReadAllText(Path.Combine(
        RepoRoot(), "src", "AqlanDentalPro.Infrastructure", "Data", "Seed", "DbSeeder.cs"));

    [Fact]
    public void The_role_matrix_never_overwrites_a_permission_that_already_exists()
    {
        var source = SeederSource();
        source.Length.Should().BeGreaterThan(10_000, "the seeder must be readable for this to mean anything");

        // The exact assignments that used to clobber the owner's choices.
        foreach (var write in new[]
                 {
                     "existing.CanView = view;",
                     "existing.CanCreate = create;",
                     "existing.CanEdit = edit;",
                     "existing.CanDelete = delete;",
                 })
        {
            source.Should().NotContain(write,
                "re-asserting code defaults over an existing row reverts what the owner set in " +
                "Settings → Roles on the next restart");
        }
    }

    [Fact]
    public void The_backfill_only_grants_and_runs_once()
    {
        var startup = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.API", "Configuration", "StartupDatabaseMaintenance.cs"));

        startup.Should().Contain("permissions.golive_operational_backfill_v1",
            "the backfill must be guarded by a marker so a restart does not re-apply it");
        startup.Should().Contain("EnsureOperationalPermissionBackfillAsync(app);",
            "declaring the method is not the same as calling it");

        // Grants only: the abilities the owner chose to withdraw are withdrawn by enforcing a
        // switch that is already off, never by writing false over somebody's row.
        var body = startup[startup.IndexOf("EnsureOperationalPermissionBackfillAsync(WebApplication app)",
                                           StringComparison.Ordinal)..];
        body = body[..body.IndexOf("\n    private static", StringComparison.Ordinal)];

        body.Should().NotContain("= false;", "the backfill must never revoke a permission");
        body.Should().Contain("row.CanEdit = true;");
    }
}
