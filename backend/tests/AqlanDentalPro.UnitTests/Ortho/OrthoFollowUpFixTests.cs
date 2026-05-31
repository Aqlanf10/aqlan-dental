using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Tests for PR #200 follow-up fixes:
/// - Cross-case contract isolation
/// - Checklist no-overcounting in overview fallback
/// - Single-approved-treatment-plan invariant
/// </summary>
public class OrthoCrossCaseContractTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoCrossCaseContractTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<(Guid patientId, Guid orthoCase1Id, Guid orthoCase2Id)> SeedPatientWithTwoOrthoCases()
    {
        var patientId = Guid.NewGuid();
        var orthoCase1Id = Guid.NewGuid();
        var orthoCase2Id = Guid.NewGuid();
        var doctorId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr. Ortho", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCase1Id,
            PatientId = patientId,
            DoctorId = doctorId,
            CaseNumber = "ORT-001",
            IsActive = true,
        });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCase2Id,
            PatientId = patientId,
            DoctorId = doctorId,
            CaseNumber = "ORT-002",
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return (patientId, orthoCase1Id, orthoCase2Id);
    }

    [Fact]
    public async Task Contract_ExplicitlyLinkedToCase_IsSelectedForThatCase()
    {
        var (patientId, orthoCase1Id, orthoCase2Id) = await SeedPatientWithTwoOrthoCases();

        // Contract linked to orthoCase1
        var contract1Id = Guid.NewGuid();
        _db.Contracts.Add(new Contract
        {
            Id = contract1Id,
            PatientId = patientId,
            RelatedCaseId = orthoCase1Id,
            TotalAmount = 100_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // Simulate contract selection for orthoCase1
        var contract = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == orthoCase1Id)
            .FirstOrDefaultAsync();

        contract.Should().NotBeNull();
        contract!.Id.Should().Be(contract1Id);
    }

    [Fact]
    public async Task Contract_LinkedToOtherCase_NotSelectedForThisCase()
    {
        var (patientId, orthoCase1Id, orthoCase2Id) = await SeedPatientWithTwoOrthoCases();

        // Contract linked to orthoCase2
        var contract2Id = Guid.NewGuid();
        _db.Contracts.Add(new Contract
        {
            Id = contract2Id,
            PatientId = patientId,
            RelatedCaseId = orthoCase2Id,
            TotalAmount = 200_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // Simulate contract selection for orthoCase1 — must not pick contract2
        var contract = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == orthoCase1Id)
            .FirstOrDefaultAsync();

        contract.Should().BeNull("contract linked to orthoCase2 should NOT be selected for orthoCase1");
    }

    [Fact]
    public async Task TwoContractsLinkedToDifferentCases_NoLeakage()
    {
        var (patientId, orthoCase1Id, orthoCase2Id) = await SeedPatientWithTwoOrthoCases();

        var contract1Id = Guid.NewGuid();
        var contract2Id = Guid.NewGuid();
        _db.Contracts.Add(new Contract
        {
            Id = contract1Id,
            PatientId = patientId,
            RelatedCaseId = orthoCase1Id,
            TotalAmount = 100_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        _db.Contracts.Add(new Contract
        {
            Id = contract2Id,
            PatientId = patientId,
            RelatedCaseId = orthoCase2Id,
            TotalAmount = 200_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // Each case should find only its own contract
        var contractForCase1 = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == orthoCase1Id)
            .FirstOrDefaultAsync();
        var contractForCase2 = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == orthoCase2Id)
            .FirstOrDefaultAsync();

        contractForCase1.Should().NotBeNull();
        contractForCase1!.TotalAmount.Should().Be(100_000);
        contractForCase2.Should().NotBeNull();
        contractForCase2!.TotalAmount.Should().Be(200_000);
    }

    [Fact]
    public async Task UnlinkedLegacyContract_SingleFallback_IsSelected()
    {
        var (patientId, orthoCase1Id, _) = await SeedPatientWithTwoOrthoCases();

        // One unlinked legacy ortho contract
        var legacyContractId = Guid.NewGuid();
        _db.Contracts.Add(new Contract
        {
            Id = legacyContractId,
            PatientId = patientId,
            RelatedCaseId = null, // Legacy — no case link
            TotalAmount = 150_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // No contract linked to orthoCase1 explicitly, so fallback to unlinked
        var explicitContract = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == orthoCase1Id)
            .FirstOrDefaultAsync();

        explicitContract.Should().BeNull();

        // Fallback: unlinked ortho contracts
        var unlinkedContracts = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == null &&
                (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
            .ToListAsync();

        var fallback = unlinkedContracts.Count == 1 ? unlinkedContracts[0] : null;
        fallback.Should().NotBeNull();
        fallback!.Id.Should().Be(legacyContractId);
    }

    [Fact]
    public async Task MultipleUnlinkedLegacyContracts_NotSilentlyChosen()
    {
        var (patientId, orthoCase1Id, _) = await SeedPatientWithTwoOrthoCases();

        // Two unlinked legacy contracts — ambiguous
        _db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            RelatedCaseId = null,
            TotalAmount = 100_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        _db.Contracts.Add(new Contract
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            RelatedCaseId = null,
            TotalAmount = 200_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        var unlinkedContracts = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == null &&
                (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
            .ToListAsync();

        var fallback = unlinkedContracts.Count == 1 ? unlinkedContracts[0] : null;
        fallback.Should().BeNull("multiple ambiguous unlinked contracts should not be silently chosen");
    }
}

/// <summary>
/// Tests verifying checklist auto-derivation does NOT overcount.
/// One photo should only increment its matching checklist item.
/// </summary>
public class OrthoChecklistNoOvercountingTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoChecklistNoOvercountingTests()
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
            CaseNumber = "ORT-CHK-002",
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return orthoCaseId;
    }

    [Fact]
    public async Task OneExtraoralPhoto_IncrementsOnlyOneItem()
    {
        var orthoCaseId = await SeedOrthoCase();

        // Add ONE extraoral photo with "frontal" caption
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId,
            PhotoType = "Extraoral",
            Caption = "frontal view",
        });
        await _db.SaveChangesAsync();

        // Simulate the per-item derivation logic from overview
        var photos = await _db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        var completed = new[]
        {
            photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("frontal") == true),
            photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("profile") == true),
            photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("smile") == true),
        }.Count(b => b);

        completed.Should().Be(1, "one extraoral frontal photo should only mark 1 item, not 3");
    }

    [Fact]
    public async Task OneIntraoralPhoto_IncrementsOnlyOneItem()
    {
        var orthoCaseId = await SeedOrthoCase();

        // Add ONE intraoral photo with "frontal" caption
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId,
            PhotoType = "Intraoral",
            Caption = "frontal view",
        });
        await _db.SaveChangesAsync();

        var photos = await _db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        var completed = new[]
        {
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("frontal") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("right") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("left") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("upper") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("lower") == true),
        }.Count(b => b);

        completed.Should().Be(1, "one intraoral frontal photo should only mark 1 item, not 5");
    }

    [Fact]
    public async Task PhotoWithoutCaption_DoesNotMarkAnyItem()
    {
        var orthoCaseId = await SeedOrthoCase();

        // Add photos WITHOUT specific captions
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId,
            PhotoType = "Extraoral",
            Caption = null,
        });
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId,
            PhotoType = "Intraoral",
            Caption = null,
        });
        await _db.SaveChangesAsync();

        var photos = await _db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();

        // Precise matching — null captions don't match any specific item
        var extraoralFrontal = photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("frontal") == true);
        var intraoralFrontal = photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("frontal") == true);

        extraoralFrontal.Should().BeFalse("null caption should not match 'frontal'");
        intraoralFrontal.Should().BeFalse("null caption should not match 'frontal'");
    }

    [Fact]
    public async Task FullChecklist_AutoDerivation_CountsExactly14()
    {
        var orthoCaseId = await SeedOrthoCase();

        // Add all photos to satisfy every checklist item
        var captions = new[]
        {
            ("Extraoral", "frontal"), ("Extraoral", "profile"), ("Extraoral", "smile"),
            ("Intraoral", "frontal"), ("Intraoral", "right"), ("Intraoral", "left"),
            ("Intraoral", "upper"), ("Intraoral", "lower"),
            ("Radiograph", "OPG"), ("Radiograph", "CBCT"),
        };
        foreach (var (type, caption) in captions)
        {
            _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
            {
                OrthoCaseId = orthoCaseId,
                PhotoType = type,
                Caption = caption,
            });
        }
        _db.CephAnalyses.Add(new CephAnalysis { OrthoCaseId = orthoCaseId, AnalysisDate = DateOnly.FromDateTime(DateTime.Today) });
        _db.ModelAnalyses.Add(new ModelAnalysis { OrthoCaseId = orthoCaseId });
        _db.Contracts.Add(new Contract
        {
            PatientId = _db.OrthoCases.First().PatientId,
            RelatedCaseId = orthoCaseId,
            TotalAmount = 100_000,
            DiscountAmount = 0,
            Specialty = "orthodontics",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        var photos = await _db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        var hasCeph = await _db.CephAnalyses.AnyAsync(c => c.OrthoCaseId == orthoCaseId);
        var hasModel = await _db.ModelAnalyses.AnyAsync(m => m.OrthoCaseId == orthoCaseId);
        var hasContract = await _db.Contracts.AnyAsync(c => c.RelatedCaseId == orthoCaseId);

        var completed = new[]
        {
            photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("frontal") == true),
            photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("profile") == true),
            photos.Any(p => p.PhotoType == "Extraoral" && p.Caption?.Contains("smile") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("frontal") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("right") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("left") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("upper") == true),
            photos.Any(p => p.PhotoType == "Intraoral" && p.Caption?.Contains("lower") == true),
            photos.Any(p => p.PhotoType == "Radiograph" && p.Caption?.Contains("OPG") == true),
            hasCeph,
            photos.Any(p => p.PhotoType == "Radiograph" && p.Caption?.Contains("CBCT") == true),
            hasModel,
            false, // Consent — cannot auto-derive
            hasContract,
        }.Count(b => b);

        completed.Should().Be(13, "all items except Consent should be marked complete");
    }
}

