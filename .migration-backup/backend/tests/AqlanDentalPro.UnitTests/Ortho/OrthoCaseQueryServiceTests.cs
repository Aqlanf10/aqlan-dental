using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TreatmentPlanEntity = AqlanDentalPro.Domain.Entities.TreatmentPlan;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Unit tests for <see cref="OrthoCaseQueryService"/>.
///
/// Mirrors the LabOrderQueryServiceTests pattern (PR #542): the query service is
/// tested directly with an in-memory <see cref="AppDbContext"/>, asserting the
/// extracted read-side behavior that was previously inline in
/// <c>OrthoCasesController</c>.
///
/// Coverage:
///   - GetLabOrdersAsync: filters by OrthoCaseId + IsActive, orders by CreatedAt desc.
///   - GetOverviewAsync: returns null when case missing; auto-derives checklist;
///     uses saved checklist when present; contract selection (linked + legacy fallback).
///   - GetClinicalExamAsync: returns null when none; latest exam otherwise.
///   - GetProblemListAsync: orders by SortOrder then CreatedAt.
///   - GetTreatmentPlansAsync: orders by PlanLabel; includes ApprovedByName/ApprovedAt.
///   - GetTreatmentPlanAsync: returns null when none; picks approved-over-unapproved.
///   - GetExtractionDecisionAsync: returns null when none.
///   - GetChecklistAsync: returns saved row when present; auto-derives when absent.
///   - GetDiagnosisAsync: returns null when case missing; returns saved diagnosis when
///     present; computes summary (skeletal class, facial pattern) from CephAnalysis
///     measurements when no saved diagnosis.
///   - GetRetentionAsync: returns null when none; nests visits ordered by VisitDate.
///   - GetRetentionVisitsAsync: orders by VisitDate descending.
///   - GetPhotosAsync: filters by category/phase/selectedOnly; default order SortOrder+TakenAt.
///   - GetImagePreparationAsync: returns null when photo missing; returns defaults when
///     no preparation row exists.
/// </summary>
public class OrthoCaseQueryServiceTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoCaseQueryServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OrthoCaseQueryServiceTests_{Guid.NewGuid()}")
            .Options);
    }

    public void Dispose() => _db.Dispose();

    private static OrthoCaseQueryService CreateService(AppDbContext db)
        => new(db, NullLogger<OrthoCaseQueryService>.Instance);

    private async Task<(Guid patientId, Guid orthoCaseId)> SeedCaseAsync()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient
        {
            Id = patientId,
            FirstName = "اختبار",
            LastName = "المريض",
            IsActive = true,
        });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            CaseNumber = $"ORT-TEST-{Guid.NewGuid():N}".Substring(0, 20),
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return (patientId, orthoCaseId);
    }

    // ─── GetLabOrdersAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetLabOrdersAsync_ReturnsOnlyActiveOrdersForCase_OrderedByCreatedAtDesc()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();

        var olderId = Guid.NewGuid();
        var newerId = Guid.NewGuid();
        _db.LabOrders.Add(new LabOrder
        {
            Id = olderId, OrthoCaseId = orthoCaseId, OrderNumber = "LAB-1",
            ApplianceType = "تاج", Status = "sent", Priority = "normal",
        });
        _db.LabOrders.Add(new LabOrder
        {
            Id = newerId, OrthoCaseId = orthoCaseId, OrderNumber = "LAB-2",
            ApplianceType = "جسر", Status = "delivered", Priority = "urgent",
        });
        // Inactive order — must be filtered out
        _db.LabOrders.Add(new LabOrder
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCaseId, OrderNumber = "LAB-INACTIVE",
            IsActive = false,
        });
        // Order for a different case — must be filtered out
        _db.LabOrders.Add(new LabOrder
        {
            Id = Guid.NewGuid(), OrthoCaseId = Guid.NewGuid(), OrderNumber = "LAB-OTHER",
        });
        await _db.SaveChangesAsync();

        // Backdate CreatedAt so ordering is deterministic (AppDbContext SaveChangesAsync
        // override resets CreatedAt on Added entities to UtcNow).
        var older = await _db.LabOrders.FindAsync(olderId);
        var newer = await _db.LabOrders.FindAsync(newerId);
        older!.CreatedAt = DateTime.UtcNow.AddDays(-1);
        newer!.CreatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetLabOrdersAsync(orthoCaseId);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(newerId, "newest CreatedAt comes first");
        result[1].Id.Should().Be(olderId);
        result.Should().OnlyContain(o => o.OrderNumber != "LAB-INACTIVE" && o.OrderNumber != "LAB-OTHER");
    }

    [Fact]
    public async Task GetLabOrdersAsync_ReturnsEmptyList_WhenNoOrders()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var service = CreateService(_db);

        var result = await service.GetLabOrdersAsync(orthoCaseId);

        result.Should().BeEmpty();
    }

    // ─── GetOverviewAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOverviewAsync_ReturnsNull_WhenCaseMissing()
    {
        var service = CreateService(_db);
        var result = await service.GetOverviewAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOverviewAsync_AutoDerivesChecklist_WhenNoSavedChecklist()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        // One tagged photo → ExtraoralFrontal auto-derives
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId, PhotoUrl = "/u/1.jpg", PhotoType = "Extraoral",
            Category = "Extraoral", Subtype = "FrontalRest",
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetOverviewAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.ChecklistCompleted.Should().Be(1, "one tagged ExtraoralFrontal photo auto-derives exactly one item");
        result.ChecklistTotal.Should().Be(14);
        result.HasClinicalExam.Should().BeFalse();
        result.ProblemsCount.Should().Be(0);
        result.HasTreatmentPlan.Should().BeFalse();
    }

    [Fact]
    public async Task GetOverviewAsync_UsesSavedChecklist_WhenPresent()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        _db.RecordsChecklists.Add(new RecordsChecklist
        {
            OrthoCaseId = orthoCaseId,
            ExtraoralFrontal = true, ExtraoralProfile = true, ExtraoralSmile = false,
            IntraoralFrontal = true, IntraoralRight = false, IntraoralLeft = false,
            UpperOcclusal = false, LowerOcclusal = false,
            Opg = true, LateralCeph = false, Cbct = false,
            StudyModels = true, Consent = false, Contract = true,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetOverviewAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.ChecklistCompleted.Should().Be(6, "saved checklist counts exactly 6 true items");
    }

    [Fact]
    public async Task GetOverviewAsync_SelectsContractLinkedToCase_WithPaidSumExcludingInactivePayments()
    {
        var (patientId, orthoCaseId) = await SeedCaseAsync();
        var contractId = Guid.NewGuid();
        _db.Contracts.Add(new Contract
        {
            Id = contractId,
            PatientId = patientId,
            RelatedCaseId = orthoCaseId,
            Specialty = "orthodontics",
            TotalAmount = 200_000,
            DiscountAmount = 20_000,
            IsActive = true,
        });
        _db.Payments.Add(new Payment { ContractId = contractId, Amount = 50_000, IsActive = true });
        // Soft-deleted payment — must NOT count toward paid
        _db.Payments.Add(new Payment { ContractId = contractId, Amount = 30_000, IsActive = false });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetOverviewAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.ContractId.Should().Be(contractId);
        result.ContractTotal.Should().Be(180_000m);     // 200_000 - 20_000
        result.ContractPaid.Should().Be(50_000m);        // only active payment
        result.ContractRemaining.Should().Be(130_000m);  // 180_000 - 50_000
    }

    // ─── GetClinicalExamAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetClinicalExamAsync_ReturnsNull_WhenNoneExists()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var service = CreateService(_db);

        var result = await service.GetClinicalExamAsync(orthoCaseId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetClinicalExamAsync_ReturnsExam_WithFormattedDateAndFields()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var examId = Guid.NewGuid();
        _db.OrthoClinicalExams.Add(new OrthoClinicalExam
        {
            Id = examId, OrthoCaseId = orthoCaseId, ExamDate = new DateOnly(2026, 2, 1),
            FacialSymmetry = "Symmetric",
            Overjet = 3.5m, Crossbite = true, OpenBite = false,
            UpperSpacing = 2.0m, MolarRelationRight = "ClassII",
            OralHygiene = "Good",
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetClinicalExamAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(examId);
        result.ExamDate.Should().Be("2026-02-01", "ExamDate is formatted as yyyy-MM-dd");
        result.FacialSymmetry.Should().Be("Symmetric");
        result.Overjet.Should().Be(3.5m);
        result.Crossbite.Should().BeTrue();
        // Phase 3 structured fields are passed through unchanged
        result.MolarRelationRight.Should().Be("ClassII");
        result.OralHygiene.Should().Be("Good");
    }

    // ─── GetProblemListAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetProblemListAsync_OrdersBySortOrder_ThenCreatedAt()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        // Insert in non-sorted order — SortOrder takes precedence
        _db.ProblemListItems.Add(new ProblemListItem
        {
            OrthoCaseId = orthoCaseId, Category = "Dental", Description = "Crowding", SortOrder = 2,
        });
        _db.ProblemListItems.Add(new ProblemListItem
        {
            OrthoCaseId = orthoCaseId, Category = "Skeletal", Description = "Class II", SortOrder = 1,
        });
        _db.ProblemListItems.Add(new ProblemListItem
        {
            OrthoCaseId = orthoCaseId, Category = "SoftTissue", Description = "Lip incompetence", SortOrder = 3,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetProblemListAsync(orthoCaseId);

        result.Should().HaveCount(3);
        result[0].SortOrder.Should().Be(1);
        result[1].SortOrder.Should().Be(2);
        result[2].SortOrder.Should().Be(3);
    }

    // ─── GetTreatmentPlansAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetTreatmentPlansAsync_OrdersByPlanLabel_AndIncludesApprovedBy()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var doctorId = Guid.NewGuid();
        var approvingDoctor = new Doctor { Id = doctorId, Name = "د. عقلان", IsActive = true };
        _db.Doctors.Add(approvingDoctor);

        _db.TreatmentPlans.Add(new TreatmentPlanEntity
        {
            OrthoCaseId = orthoCaseId, PlanLabel = "B", IsApproved = false,
            ApplianceType = "Metal",
        });
        _db.TreatmentPlans.Add(new TreatmentPlanEntity
        {
            OrthoCaseId = orthoCaseId, PlanLabel = "A", IsApproved = true,
            ApprovedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ApprovedBy = doctorId,
            ApprovedByDoctor = approvingDoctor, // set navigation explicitly (FK name doesn't match conventions)
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetTreatmentPlansAsync(orthoCaseId);

        result.Should().HaveCount(2);
        result[0].PlanLabel.Should().Be("A");
        result[1].PlanLabel.Should().Be("B");
        result[0].IsApproved.Should().BeTrue();
        result[0].ApprovedByName.Should().Be("د. عقلان");
        result[0].ApprovedAt.Should().Be("2026-01-15");
    }

    // ─── GetTreatmentPlanAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetTreatmentPlanAsync_ReturnsNull_WhenNoPlanExists()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var service = CreateService(_db);

        var result = await service.GetTreatmentPlanAsync(orthoCaseId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTreatmentPlanAsync_PrefersApprovedOverUnapproved_EvenWhenUnapprovedIsNewer()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var approvedId = Guid.NewGuid();
        var unapprovedId = Guid.NewGuid();

        // Approved plan (older CreatedAt — must still win because IsApproved=true)
        _db.TreatmentPlans.Add(new TreatmentPlanEntity
        {
            Id = approvedId, OrthoCaseId = orthoCaseId, PlanLabel = "A", IsApproved = true,
            ApplianceType = "Metal", ApprovedAt = DateTime.UtcNow,
        });
        // Unapproved plan (newer CreatedAt — must NOT win over the approved one)
        _db.TreatmentPlans.Add(new TreatmentPlanEntity
        {
            Id = unapprovedId, OrthoCaseId = orthoCaseId, PlanLabel = "B", IsApproved = false,
            ApplianceType = "Ceramic",
        });
        await _db.SaveChangesAsync();

        // Make Plan B (unapproved) newer than Plan A — but still NOT preferred because of the
        // IsApproved ? 1 : 0 ordering in the query.
        var approved = await _db.TreatmentPlans.FindAsync(approvedId);
        var unapproved = await _db.TreatmentPlans.FindAsync(unapprovedId);
        approved!.CreatedAt = DateTime.UtcNow.AddDays(-5);
        unapproved!.CreatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetTreatmentPlanAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(approvedId, "approved plan wins even when an unapproved one is newer");
        result.IsApproved.Should().BeTrue();
    }

    // ─── GetExtractionDecisionAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetExtractionDecisionAsync_ReturnsNull_WhenNoneExists()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var service = CreateService(_db);

        var result = await service.GetExtractionDecisionAsync(orthoCaseId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExtractionDecisionAsync_ReturnsLatest_WithDoctorName()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var doctorId = Guid.NewGuid();
        var approvingDoctor = new Doctor { Id = doctorId, Name = "د. عقلان", IsActive = true };
        _db.Doctors.Add(approvingDoctor);

        var decisionId = Guid.NewGuid();
        _db.ExtractionDecisions.Add(new ExtractionDecision
        {
            Id = decisionId, OrthoCaseId = orthoCaseId, Decision = "Extraction", DecidedBy = doctorId,
            DecidedAt = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc),
            Doctor = approvingDoctor,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetExtractionDecisionAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(decisionId);
        result.Decision.Should().Be("Extraction");
        result.DecidedByName.Should().Be("د. عقلان");
        result.DecidedAt.Should().Be("2026-02-01");
    }

    // ─── GetChecklistAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetChecklistAsync_ReturnsSavedRow_WhenPresent()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var checklistId = Guid.NewGuid();
        _db.RecordsChecklists.Add(new RecordsChecklist
        {
            Id = checklistId, OrthoCaseId = orthoCaseId,
            ExtraoralFrontal = true, ExtraoralProfile = false, ExtraoralSmile = true,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetChecklistAsync(orthoCaseId);

        result.Id.Should().Be(checklistId);
        result.OrthoCaseId.Should().Be(orthoCaseId);
        result.ExtraoralFrontal.Should().BeTrue();
        result.ExtraoralProfile.Should().BeFalse();
        result.ExtraoralSmile.Should().BeTrue();
    }

    [Fact]
    public async Task GetChecklistAsync_AutoDerivesFromPhotos_WhenNoSavedRow_WithNullId()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId, PhotoUrl = "/u/1.jpg", PhotoType = "Extraoral",
            Category = "Extraoral", Subtype = "Profile",
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetChecklistAsync(orthoCaseId);

        result.Id.Should().BeNull("no saved checklist → Id is null in the auto-derived response");
        result.OrthoCaseId.Should().Be(orthoCaseId);
        result.ExtraoralProfile.Should().BeTrue("one tagged ExtraoralProfile photo auto-derives the item");
        result.ExtraoralFrontal.Should().BeFalse();
        result.Consent.Should().BeFalse("Consent cannot be auto-derived");
    }

    // ─── GetDiagnosisAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetDiagnosisAsync_ReturnsNull_WhenCaseMissing()
    {
        var service = CreateService(_db);
        var result = await service.GetDiagnosisAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDiagnosisAsync_ComputesSkeletalClass_FromCephANBMeasurement_WhenNoSavedDiagnosis()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var cephId = Guid.NewGuid();
        _db.CephAnalyses.Add(new CephAnalysis
        {
            Id = cephId, OrthoCaseId = orthoCaseId, AnalysisDate = new DateOnly(2026, 1, 1),
        });
        // ANB = 6 → Class II; FMA = 30 → Hyperdivergent
        _db.Entry(await _db.CephAnalyses.FindAsync(cephId)).Collection(c => c.Measurements).LoadAsync();
        (await _db.CephAnalyses.FindAsync(cephId))!.Measurements.Add(new CephMeasurement
        {
            AnalysisId = cephId, MeasurementName = "ANB", MeasurementValue = 6m,
        });
        (await _db.CephAnalyses.FindAsync(cephId))!.Measurements.Add(new CephMeasurement
        {
            AnalysisId = cephId, MeasurementName = "FMA", MeasurementValue = 30m,
        });

        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetDiagnosisAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.Id.Should().BeNull("no saved diagnosis → Id is null");
        result.SkeletalClassification.Should().Be("Class II", "ANB > 4");
        result.FacialPattern.Should().Be("Hyperdivergent", "FMA > 28");
        result.ANB.Should().Be(6m);
        result.FMA.Should().Be(30m);
        result.IsApproved.Should().BeFalse();
        result.IsCephSyncOutdated.Should().BeTrue("a ceph exists but diagnosis has no CephSourceAnalysisId");
    }

    [Fact]
    public async Task GetDiagnosisAsync_ReturnsSavedDiagnosis_WithApprovalMetadata()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var doctorId = Guid.NewGuid();
        _db.Doctors.Add(new Doctor { Id = doctorId, Name = "د. عقلان", IsActive = true });

        _db.OrthoDiagnoses.Add(new OrthoDiagnosis
        {
            OrthoCaseId = orthoCaseId,
            SkeletalClassification = "Class I",
            ANB = 2m,
            ApprovedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            ApprovedBy = doctorId,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetDiagnosisAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.SkeletalClassification.Should().Be("Class I");
        result.ANB.Should().Be(2m);
        result.IsApproved.Should().BeTrue();
        result.ApprovedByName.Should().Be("د. عقلان");
        result.ApprovedAt.Should().Be("2026-01-15");
    }

    // ─── GetRetentionAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetRetentionAsync_ReturnsNull_WhenNoRecordExists()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var service = CreateService(_db);

        var result = await service.GetRetentionAsync(orthoCaseId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRetentionAsync_ReturnsRecord_WithVisitsOrderedByVisitDate()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var retentionId = Guid.NewGuid();
        _db.RetentionRecords.Add(new RetentionRecord
        {
            Id = retentionId, OrthoCaseId = orthoCaseId,
            DebondDate = new DateOnly(2026, 1, 1), UpperRetainer = "Hawley", Status = "active",
            Visits =
            {
                new RetentionVisit { VisitDate = new DateOnly(2026, 3, 1), Period = "3mo" },
                new RetentionVisit { VisitDate = new DateOnly(2026, 1, 15), Period = "2wk" },
            },
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetRetentionAsync(orthoCaseId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(retentionId);
        result.DebondDate.Should().Be("2026-01-01");
        result.UpperRetainer.Should().Be("Hawley");
        result.Visits.Should().HaveCount(2);
        result.Visits[0].VisitDate.Should().Be("2026-01-15", "ascending by VisitDate");
        result.Visits[1].VisitDate.Should().Be("2026-03-01");
    }

    // ─── GetRetentionVisitsAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetRetentionVisitsAsync_OrdersByVisitDateDescending()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var retentionId = Guid.NewGuid();
        _db.RetentionRecords.Add(new RetentionRecord
        {
            Id = retentionId, OrthoCaseId = orthoCaseId,
            Visits =
            {
                new RetentionVisit { VisitDate = new DateOnly(2026, 1, 1), Period = "2wk" },
                new RetentionVisit { VisitDate = new DateOnly(2026, 6, 1), Period = "6mo" },
                new RetentionVisit { VisitDate = new DateOnly(2026, 3, 1), Period = "3mo" },
            },
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetRetentionVisitsAsync(orthoCaseId);

        result.Should().HaveCount(3);
        result[0].VisitDate.Should().Be("2026-06-01", "descending by VisitDate");
        result[1].VisitDate.Should().Be("2026-03-01");
        result[2].VisitDate.Should().Be("2026-01-01");
    }

    // ─── GetPhotosAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetPhotosAsync_FiltersByCategory_AndPhase_AndSelectedOnly()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var extraoralId = Guid.NewGuid();
        var intraoralId = Guid.NewGuid();
        var selectedId = Guid.NewGuid();

        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            Id = extraoralId, OrthoCaseId = orthoCaseId, PhotoUrl = "/u/e.jpg",
            PhotoType = "Extraoral", Category = "Extraoral", Subtype = "Profile",
            TreatmentPhase = "Initial", SortOrder = 1,
        });
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            Id = intraoralId, OrthoCaseId = orthoCaseId, PhotoUrl = "/u/i.jpg",
            PhotoType = "Intraoral", Category = "Intraoral", Subtype = "Frontal",
            TreatmentPhase = "Progress", SortOrder = 2,
        });
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            Id = selectedId, OrthoCaseId = orthoCaseId, PhotoUrl = "/u/s.jpg",
            PhotoType = "Extraoral", Category = "Extraoral", Subtype = "FrontalRest",
            TreatmentPhase = "Initial", IsSelectedForReport = true, SortOrder = 3,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);

        // Filter by category only
        var byCategory = await service.GetPhotosAsync(orthoCaseId, "Extraoral", null, null);
        byCategory.Should().HaveCount(2);
        byCategory.Should().OnlyContain(p => p.Category == "Extraoral");

        // Filter by phase only
        var byPhase = await service.GetPhotosAsync(orthoCaseId, null, "Initial", null);
        byPhase.Should().HaveCount(2);
        byPhase.Should().OnlyContain(p => p.TreatmentPhase == "Initial");

        // Filter by selectedOnly
        var selectedOnly = await service.GetPhotosAsync(orthoCaseId, null, null, true);
        selectedOnly.Should().HaveCount(1);
        selectedOnly[0].Id.Should().Be(selectedId);

        // Combined category + phase
        var combined = await service.GetPhotosAsync(orthoCaseId, "Extraoral", "Initial", null);
        combined.Should().HaveCount(2);

        // No filter — all 3
        var all = await service.GetPhotosAsync(orthoCaseId, null, null, null);
        all.Should().HaveCount(3);
        // Ordered by SortOrder ascending
        all[0].SortOrder.Should().Be(1);
        all[1].SortOrder.Should().Be(2);
        all[2].SortOrder.Should().Be(3);
    }

    [Fact]
    public async Task GetPhotosAsync_SetsPreparationStatus_Defaults_WhenNoPreparation()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId, PhotoUrl = "/u/1.jpg", PhotoType = "Extraoral",
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetPhotosAsync(orthoCaseId, null, null, null);

        result.Should().HaveCount(1);
        result[0].PreparationStatus.Should().Be("OriginalUploaded");
        result[0].IsPreparedForReport.Should().BeFalse();
        result[0].PreparedImageUrl.Should().BeNull();
    }

    // ─── GetImagePreparationAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetImagePreparationAsync_ReturnsNull_WhenPhotoNotFound()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var service = CreateService(_db);

        var result = await service.GetImagePreparationAsync(orthoCaseId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetImagePreparationAsync_ReturnsDefaults_WhenNoPreparationRow()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var photoId = Guid.NewGuid();
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            Id = photoId, OrthoCaseId = orthoCaseId, PhotoUrl = "/uploads/original.jpg",
            PhotoType = "Extraoral",
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetImagePreparationAsync(orthoCaseId, photoId);

        result.Should().NotBeNull();
        result!.PhotoId.Should().Be(photoId);
        result.OriginalPhotoUrl.Should().Be("/uploads/original.jpg");
        result.Status.Should().Be("OriginalUploaded");
        result.Zoom.Should().Be(1m);
        result.CropWidth.Should().Be(1m);
        result.CropHeight.Should().Be(1m);
        result.AspectRatio.Should().Be("Original");
        result.PreparedImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetImagePreparationAsync_ReturnsSavedPreparation_WhenExists()
    {
        var (_, orthoCaseId) = await SeedCaseAsync();
        var photoId = Guid.NewGuid();
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            Id = photoId, OrthoCaseId = orthoCaseId, PhotoUrl = "/uploads/original.jpg",
            PhotoType = "Extraoral",
            ImagePreparation = new OrthoImagePreparation
            {
                CropX = 0.1m, CropY = 0.2m, CropWidth = 0.5m, CropHeight = 0.7m,
                Zoom = 1.5m, RotationDegrees = 15, Brightness = 10, Contrast = -5,
                FlipHorizontal = true, AspectRatio = "4:5", Status = "PreparedForReport",
                PreparedImageUrl = "/uploads/prepared.jpg",
                PreparedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            },
        });
        await _db.SaveChangesAsync();

        var service = CreateService(_db);
        var result = await service.GetImagePreparationAsync(orthoCaseId, photoId);

        result.Should().NotBeNull();
        result!.PhotoId.Should().Be(photoId);
        result.CropX.Should().Be(0.1m);
        result.Zoom.Should().Be(1.5m);
        result.AspectRatio.Should().Be("4:5");
        result.Status.Should().Be("PreparedForReport");
        result.PreparedImageUrl.Should().Be("/uploads/prepared.jpg");
        result.FlipHorizontal.Should().BeTrue();
    }
}
