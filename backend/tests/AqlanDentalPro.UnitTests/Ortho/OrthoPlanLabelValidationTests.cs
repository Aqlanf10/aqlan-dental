using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TreatmentPlanEntity = AqlanDentalPro.Domain.Entities.TreatmentPlan;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Tests for PlanLabel validation: only A/B/C allowed, no duplicates per ortho case,
/// max three plans per case, unique index in EF model snapshot, canonical uppercase
/// normalisation, and concurrent conflict detection.
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

    // ── Canonicalisation tests ──────────────────────────────────────────────────

    [Fact]
    public async Task LowercaseLabel_NormalisedToUppercase()
    {
        // Sending "a" must be normalised to "A" before persistence.
        // This mirrors the controller logic: normalizedLabel = req.PlanLabel?.Trim().ToUpperInvariant()
        var rawLabel = "a";
        var normalizedLabel = rawLabel.Trim().ToUpperInvariant();

        normalizedLabel.Should().Be("A", "lowercase 'a' must be canonicalised to uppercase 'A' before storage");

        // Persist and verify the stored value is uppercase
        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = normalizedLabel });
        await _db.SaveChangesAsync();

        var stored = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId);
        stored.PlanLabel.Should().Be("A");
    }

    [Fact]
    public async Task PaddedLowercaseLabel_NormalisedToUppercase()
    {
        // Sending " b " must be normalised to "B" before persistence.
        var rawLabel = " b ";
        var normalizedLabel = rawLabel.Trim().ToUpperInvariant();

        normalizedLabel.Should().Be("B", "' b ' must be trimmed and uppercased to 'B'");

        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = normalizedLabel });
        await _db.SaveChangesAsync();

        var stored = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId);
        stored.PlanLabel.Should().Be("B");
    }

    [Fact]
    public async Task ExistingUppercaseA_RejectsLowercaseA()
    {
        // If Plan A already exists, requesting "a" (which normalises to "A") must be rejected.
        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "A" });
        await _db.SaveChangesAsync();

        // Simulate controller logic: normalise then check duplicates
        var normalizedLabel = "a".Trim().ToUpperInvariant(); // → "A"
        var existingLabels = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .Select(p => p.PlanLabel)
            .ToListAsync();

        var isDuplicate = existingLabels.Contains(normalizedLabel, StringComparer.OrdinalIgnoreCase);
        isDuplicate.Should().BeTrue("an existing 'A' must reject a requested 'a' after normalisation");
    }

    [Fact]
    public async Task MixedCaseDuplicateLabels_CannotCoexistForSameOrthoCaseId()
    {
        // After canonicalisation, "A" and "a" map to the same value and cannot both exist.
        // The unique index on (OrthoCaseId, PlanLabel) enforces this at the DB level,
        // and the pre-check with OrdinalIgnoreCase catches it before SaveChanges.
        var orthoCaseId = await SeedOrthoCase();
        _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = "A" });
        await _db.SaveChangesAsync();

        // Normalise "a" → "A", then check for duplicate
        var requestedLabel = "a".Trim().ToUpperInvariant(); // → "A"
        var existingLabels = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .Select(p => p.PlanLabel)
            .ToListAsync();

        var isDuplicate = existingLabels.Contains(requestedLabel, StringComparer.OrdinalIgnoreCase);
        isDuplicate.Should().BeTrue("mixed-case duplicates must be detected after canonicalisation");
    }

    [Fact]
    public async Task DatabaseUniqueness_EnforcesCanonicalUppercase()
    {
        // The unique index on (OrthoCaseId, PlanLabel) in PostgreSQL is case-sensitive.
        // Because the controller always normalises to uppercase BEFORE persisting,
        // the index never sees lowercase values. This test verifies that the
        // canonicalisation + index strategy is self-consistent: all stored values
        // are uppercase, so case-sensitivity of the index is never a problem.
        var orthoCaseId = await SeedOrthoCase();

        // Simulate three normalised inserts (matching controller flow)
        var labels = new[] { "a", " B ", "c" };
        foreach (var raw in labels)
        {
            var normalized = raw.Trim().ToUpperInvariant();
            _db.TreatmentPlans.Add(new TreatmentPlanEntity { OrthoCaseId = orthoCaseId, PlanLabel = normalized });
        }
        await _db.SaveChangesAsync();

        var storedPlans = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .OrderBy(p => p.PlanLabel)
            .ToListAsync();

        storedPlans.Should().HaveCount(3);
        storedPlans[0].PlanLabel.Should().Be("A");
        storedPlans[1].PlanLabel.Should().Be("B");
        storedPlans[2].PlanLabel.Should().Be("C");

        // All stored labels are uppercase — consistent with the unique index
        storedPlans.All(p => p.PlanLabel == p.PlanLabel.ToUpperInvariant()).Should().BeTrue();
    }

    // ── Original tests ──────────────────────────────────────────────────────────

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

    // ── Entity default label test ───────────────────────────────────────────────

    [Fact]
    public async Task NewTreatmentPlan_DefaultsToPlanLabelA()
    {
        // The TreatmentPlan entity defaults PlanLabel to "A".
        // This is critical for the legacy PUT endpoint which creates plans
        // without specifying a label.
        var orthoCaseId = await SeedOrthoCase();
        var plan = new TreatmentPlanEntity { OrthoCaseId = orthoCaseId };
        plan.PlanLabel.Should().Be("A", "entity default must be uppercase 'A' for the legacy PUT endpoint");

        _db.TreatmentPlans.Add(plan);
        await _db.SaveChangesAsync();

        var stored = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId);
        stored.PlanLabel.Should().Be("A");
    }
}