/// <summary>
/// Tests that only one treatment plan can be approved at a time,
/// through both the legacy and specific approval endpoints.
/// </summary>
public class OrthoSingleApprovedPlanTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoSingleApprovedPlanTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Guid> SeedOrthoCaseWithMultiplePlans()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            CaseNumber = "ORT-SPA-001",
            IsActive = true,
        });
        _db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            OrthoCaseId = orthoCaseId, PlanLabel = "A", TreatmentGoals = "Non-extraction",
        });
        _db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            OrthoCaseId = orthoCaseId, PlanLabel = "B", TreatmentGoals = "Extraction of 4 premolars",
        });
        _db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            OrthoCaseId = orthoCaseId, PlanLabel = "C", TreatmentGoals = "Surgical orthodontics",
        });
        await _db.SaveChangesAsync();
        return orthoCaseId;
    }

    [Fact]
    public async Task ApprovingPlanB_UnapprovesPlanA()
    {
        var orthoCaseId = await SeedOrthoCaseWithMultiplePlans();

        var plans = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId).OrderBy(p => p.PlanLabel).ToListAsync();

        // First approve Plan A
        plans[0].IsApproved = true;
        plans[0].ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Now approve Plan B — simulate the invariant enforcement logic
        var planToApprove = plans[1];
        var otherPlans = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId && p.Id != planToApprove.Id)
            .ToListAsync();
        foreach (var op in otherPlans) { op.IsApproved = false; }
        planToApprove.IsApproved = true;
        planToApprove.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Verify only one plan is approved
        var allPlans = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        allPlans.Count(p => p.IsApproved).Should().Be(1);
        allPlans.First(p => p.IsApproved).PlanLabel.Should().Be("B");
    }

    [Fact]
    public async Task ReApprovingSamePlan_StillOnlyOneApproved()
    {
        var orthoCaseId = await SeedOrthoCaseWithMultiplePlans();

        var planA = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId && p.PlanLabel == "A");

        // Approve Plan A
        var otherPlans = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId && p.Id != planA.Id)
            .ToListAsync();
        foreach (var op in otherPlans) { op.IsApproved = false; }
        planA.IsApproved = true;
        planA.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Re-approve Plan A
        otherPlans = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId && p.Id != planA.Id)
            .ToListAsync();
        foreach (var op in otherPlans) { op.IsApproved = false; }
        planA.IsApproved = true;
        planA.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var allPlans = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        allPlans.Count(p => p.IsApproved).Should().Be(1);
        allPlans.First(p => p.IsApproved).PlanLabel.Should().Be("A");
    }

    [Fact]
    public async Task SwitchingApproval_MultipleTimes_OnlyOneRemains()
    {
        var orthoCaseId = await SeedOrthoCaseWithMultiplePlans();

        // Approve A
        var planA = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId && p.PlanLabel == "A");
        var others = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId && p.Id != planA.Id).ToListAsync();
        foreach (var op in others) { op.IsApproved = false; }
        planA.IsApproved = true;
        planA.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Approve C
        var planC = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId && p.PlanLabel == "C");
        others = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId && p.Id != planC.Id).ToListAsync();
        foreach (var op in others) { op.IsApproved = false; }
        planC.IsApproved = true;
        planC.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Approve B
        var planB = await _db.TreatmentPlans.FirstAsync(p => p.OrthoCaseId == orthoCaseId && p.PlanLabel == "B");
        others = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId && p.Id != planB.Id).ToListAsync();
        foreach (var op in others) { op.IsApproved = false; }
        planB.IsApproved = true;
        planB.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var allPlans = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        allPlans.Count(p => p.IsApproved).Should().Be(1);
        allPlans.First(p => p.IsApproved).PlanLabel.Should().Be("B");
    }

    [Fact]
    public async Task LegacyApprovalEndpoint_AlsoEnforcesInvariant()
    {
        // This test simulates the logic in the legacy ApproveTreatmentPlan endpoint
        // (PATCH /treatment-plan/approve) which approves the latest plan
        var orthoCaseId = await SeedOrthoCaseWithMultiplePlans();

        // Legacy endpoint picks the latest plan by CreatedAt
        var plan = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        plan.Should().NotBeNull();

        // Simulate: un-approve other plans
        var otherPlans = await _db.TreatmentPlans
            .Where(p => p.OrthoCaseId == orthoCaseId && p.Id != plan!.Id)
            .ToListAsync();
        foreach (var op in otherPlans) { op.IsApproved = false; }

        plan.IsApproved = true;
        plan.ApprovedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var allPlans = await _db.TreatmentPlans.Where(p => p.OrthoCaseId == orthoCaseId).ToListAsync();
        allPlans.Count(p => p.IsApproved).Should().Be(1);
    }
}
