using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// GOLIVE-PERM-001 step 2 — `contracts/permission-action-map.json` must describe the code.
///
/// <para>
/// The contract exists because the six CRUD actions do not describe daily operations. Calling,
/// recalling and rooming a patient are all POSTs to the queue, and none of them creates
/// anything; deriving the action from the HTTP verb would have put "who may call the next
/// patient" behind a switch labelled «إنشاء». So the mapping is a written decision, and this
/// keeps the decision and the code from drifting apart in either direction.
/// </para>
/// </summary>
public class PermissionActionContractTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "contracts", "permission-action-map.json")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the contract file must be findable from the test output directory");
        return dir!.FullName;
    }

    private sealed record Endpoint(string Route, string Verb, string Resource, string Action, bool Enforced);

    private static (List<Endpoint> Endpoints, string[] Vocabulary) LoadContract()
    {
        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "contracts", "permission-action-map.json")));

        var vocabulary = doc.RootElement.GetProperty("actionVocabulary")
            .EnumerateArray().Select(v => v.GetString()!).ToArray();

        var endpoints = doc.RootElement.GetProperty("endpoints").EnumerateArray()
            .Select(e => new Endpoint(
                e.GetProperty("route").GetString()!,
                e.GetProperty("verb").GetString()!,
                e.GetProperty("resource").GetString()!,
                e.GetProperty("action").GetString()!,
                e.GetProperty("enforced").GetBoolean()))
            .ToList();

        return (endpoints, vocabulary);
    }

    [Fact]
    public void Every_action_is_one_the_permission_model_can_actually_store()
    {
        var (endpoints, vocabulary) = LoadContract();

        endpoints.Should().NotBeEmpty("an empty contract would pass every other test here");

        // RolePermission has exactly six boolean columns; PermissionGuard returns false for
        // anything else. An action outside this list would silently deny forever.
        vocabulary.Should().BeEquivalentTo(["view", "create", "edit", "delete", "export", "approve"]);

        foreach (var e in endpoints)
            vocabulary.Should().Contain(e.Action,
                $"{e.Verb} {e.Route} maps to '{e.Action}', which PermissionGuard cannot evaluate");
    }

    /// <summary>
    /// The whole point of step 1: an endpoint marked enforced must really call the guard, and
    /// one marked not-yet must not silently have been switched on without the contract saying so.
    /// </summary>
    [Fact]
    public void Enforced_endpoints_call_the_guard_and_unenforced_ones_do_not_claim_to()
    {
        var (endpoints, _) = LoadContract();
        var controllers = Path.Combine(RepoRoot(), "backend", "src", "AqlanDentalPro.API", "Controllers");

        foreach (var e in endpoints.Where(x => x.Enforced))
        {
            var callers = Directory.EnumerateFiles(controllers, "*.cs")
                .Where(f => File.ReadAllText(f)
                    .Contains($"PermissionGuard.HasAsync(db, currentUser, \"{e.Resource}\"", StringComparison.Ordinal))
                .ToList();

            callers.Should().NotBeEmpty(
                $"{e.Verb} {e.Route} is marked enforced, so some controller must resolve " +
                $"'{e.Resource}' through PermissionGuard");
        }
    }

    /// <summary>
    /// Every endpoint the contract lists is now enforced. This started life as the opposite
    /// assertion — that unfinished work must stay recorded — and was flipped when the last
    /// slice landed. Keep it pointed at whichever direction is true: a contract that quietly
    /// agrees with any state checks nothing.
    /// </summary>
    [Fact]
    public void Every_listed_endpoint_is_enforced()
    {
        var (endpoints, _) = LoadContract();

        endpoints.Should().HaveCountGreaterThan(15,
            "a trimmed list would make the rest of this file vacuous");

        endpoints.Where(e => !e.Enforced).Should().BeEmpty(
            "adding an endpoint here without a guard leaves a switch the owner can see and " +
            "the server ignores — the defect this whole contract exists to prevent");
    }
}