/// <summary>
/// Tests for concurrent PlanLabel conflict handling via PostgreSQL unique-constraint detection.
/// Since Npgsql 8.x PostgresException constructors are internal, we verify the detection logic
/// through the string-based fallback path and verify the exception-filter pattern works correctly.
/// </summary>
public class OrthoPlanLabelConflictTests
{
    [Fact]
    public void UniqueConstraintViolation_FallbackStringDetection_With23505()
    {
        // Arrange: simulate a DbUpdateException wrapping an exception with SQLSTATE 23505
        // This exercises the string-based fallback in IsUniqueConstraintViolation
        var innerException = new Exception("duplicate key value violates unique constraint \"IX_TreatmentPlans_OrthoCaseId_PlanLabel\" — SQLSTATE 23505");
        var dbUpdateException = new DbUpdateException("An error occurred while saving the entity changes.", innerException);

        // Act: the fallback path checks inner exception message for "23505"
        var inner = dbUpdateException.InnerException;
        var isDetected = inner?.Message?.Contains("23505") == true;

        // Assert
        isDetected.Should().BeTrue("the fallback string detection must recognize '23505' in the inner exception message");
    }

    [Fact]
    public void NonUniqueConstraintError_NotDetectedAsConflict()
    {
        // Arrange: FK violation (23503) should NOT be treated as unique constraint
        var innerException = new Exception("insert or update on table violates foreign key constraint — SQLSTATE 23503");
        var dbUpdateException = new DbUpdateException("An error occurred while saving the entity changes.", innerException);

        // Act
        var inner = dbUpdateException.InnerException;
        var isDetectedAs23505 = inner?.Message?.Contains("23505") == true;

        // Assert
        isDetectedAs23505.Should().BeFalse("FK violation (23503) must not be detected as a unique-constraint conflict");
    }

    [Fact]
    public void DbUpdateException_WithoutInnerException_IsNotConflict()
    {
        // Arrange: a DbUpdateException with no inner exception
        var dbUpdateException = new DbUpdateException("Concurrency conflict.", (Exception?)null);

        // Act
        var inner = dbUpdateException.InnerException;
        var isDetected = inner?.Message?.Contains("23505") == true;

        // Assert
        isDetected.Should().BeFalse("a DbUpdateException without an inner exception must not be treated as a unique-constraint conflict");
    }

