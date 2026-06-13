using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TreatmentPlanEntity = AqlanDentalPro.Domain.Entities.TreatmentPlan;

namespace AqlanDentalPro.UnitTests.Ortho;

public class OrthoTreatmentPlanP4Tests : IDisposable
{
    private readonly AppDbContext db;
    private readonly OrthoCasesController controller;

    public OrthoTreatmentPlanP4Tests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new AppDbContext(options);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(user => user.Role).Returns(UserRole.Admin);
        currentUser.SetupGet(user => user.IsAdmin).Returns(true);

        controller = new OrthoCasesController(
            new OrthoService(db, currentUser.Object),
            db,
            currentUser.Object);
    }

    public void Dispose() => db.Dispose();

    private async Task<Guid> SeedCaseAsync()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "P4",
            LastName = "Patient",
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            CaseNumber = $"ORT-P4-{Guid.NewGuid():N}"[..20],
            IsActive = true,
        };
        db.Patients.Add(patient);
        db.OrthoCases.Add(orthoCase);
        await db.SaveChangesAsync();
        return orthoCase.Id;
    }

    private static CreateTreatmentPlanRequest CompletePlan(
        string label = "A",
        List<TreatmentPlanObjectiveRequest>? objectives = null) => new()
    {
        PlanLabel = label,
        ApplianceType = "Fixed",
        ExpectedDurationMonths = 20,
        TreatmentGoals = "تحسين العلاقة الهيكلية ومحاذاة الأسنان",
        MechanicsPlan = "Leveling, alignment and space closure",
        AuxiliaryAppliances = "TADs when clinically required",
        SpaceManagementPlan = "IPR as indicated",
        InterdisciplinaryPlan = "Periodontal review before finishing",
        Objectives = objectives ??
        [
            new TreatmentPlanObjectiveRequest
            {
                Category = "Dental",
                Description = "تصحيح التزاحم العلوي والسفلي",
                Priority = 1,
                SortOrder = 0,
            },
        ],
        Phases =
        [
            new TreatmentPlanPhaseRequest
            {
                PhaseName = "المحاذاة والتسوية",
                SequenceNumber = 1,
                ObjectiveSummary = "الوصول إلى أسلاك مستطيلة",
                PlannedAppliance = "Fixed appliance",
                Mechanics = "NiTi sequence",
                TargetDurationMonths = 6,
                PlannedStartDate = "2026-07-01",
                PlannedEndDate = "2026-12-31",
                Status = "Planned",
            },
        ],
    };

    [Fact]
    public async Task CreateStructuredPlan_PersistsObjectivesPhasesAndMechanics()
    {
        var caseId = await SeedCaseAsync();

        var result = await controller.CreateTreatmentPlan(caseId, CompletePlan());

        result.Should().BeOfType<OkObjectResult>();
        var plan = await db.TreatmentPlans
            .Include(item => item.Objectives)
            .Include(item => item.Phases)
            .SingleAsync(item => item.OrthoCaseId == caseId);
        plan.MechanicsPlan.Should().Contain("Leveling");
        plan.AuxiliaryAppliances.Should().Contain("TADs");
        plan.Objectives.Should().ContainSingle();
        plan.Objectives.Single().Description.Should().Contain("التزاحم");
        plan.Phases.Should().ContainSingle();
        plan.Phases.Single().PhaseName.Should().Be("المحاذاة والتسوية");
        plan.PatientDecisionStatus.Should().Be("NotPresented");
    }

    [Fact]
    public async Task CreatePlan_DuplicatePhaseSequence_ReturnsBadRequestWithoutWrite()
    {
        var caseId = await SeedCaseAsync();
        var request = CompletePlan();
        request.Phases!.Add(new TreatmentPlanPhaseRequest
        {
            PhaseName = "الإغلاق",
            SequenceNumber = 1,
        });

        var result = await controller.CreateTreatmentPlan(caseId, request);

        result.Should().BeOfType<BadRequestObjectResult>();
        var wasWritten = await db.TreatmentPlans.AnyAsync(item => item.OrthoCaseId == caseId);
        wasWritten.Should().BeFalse();
    }

    [Fact]
    public async Task ApprovePlan_WithoutStructuredRequirements_ReturnsBadRequest()
    {
        var caseId = await SeedCaseAsync();
        var plan = new TreatmentPlanEntity
        {
            OrthoCaseId = caseId,
            PlanLabel = "A",
            TreatmentGoals = "هدف مختصر",
            ExpectedDurationMonths = 12,
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var result = await controller.ApproveSpecificTreatmentPlan(caseId, plan.Id);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.TreatmentPlans.FindAsync(plan.Id))!.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveCompletePlan_SucceedsAndLocksFutureUpdates()
    {
        var caseId = await SeedCaseAsync();
        await controller.CreateTreatmentPlan(caseId, CompletePlan());
        var plan = await db.TreatmentPlans.SingleAsync(item => item.OrthoCaseId == caseId);

        var approval = await controller.ApproveSpecificTreatmentPlan(caseId, plan.Id);
        var update = await controller.UpdateTreatmentPlan(
            caseId,
            plan.Id,
            new UpsertTreatmentPlanRequest { TreatmentGoals = "تعديل بعد الاعتماد" });

        approval.Should().BeOfType<OkObjectResult>();
        update.Should().BeOfType<BadRequestObjectResult>();
        (await db.TreatmentPlans.FindAsync(plan.Id))!.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task PatientDecision_BeforeClinicalApproval_IsRejected()
    {
        var caseId = await SeedCaseAsync();
        await controller.CreateTreatmentPlan(caseId, CompletePlan());
        var plan = await db.TreatmentPlans.SingleAsync(item => item.OrthoCaseId == caseId);

        var result = await controller.RecordPatientDecision(
            caseId,
            plan.Id,
            new RecordPatientDecisionRequest
            {
                Status = "Accepted",
                DecisionBy = "ولي أمر المريض",
            });

        result.Should().BeOfType<BadRequestObjectResult>();
        (await db.TreatmentPlans.FindAsync(plan.Id))!.PatientDecisionStatus
            .Should().Be("NotPresented");
    }

    [Fact]
    public async Task AcceptedDecision_RequiresDecisionMakerName()
    {
        var caseId = await SeedCaseAsync();
        await controller.CreateTreatmentPlan(caseId, CompletePlan());
        var plan = await db.TreatmentPlans.SingleAsync(item => item.OrthoCaseId == caseId);
        await controller.ApproveSpecificTreatmentPlan(caseId, plan.Id);

        var result = await controller.RecordPatientDecision(
            caseId,
            plan.Id,
            new RecordPatientDecisionRequest { Status = "Accepted" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AcceptedDecision_PersistsPresentationConsentAndAuditDates()
    {
        var caseId = await SeedCaseAsync();
        await controller.CreateTreatmentPlan(caseId, CompletePlan());
        var plan = await db.TreatmentPlans.SingleAsync(item => item.OrthoCaseId == caseId);
        await controller.ApproveSpecificTreatmentPlan(caseId, plan.Id);

        var result = await controller.RecordPatientDecision(
            caseId,
            plan.Id,
            new RecordPatientDecisionRequest
            {
                Status = "accepted",
                DecisionBy = "والد المريض",
                ConsentMethod = "Written",
                Notes = "تم شرح البدائل والمخاطر",
            });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.TreatmentPlans.FindAsync(plan.Id);
        saved!.PatientDecisionStatus.Should().Be("Accepted");
        saved.PatientDecisionBy.Should().Be("والد المريض");
        saved.PatientConsentMethod.Should().Be("Written");
        saved.PresentedAt.Should().NotBeNull();
        saved.PatientDecisionAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPlans_ReturnsStructuredCollectionsInStableOrder()
    {
        var caseId = await SeedCaseAsync();
        var request = CompletePlan(objectives:
        [
            new TreatmentPlanObjectiveRequest
            {
                Category = "Stability",
                Description = "الاحتفاظ بالنتيجة",
                Priority = 2,
                SortOrder = 2,
            },
            new TreatmentPlanObjectiveRequest
            {
                Category = "Dental",
                Description = "المحاذاة",
                Priority = 1,
                SortOrder = 1,
            },
        ]);
        await controller.CreateTreatmentPlan(caseId, request);

        var result = await controller.GetTreatmentPlans(caseId);

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.TreatmentPlanObjectives
            .Where(item => item.TreatmentPlan.OrthoCaseId == caseId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        saved.Select(item => item.SortOrder).Should().Equal(1, 2);
    }
}
