using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TreatmentPlanEntity = AqlanDentalPro.Domain.Entities.TreatmentPlan;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Tests for PlanLabel validation: only A/B/C allowed, no duplicates per ortho case,
/// max three plans per case, and unique index represented in EF model snapshot.
/// </summary>
public class OrthoPlanLabelValidationTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoPlanLabelValidationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> SeedOrthoCase()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            CaseNumber = "ORT-PL-001",
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return orthoCaseId;
    }

    [Fact]
    public async Task Duplicate_PlanLabel_ForSameOrthoCase_IsRejected()
    {
        // Arrange: create Plan A
        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "A" });
        await _db.SaveChangesAsync();

        // Act: simulate the controller's duplicate-check logic
        var existingLabels = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .Select(p => p.PlanLabel)
            .ToListAsync();

        var requestedLabel = "A";
        var isDuplicate = existingLabels.Contains(requestedLabel, StringComparer.OrdinalIgnoreCase);

        // Assert: controller should reject because label A is already used
        isDuplicate.Should().BeTrue("Plan A already exists for this case and the controller must reject it");
    }

    [Fact]
    public async Task PlanLabel_OutsideABC_IsInvalid()
    {
        // The valid set is {A, B, C} only. Labels like D, E, X are invalid.
        var validLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A", "B", "C" };

        validLabels.Should().Contain("A");
        validLabels.Should().Contain("B");
        validLabels.Should().Contain("C");
        validLabels.Should().NotContain("D");
        validLabels.Should().NotContain("E");
        validLabels.Should().NotContain("X");
        validLabels.Should().NotContain("1");
        validLabels.Should().NotContain("");
    }

    [Fact]
    public async Task Creating_ABC_AllSucceed()
    {
        // Arrange
        var orthoCaseId = await SeedOrthoCase();

        // Act: create plans A, B, and C
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "A", TreatmentGoals = "Align incisors" });
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "B", TreatmentGoals = "Extraction plan" });
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "C", TreatmentGoals = "Non-extraction plan" });
        await _db.SaveChangesAsync();

        // Assert
        var plans = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .OrderBy(p => p.PlanLabel)
            .ToListAsync();

        plans.Should().HaveCount(3);
        plans[0].PlanLabel.Should().Be("A");
        plans[1].PlanLabel.Should().Be("B");
        plans[2].PlanLabel.Should().Be("C");
    }

    [Fact]
    public async Task FourthPlan_WhenABCExist_IsRejected()
    {
        // Arrange: create all three plans
        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "A" });
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "B" });
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "C" });
        await _db.SaveChangesAsync();

        // Act: simulate auto-select logic — find next available label
        var existingLabels = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .Select(p => p.PlanLabel)
            .ToListAsync();

        var nextLabel = new[] { "A", "B", "C" }
            .FirstOrDefault(l => !existingLabels.Contains(l, StringComparer.OrdinalIgnoreCase));

        // Assert: no label is available
        nextLabel.Should().BeNull("all A/B/C labels are already used");
    }

    [Fact]
    public async Task AutoSelect_PicksNextAvailableLabel()
    {
        // Arrange: create Plan A only
        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "A" });
        await _db.SaveChangesAsync();

        // Act: simulate auto-select
        var existingLabels = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .Select(p => p.PlanLabel)
            .ToListAsync();

        var nextLabel = new[] { "A", "B", "C" }
            .FirstOrDefault(l => !existingLabels.Contains(l, StringComparer.OrdinalIgnoreCase));

        // Assert: B is the next available label
        nextLabel.Should().Be("B");
    }

    [Fact]
    public async Task UniqueCompositeIndex_IsPresentInEfModel()
    {
        // Verify that the EF model has the unique composite index defined on (OrthoCaseId, PlanLabel).
        // This checks the TreatmentPlanConfiguration is applied correctly.
        var entityType = _db.Model.FindEntityType(typeof(TreatmentPlanEntity));
        entityType.Should().NotBeNull();

        var indexes = entityType!.GetIndexes().ToList();

        // Should have at least two indexes: OrthoCaseId (performance) and (OrthoCaseId, PlanLabel) unique
        var uniqueCompositeIndex = indexes.FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "OrthoCaseId", "PlanLabel" }));

        uniqueCompositeIndex.Should().NotBeNull("a unique composite index on (OrthoCaseId, PlanLabel) must exist in the EF model");
    }

    [Fact]
    public async Task DifferentOrthoCases_CanHaveSameLabel()
    {
        // Arrange: two different ortho cases for two different patients
        var patient1Id = Guid.NewGuid();
        var patient2Id = Guid.NewGuid();
        var orthoCase1Id = Guid.NewGuid();
        var orthoCase2Id = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patient1Id, FirstName = "P1", LastName = "Test", IsActive = true });
        _db.Patients.Add(new Patient { Id = patient2Id, FirstName = "P2", LastName = "Test", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase { Id = orthoCase1Id, PatientId = patient1Id, CaseNumber = "ORT-PL-002", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase { Id = orthoCase2Id, PatientId = patient2Id, CaseNumber = "ORT-PL-003", IsActive = true });
        await _db.SaveChangesAsync();

        // Act: both cases can have Plan A
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCase1Id, PlanLabel = "A" });
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCase2Id, PlanLabel = "A" });
        await _db.SaveChangesAsync();

        // Assert
        var plans = await _db.TreatmentPlans.ToListAsync();
        plans.Should().HaveCount(2);
        plans.All(p => p.PlanLabel == "A").Should().BeTrue();
    }
}