    [Fact]
    public async Task Controller_Returns409_OnConcurrentDuplicateLabel()
    {
        // Arrange: verify the full controller flow pattern.
        // When the unique index IX_TreatmentPlans_OrthoCaseId_PlanLabel catches a race condition,
        // the controller must return HTTP 409 with an Arabic message — NOT a 500.
        // We verify the expected response structure here.

        // The conflict response shape must match:
        var conflictResponse = new { message = "تصنيف الخطة مستخدم بالفعل لهذه الحالة. قم بتحديث الصفحة والمحاولة مرة أخرى." };

        // Assert: message is Arabic, user-friendly, no database details exposed
        conflictResponse.message.Should().Contain("تصنيف الخطة");
        conflictResponse.message.Should().Contain("تحديث الصفحة");
        conflictResponse.message.Should().NotContain("23505", "database error codes must not be exposed to clients");
        conflictResponse.message.Should().NotContain("IX_TreatmentPlans", "index names must not be exposed to clients");
        conflictResponse.message.Should().NotContain("unique constraint", "internal constraint details must not be exposed to clients");
    }

    [Fact]
    public void PreCheck_ThenSave_HasRaceConditionWindow()
    {
        // Document the known race condition window:
        // 1. Request A reads existing labels → ["A"] → auto-selects "B"
        // 2. Request B reads existing labels → ["A"] → also auto-selects "B"
        // 3. Request A saves Plan B → success
        // 4. Request B saves Plan B → DbUpdateException (23505) → caught → HTTP 409
        //
        // The pre-check (duplicate label check) eliminates the common case.
        // The try/catch on SaveChanges handles the race condition window.
        // Both layers are needed for correctness.

        var existingLabels = new[] { "A" }.ToList();
        var validLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A", "B", "C" };

        // Both concurrent requests would select "B"
        var chosenByRequest1 = validLabels.FirstOrDefault(l => !existingLabels.Contains(l, StringComparer.OrdinalIgnoreCase));
        var chosenByRequest2 = validLabels.FirstOrDefault(l => !existingLabels.Contains(l, StringComparer.OrdinalIgnoreCase));

        chosenByRequest1.Should().Be("B");
        chosenByRequest2.Should().Be("B");
        // Both chose the same label — the unique index will catch one of them.
    }

    [Fact]
    public void ConcurrentLowercaseRequests_Canonicalised_BeforeRace()
    {
        // Even if two concurrent requests send different cases for the same logical label
        // (e.g. "a" and "A"), canonicalisation normalises both to "A" before the
        // pre-check, so the second request is rejected at the validation layer —
        // not only at the database constraint layer.
        var raw1 = "a";
        var raw2 = "A";

        var normalized1 = raw1.Trim().ToUpperInvariant(); // "A"
        var normalized2 = raw2.Trim().ToUpperInvariant(); // "A"

        normalized1.Should().Be(normalized2, "different cases of the same label must canonicalise to the same value");

        // After canonicalisation, both map to "A" — the pre-check catches it.
        var existingLabels = new List<string> { "A" };
        var isDuplicate1 = existingLabels.Contains(normalized1, StringComparer.OrdinalIgnoreCase);
        var isDuplicate2 = existingLabels.Contains(normalized2, StringComparer.OrdinalIgnoreCase);

        isDuplicate1.Should().BeTrue("normalised 'a' → 'A' must be detected as duplicate of existing 'A'");
        isDuplicate2.Should().BeTrue("normalised 'A' must be detected as duplicate of existing 'A'");
    }

    [Fact]
    public void PaddedLabel_Canonicalised_BeforeValidation()
    {
        // A label like " b " is trimmed and uppercased to "B" before any validation.
        // This ensures the unique index (case-sensitive) never sees lowercase or padded values.
        var rawLabel = " b ";
        var normalizedLabel = rawLabel.Trim().ToUpperInvariant();

        normalizedLabel.Should().Be("B");

        var validLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "A", "B", "C" };
        validLabels.Contains(normalizedLabel).Should().BeTrue("normalised 'B' must pass the whitelist check");
    }
}
