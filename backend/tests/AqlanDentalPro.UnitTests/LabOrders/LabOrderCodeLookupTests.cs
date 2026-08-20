using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using LabOrderEntity = AqlanDentalPro.Domain.Entities.LabOrder;
using PatientEntity = AqlanDentalPro.Domain.Entities.Patient;

namespace AqlanDentalPro.UnitTests.LabOrders;

/// <summary>
/// LABINV-REQ-008 — resolving a scanned order code to an order.
///
/// <para>
/// A scanner is an enumeration surface. Lab order numbers are sequential, so anyone holding
/// one printed slip can guess the rest; if the lookup answered differently for "does not
/// exist" and "exists but is not yours", that guessing turns into a map of the clinic's
/// order book, and of which branches are busy.
/// </para>
///
/// <para>
/// The defence is that the lookup resolves through the same branch-scoped query as every
/// other lab read, so an out-of-scope code returns null and the controller turns null into
/// the single shared 404. These tests pin the scoping half — the half that would silently
/// stop working if someone "optimised" the lookup into a direct <c>_db.LabOrders</c> query.
/// </para>
/// </summary>
public class LabOrderCodeLookupTests
{
    private static AppDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"LabOrderCodeLookupTests_{Guid.NewGuid()}")
            .Options);

    /// <param name="branchId">null grants global access (Admin); a value scopes to that branch.</param>
    private static LabOrderQueryService CreateService(AppDbContext db, Guid? branchId = null)
    {
        var branchScope = new Mock<IBranchResourceScope>();
        branchScope.SetupGet(s => s.HasGlobalAccess).Returns(branchId is null);
        branchScope.SetupGet(s => s.EffectiveBranchId).Returns(branchId);
        return new LabOrderQueryService(db, branchScope.Object, NullLogger<LabOrderQueryService>.Instance);
    }

    private static async Task<(Guid OrderId, Guid BranchId)> SeedOrderAsync(
        AppDbContext db, string orderNumber, Guid? branchId = null)
    {
        var branch = branchId ?? Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        db.Patients.Add(new PatientEntity
        {
            Id = patientId,
            FirstName = "أحمد",
            LastName = "محمد",
            PatientNumber = "P-001",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Male,
            BranchId = branch,
        });
        db.LabOrders.Add(new LabOrderEntity
        {
            Id = orderId,
            PatientId = patientId,
            OrderNumber = orderNumber,
            Status = "sent",
            BranchId = branch,
        });
        await db.SaveChangesAsync();
        return (orderId, branch);
    }

    [Fact]
    public async Task Resolves_A_Known_Code_To_Its_Order()
    {
        using var db = CreateDbContext();
        var (orderId, _) = await SeedOrderAsync(db, "LAB-2026-003");

        var found = await CreateService(db).FindIdByOrderNumberAsync("LAB-2026-003");

        found.Should().Be(orderId);
    }

    /// <summary>A camera decode and a barcode wedge do not agree on case or padding.</summary>
    [Theory]
    [InlineData("lab-2026-003")]
    [InlineData("LAB-2026-003 ")]
    [InlineData("  LAB-2026-003")]
    public async Task Tolerates_Case_And_Whitespace_From_A_Decode(string scanned)
    {
        using var db = CreateDbContext();
        var (orderId, _) = await SeedOrderAsync(db, "LAB-2026-003");

        var found = await CreateService(db).FindIdByOrderNumberAsync(scanned);

        found.Should().Be(orderId);
    }

    [Fact]
    public async Task Returns_Null_For_A_Code_That_Matches_Nothing()
    {
        using var db = CreateDbContext();
        await SeedOrderAsync(db, "LAB-2026-003");

        var found = await CreateService(db).FindIdByOrderNumberAsync("LAB-9999-999");

        found.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Returns_Null_For_An_Empty_Code_Without_Querying(string code)
    {
        using var db = CreateDbContext();
        await SeedOrderAsync(db, "LAB-2026-003");

        (await CreateService(db).FindIdByOrderNumberAsync(code)).Should().BeNull();
    }

    /// <summary>
    /// The assertion that matters. A user scoped to branch A must not resolve branch B's
    /// slip — and must get exactly the same answer as for a code that does not exist.
    /// </summary>
    [Fact]
    public async Task Refuses_A_Code_Belonging_To_Another_Branch()
    {
        using var db = CreateDbContext();
        var otherBranch = Guid.NewGuid();
        await SeedOrderAsync(db, "LAB-2026-003", otherBranch);

        var scopedToDifferentBranch = CreateService(db, Guid.NewGuid());

        var existingButForeign = await scopedToDifferentBranch.FindIdByOrderNumberAsync("LAB-2026-003");
        var nonExistent = await scopedToDifferentBranch.FindIdByOrderNumberAsync("LAB-9999-999");

        existingButForeign.Should().BeNull();
        existingButForeign.Should().Be(nonExistent,
            "an existing foreign order and a non-existent one must be indistinguishable");
    }

    [Fact]
    public async Task Resolves_A_Code_Inside_The_Users_Own_Branch()
    {
        using var db = CreateDbContext();
        var branch = Guid.NewGuid();
        var (orderId, _) = await SeedOrderAsync(db, "LAB-2026-003", branch);

        var found = await CreateService(db, branch).FindIdByOrderNumberAsync("LAB-2026-003");

        found.Should().Be(orderId);
    }

    /// <summary>
    /// A user with no branch assigned resolves nothing, rather than falling through to
    /// every branch. This mirrors <c>ScopedOrders</c>' <c>Where(_ =&gt; false)</c> guard.
    /// </summary>
    [Fact]
    public async Task Resolves_Nothing_For_A_User_With_No_Branch()
    {
        using var db = CreateDbContext();
        await SeedOrderAsync(db, "LAB-2026-003");

        var branchScope = new Mock<IBranchResourceScope>();
        branchScope.SetupGet(s => s.HasGlobalAccess).Returns(false);
        branchScope.SetupGet(s => s.EffectiveBranchId).Returns((Guid?)null);
        var service = new LabOrderQueryService(db, branchScope.Object, NullLogger<LabOrderQueryService>.Instance);

        (await service.FindIdByOrderNumberAsync("LAB-2026-003")).Should().BeNull();
    }

    [Fact]
    public async Task Ignores_Orders_That_Carry_No_Number()
    {
        using var db = CreateDbContext();
        var patientId = Guid.NewGuid();
        db.Patients.Add(new PatientEntity
        {
            Id = patientId,
            FirstName = "أحمد",
            LastName = "محمد",
            PatientNumber = "P-002",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Male,
        });
        db.LabOrders.Add(new LabOrderEntity
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            OrderNumber = null,
            Status = "draft",
        });
        await db.SaveChangesAsync();

        (await CreateService(db).FindIdByOrderNumberAsync("")).Should().BeNull();
        (await CreateService(db).FindIdByOrderNumberAsync("LAB-2026-003")).Should().BeNull();
    }
}
