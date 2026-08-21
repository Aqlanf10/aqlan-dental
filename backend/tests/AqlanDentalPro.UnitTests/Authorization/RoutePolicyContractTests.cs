using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using AqlanDentalPro.API.Configuration;
using AqlanDentalPro.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// Phase 1 exit gate — the backend half of <c>contracts/route-policy-map.json</c>.
///
/// <para>
/// The gate for Phase 1 is "sidebar/guards/server policy agree". <c>CORE-P1-S3</c> made the
/// sidebar and route guards read one frontend manifest, and <c>CORE-P1-S4</c> pinned each
/// controller's policy. Neither checked the agreement, which is the gate itself — and the two
/// halves are written in different languages, so nothing could check it without a shared
/// artifact.
/// </para>
/// <para>
/// The contract JSON is that artifact. This file proves its backend half: that every role set
/// it lists is what the application's own DI container actually grants, and that every route's
/// named owner really enforces the policy claimed for it. The frontend proves the other half
/// against the same file, so neither side has to parse the other's language and neither can
/// drift without a test going red.
/// </para>
/// <para>
/// Role sets are derived by <b>asking the real authorization service</b>, one synthetic
/// principal per role, rather than by reading requirement objects. <c>StaffOnly</c> is an
/// assertion rather than a role requirement, so a reflection-based reading would have to
/// special-case it — and would then be checking the special case rather than the policy.
/// </para>
/// </summary>
public sealed class RoutePolicyContractTests
{
    private sealed record RouteEntry(string Path, string Owner, string Policy);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "contracts")))
            dir = dir.Parent;

        dir.Should().NotBeNull("the test must be able to locate the repository's contracts directory");
        return dir!.FullName;
    }

    private static JsonElement Contract()
    {
        var path = Path.Combine(RepoRoot(), "contracts", "route-policy-map.json");
        File.Exists(path).Should().BeTrue($"the shared contract must exist at {path}");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    private static IAuthorizationService AuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicies();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static async Task<List<string>> RolesGrantedAsync(IAuthorizationService auth, string policy)
    {
        var granted = new List<string>();
        foreach (var role in Enum.GetNames<UserRole>())
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "contract-test"), new Claim(ClaimTypes.Role, role)],
                "Test"));

            if ((await auth.AuthorizeAsync(user, resource: null, policy)).Succeeded) granted.Add(role);
        }
        return granted;
    }

    private static List<RouteEntry> Routes() =>
        Contract().GetProperty("routes").EnumerateArray()
            .Select(r => new RouteEntry(
                r.GetProperty("path").GetString()!,
                r.GetProperty("owner").GetString()!,
                r.GetProperty("policy").GetString()!))
            .ToList();

    /// <summary>
    /// The contract is only worth anything if its numbers are the application's numbers.
    /// </summary>
    [Fact]
    public async Task Every_policy_grants_exactly_the_roles_the_contract_claims()
    {
        var auth = AuthorizationService();
        var mismatches = new List<string>();

        foreach (var declared in Contract().GetProperty("policyRoles").EnumerateObject())
        {
            var expected = declared.Value.EnumerateArray().Select(v => v.GetString()!).OrderBy(r => r).ToList();
            var actual = (await RolesGrantedAsync(auth, declared.Name)).OrderBy(r => r).ToList();

            if (!expected.SequenceEqual(actual))
                mismatches.Add($"{declared.Name}: contract says [{string.Join(", ", expected)}] "
                             + $"but the container grants [{string.Join(", ", actual)}]");
        }

        mismatches.Should().BeEmpty(
            "a policy's role set changed without updating contracts/route-policy-map.json. The "
            + "frontend checks its route guards against that file, so leaving it stale would let "
            + "the sidebar and the server disagree again.");
    }

    /// <summary>
    /// A route entry naming a policy the owner does not enforce would let the frontend check
    /// itself against a rule the server never applies.
    /// </summary>
    [Fact]
    public void Every_route_owner_enforces_the_policy_the_contract_names()
    {
        var controllers = typeof(AqlanDentalPro.API.Controllers.PatientsController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t is { IsAbstract: false, IsPublic: true })
            .ToDictionary(t => t.Name, t => t.GetCustomAttribute<AuthorizeAttribute>(inherit: true)?.Policy);

        var wrong = new List<string>();

        foreach (var route in Routes())
        {
            if (!controllers.TryGetValue(route.Owner, out var actual))
            {
                wrong.Add($"{route.Path}: owner '{route.Owner}' is not an API controller");
                continue;
            }

            if (actual != route.Policy)
                wrong.Add($"{route.Path}: contract names '{route.Policy}' for {route.Owner}, "
                        + $"which enforces '{actual ?? "(anonymous)"}'");
        }

        wrong.Should().BeEmpty("the contract must describe the authorization the server really applies");
    }

    /// <summary>
    /// Every policy a route names must be one the contract prices, or the frontend has nothing
    /// to compare its roles against and would silently skip the check.
    /// </summary>
    [Fact]
    public void Every_policy_a_route_names_is_priced_in_the_contract()
    {
        var priced = Contract().GetProperty("policyRoles").EnumerateObject()
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        Routes().Select(r => r.Policy).Distinct()
            .Where(p => !priced.Contains(p))
            .Should().BeEmpty("an unpriced policy makes the frontend's half of this contract vacuous");
    }

    /// <summary>
    /// Guards this file against itself: a comparison that reports no mismatch because it never
    /// resolved a policy would pass forever.
    /// </summary>
    [Fact]
    public async Task The_role_probe_actually_distinguishes_policies()
    {
        var auth = AuthorizationService();

        var adminOnly = await RolesGrantedAsync(auth, "AdminOnly");
        var staffOnly = await RolesGrantedAsync(auth, "StaffOnly");

        adminOnly.Should().ContainSingle().Which.Should().Be("Admin");
        staffOnly.Should().Contain("Reception").And.NotContain("Patient");
        staffOnly.Count.Should().BeGreaterThan(adminOnly.Count,
            "if the probe returned the same answer for every policy it would be measuring nothing");
    }
}
