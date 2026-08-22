using System.Text.RegularExpressions;
using AqlanDentalPro.API.Authorization;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Authorization;

/// <summary>
/// GOLIVE-PERM-001, step 1 — the destructive switches in the roles screen must be real.
///
/// <para>
/// The settings screen renders a «حذف» switch for `appointments` and `visits`, and until now
/// nothing on the server read either one. Walked against a running stack as Reception, with
/// `appointments.delete` off, DELETE /api/appointments/{id} returned 200 and the appointment
/// was gone. The switch was decoration.
/// </para>
///
/// <para>
/// Two things are asserted here, because either alone would pass while the system stayed
/// broken: that <see cref="PermissionGuard"/> answers correctly for these resources, and
/// that the destructive endpoints actually consult it. A guard nobody calls is what the
/// original defect was.
/// </para>
/// </summary>
public class DestructiveActionPermissionTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "AqlanDentalPro.API")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"destructive-perm-{Guid.NewGuid()}")
            .Options);

    private static ICurrentUserService AsRole(UserRole role)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(u => u.Role).Returns(role);
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        return user.Object;
    }

    private static async Task SeedAsync(AppDbContext db, string role, string resource, bool canDelete)
    {
        db.RolePermissions.Add(new RolePermission
        {
            Id = Guid.NewGuid(),
            Role = role,
            Resource = resource,
            CanView = true,
            CanCreate = true,
            CanEdit = true,
            CanDelete = canDelete,
        });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData("appointments")]
    [InlineData("visits")]
    public async Task A_role_with_the_delete_switch_off_is_refused(string resource)
    {
        using var db = CreateDb();
        await SeedAsync(db, nameof(UserRole.Reception), resource, canDelete: false);

        var allowed = await PermissionGuard.HasAsync(db, AsRole(UserRole.Reception), resource, "delete");

        allowed.Should().BeFalse($"the owner switched «حذف» off for {resource}");
    }

    [Theory]
    [InlineData("appointments")]
    [InlineData("visits")]
    public async Task A_role_with_the_delete_switch_on_is_allowed(string resource)
    {
        using var db = CreateDb();
        await SeedAsync(db, nameof(UserRole.Reception), resource, canDelete: true);

        var allowed = await PermissionGuard.HasAsync(db, AsRole(UserRole.Reception), resource, "delete");

        allowed.Should().BeTrue(
            $"turning «حذف» on for {resource} must actually grant it — a switch that only ever " +
            "denies is no more honest than one that only ever permits");
    }

    /// <summary>
    /// The queue is the busiest screen in the clinic, so it was the first slice enforced.
    /// Every state transition must read a switch — one unguarded endpoint is a way around
    /// all the others, since call/start/enter-room/complete reach the same states.
    /// </summary>
    [Fact]
    public void Every_queue_state_transition_reads_a_switch()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "src", "AqlanDentalPro.API", "Controllers", "ClinicQueueController.cs"));

        string[] mutating =
        [
            "[HttpPost]", "[HttpPost(\"arrive/{id:guid}\")]", "[HttpPost(\"reorder\")]",
            "[HttpPost(\"{id:guid}/call\")]", "[HttpPost(\"{id:guid}/recall\")]",
            "[HttpPost(\"{id:guid}/start\")]", "[HttpPost(\"{id:guid}/enter-room\")]",
            "[HttpPost(\"{id:guid}/complete\")]", "[HttpPost(\"{id:guid}/no-show\")]",
            "[HttpPost(\"{id:guid}/notify\")]", "[HttpPost(\"{id:guid}/cancel\")]",
            "[HttpPatch(\"{id:guid}/priority\")]", "[HttpPatch(\"{id:guid}/room\")]",
        ];

        var unguarded = new List<string>();
        foreach (var attr in mutating)
        {
            var at = source.IndexOf("    " + attr + "\n", StringComparison.Ordinal);
            at.Should().BeGreaterThan(-1, $"{attr} must exist — this test is worthless if the route moved");

            var next = source.IndexOf("    [Http", at + 10, StringComparison.Ordinal);
            var body = next > at ? source[at..next] : source[at..];
            if (!body.Contains("CanAsync(", StringComparison.Ordinal)) unguarded.Add(attr);
        }

        unguarded.Should().BeEmpty("an unguarded transition is a way around every guarded one");
    }

    /// <summary>
    /// The guard existing is not the fix; the endpoint calling it is. This reads the delete
    /// method out of each controller and requires the check to sit before any mutation.
    /// </summary>
    [Theory]
    [InlineData("AppointmentsController.cs", "appointments")]
    [InlineData("VisitsController.cs", "visits")]
    public void The_delete_endpoint_consults_the_permission_guard(string file, string resource)
    {
        var source = File.ReadAllText(
            Path.Combine(RepoRoot(), "src", "AqlanDentalPro.API", "Controllers", file));

        source.Should().Contain($"PermissionGuard.HasAsync(db, currentUser, \"{resource}\"",
            $"{file} must resolve its permissions against the {resource} resource");

        // Slice from the delete attribute to the next endpoint, rather than pattern-matching
        // a method body — brace matching by regex is how these tests start lying.
        var attr = source.IndexOf("[HttpDelete(\"{id:guid}\")]", StringComparison.Ordinal);
        attr.Should().BeGreaterThan(-1, $"the delete endpoint must be findable in {file}");

        var next = source.IndexOf("    [Http", attr + 10, StringComparison.Ordinal);
        var body = next > attr ? source[attr..next] : source[attr..];

        var check = body.IndexOf("CanAsync(\"delete\")", StringComparison.Ordinal);
        check.Should().BeGreaterThan(-1,
            $"the delete endpoint in {file} must check the switch, not merely have a guard available");

        // A check that runs after the row is already soft-deleted would still "call the guard".
        var save = body.IndexOf("SaveChangesAsync", StringComparison.Ordinal);
        if (save > -1)
            check.Should().BeLessThan(save, $"the check in {file} must precede the mutation");
    }
}
