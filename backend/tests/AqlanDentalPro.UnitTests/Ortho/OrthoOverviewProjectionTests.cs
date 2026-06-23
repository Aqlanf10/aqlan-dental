using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// CLIN-15 — verifies the rewritten GetOverview server-side projection returns the
/// exact same JSON shape as before (frontend useOrthoOverview depends on every
/// field) and that "latest" subqueries correctly pick the newest row in SQL.
///
/// These tests assert against the *response payload*, not the SQL — so they keep
/// guarding the contract regardless of whether the implementation uses Includes
/// or a projection.
/// </summary>
public class OrthoOverviewProjectionTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly API.Controllers.OrthoCasesController _controller;

    public OrthoOverviewProjectionTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var currentUser = new Mock<Application.Interfaces.Services.ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        var patientAccess = new Mock<Application.Interfaces.Services.IPatientAccessService>();
        patientAccess.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var audit = new Mock<Application.Interfaces.Services.IAuditService>();
        _controller = new API.Controllers.OrthoCasesController(
            new Application.Services.OrthoService(_db, currentUser.Object), _db, currentUser.Object, patientAccess.Object, audit.Object);
    }

    public void Dispose() => _db.Dispose();

    private static string? GetMessage(IActionResult result)
    {
        var value = (result as ObjectResult)?.Value;
        return value?.GetType().GetProperty("message")?.GetValue(value) as string;
    }

    /// <summary>
    /// AppDbContext.SaveChangesAsync override unconditionally resets CreatedAt/UpdatedAt on
    /// Added entities to DateTime.UtcNow, so we can't seed distinct timestamps through the
    /// normal Add path. This helper re-sets the timestamp post-save (Modified state — the
    /// override touches only UpdatedAt, preserving our explicit CreatedAt).
    /// </summary>
    private async Task BackdateCreatedAtAsync<TEntity>(Guid id, DateTime createdAt) where TEntity : BaseEntity
    {
        var entity = await _db.Set<TEntity>().FindAsync(id);
        entity!.CreatedAt = createdAt;
        await _db.SaveChangesAsync();
    }

    private async Task<(Guid patientId, Guid orthoCaseId, Guid cephId, Guid profileId, Guid frontalId, Guid contractId)> SeedFullCaseAsync()
    {
        var patientId = Guid.NewGuid();
        var doctorId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        var cephId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var frontalId = Guid.NewGuid();
        var contractId = Guid.NewGuid();

        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.Doctors.Add(new Doctor { Id = doctorId, Name = "Dr. Ortho", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase
        {
            Id = orthoCaseId,
            PatientId = patientId,
            DoctorId = doctorId,
            CaseNumber = "ORT-CLIN15-001",
            IsActive = true,
        });

        // Latest approved treatment plan (Plan A)
        _db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            OrthoCaseId = orthoCaseId, PlanLabel = "A", IsApproved = true,
            ApprovedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
        });
        // An older plan (Plan B) — must NOT be picked as "latest"
        var olderPlanId = Guid.NewGuid();
        _db.TreatmentPlans.Add(new AqlanDentalPro.Domain.Entities.TreatmentPlan
        {
            Id = olderPlanId, OrthoCaseId = orthoCaseId, PlanLabel = "B", IsApproved = false,
        });

        // A completed + a pending stage
        _db.TreatmentStages.Add(new TreatmentStage { OrthoCaseId = orthoCaseId, StageName = "Alignment", Status = "completed" });
        _db.TreatmentStages.Add(new TreatmentStage { OrthoCaseId = orthoCaseId, StageName = "Finishing", Status = "pending" });

        // Two visits — the one with the later VisitDate must be picked
        _db.OrthoVisits.Add(new OrthoVisit
        {
            OrthoCaseId = orthoCaseId, VisitDate = new DateOnly(2026, 1, 10), VisitNumber = 1,
        });
        _db.OrthoVisits.Add(new OrthoVisit
        {
            OrthoCaseId = orthoCaseId, VisitDate = new DateOnly(2026, 2, 20), VisitNumber = 2,
            NextAppointmentDate = new DateOnly(2026, 3, 15),
        });

        // Clinical exam → HasClinicalExam = true
        _db.OrthoClinicalExams.Add(new OrthoClinicalExam { OrthoCaseId = orthoCaseId, ExamDate = new DateOnly(2026, 1, 5) });

        // Two problem list items → ProblemsCount = 2
        _db.ProblemListItems.Add(new ProblemListItem { OrthoCaseId = orthoCaseId, Category = "Skeletal", Description = "Class II", SortOrder = 1 });
        _db.ProblemListItems.Add(new ProblemListItem { OrthoCaseId = orthoCaseId, Category = "Dental", Description = "Crowding", SortOrder = 2 });

        // Two photos → PhotosCount = 2; tagged so auto-derive yields ExtraoralFrontal + ExtraoralProfile
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId, PhotoUrl = "/u/1.jpg", PhotoType = "Extraoral",
            Category = "Extraoral", Subtype = "FrontalRest",
        });
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId, PhotoUrl = "/u/2.jpg", PhotoType = "Extraoral",
            Category = "Extraoral", Subtype = "Profile",
        });

        // Ceph analyses with distinct AnalysisDate values so the latest is deterministic
        // (AppDbContext SaveChanges override clobbers UpdatedAt on Add, so we rely on AnalysisDate).
        _db.CephAnalyses.Add(new CephAnalysis
        {
            Id = cephId, OrthoCaseId = orthoCaseId, AnalysisDate = new DateOnly(2026, 2, 1),
        });
        _db.CephAnalyses.Add(new CephAnalysis
        {
            OrthoCaseId = orthoCaseId, AnalysisDate = new DateOnly(2026, 1, 1),
        });

        // Profile + frontal photo analyses (CreatedAt backdated post-save below)
        _db.PhotoAnalyses.Add(new PhotoAnalysis
        {
            Id = profileId, OrthoCaseId = orthoCaseId, ViewType = "profile", ImageFileUrl = "/u/p.jpg",
        });
        _db.PhotoAnalyses.Add(new PhotoAnalysis
        {
            Id = frontalId, OrthoCaseId = orthoCaseId, ViewType = "frontal", ImageFileUrl = "/u/f.jpg",
        });

        // Approved diagnosis referencing the ceph + both photo analyses
        _db.OrthoDiagnoses.Add(new OrthoDiagnosis
        {
            OrthoCaseId = orthoCaseId,
            ApprovedAt = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc),
            CephSourceAnalysisId = cephId,
            CephSyncedAt = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc),
            ProfileSourceAnalysisId = profileId,
            FrontalSourceAnalysisId = frontalId,
            PhotoAnalysisSyncedAt = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc),
        });

        // Contract explicitly linked to this case with an active payment
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
        _db.Payments.Add(new Payment
        {
            ContractId = contractId, Amount = 50_000, IsActive = true,
        });

        await _db.SaveChangesAsync();

        // Backdate the older plan + photo analyses so the OrderByDescending(CreatedAt) is
        // deterministic (the SaveChangesAsync override sets CreatedAt to DateTime.UtcNow on Add).
        await BackdateCreatedAtAsync<AqlanDentalPro.Domain.Entities.TreatmentPlan>(olderPlanId, new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));
        await BackdateCreatedAtAsync<PhotoAnalysis>(profileId, new DateTime(2026, 2, 5, 0, 0, 0, DateTimeKind.Utc));
        await BackdateCreatedAtAsync<PhotoAnalysis>(frontalId, new DateTime(2026, 2, 6, 0, 0, 0, DateTimeKind.Utc));

        return (patientId, orthoCaseId, cephId, profileId, frontalId, contractId);
    }

    [Fact]
    public async Task GetOverview_ReturnsCorrectShape_ForExistingCase()
    {
        var (_, orthoCaseId, cephId, profileId, frontalId, contractId) = await SeedFullCaseAsync();

        var result = await _controller.GetOverview(orthoCaseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        // Helper accessors
        T Get<T>(string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
        T? GetNullable<T>(string name) where T : struct => (T?)value.GetType().GetProperty(name)!.GetValue(value);
        object? GetRef(string name) => value.GetType().GetProperty(name)!.GetValue(value);

        // Flags + counts
        Get<bool>("HasClinicalExam").Should().BeTrue();
        Get<int>("ProblemsCount").Should().Be(2);
        Get<bool>("HasDiagnosis").Should().BeTrue();
        Get<bool>("IsDiagnosisApproved").Should().BeTrue();
        Get<bool>("HasTreatmentPlan").Should().BeTrue();
        Get<bool>("IsTreatmentPlanApproved").Should().BeTrue("latest plan (Plan A) is approved");
        Get<int>("TreatmentPlansCount").Should().Be(2);
        Get<int>("CompletedStages").Should().Be(1);
        Get<int>("TotalStages").Should().Be(2);
        Get<int>("VisitsCount").Should().Be(2);
        Get<int>("PhotosCount").Should().Be(2);
        Get<int>("CephAnalysesCount").Should().Be(2);
        Get<int>("PhotoAnalysesCount").Should().Be(2);

        // Latest ceph
        GetRef("LatestCephAnalysisId").Should().Be(cephId);
        Get<string>("LatestCephAnalysisDate").Should().Be("2026-02-01");

        // Diagnosis sync flags — diagnosis references the latest analyses, so not outdated
        GetNullable<DateTime>("CephDiagnosisSyncedAt").Should().NotBeNull();
        Get<bool>("IsCephDiagnosisOutdated").Should().BeFalse();
        GetRef("LatestProfileAnalysisId").Should().Be(profileId);
        Get<string>("LatestProfileAnalysisDate").Should().Be("2026-02-05");
        GetRef("LatestFrontalAnalysisId").Should().Be(frontalId);
        Get<string>("LatestFrontalAnalysisDate").Should().Be("2026-02-06");
        GetNullable<DateTime>("PhotoAnalysisSyncedAt").Should().NotBeNull();
        Get<bool>("IsPhotoAnalysisSyncOutdated").Should().BeFalse();

        // No retention record seeded
        Get<bool>("HasRetention").Should().BeFalse();

        // Checklist auto-derive (no RecordsChecklist saved):
        //   ExtraoralFrontal (FrontalRest tag) + ExtraoralProfile (Profile tag) +
        //   LateralCeph (hasCeph=true) + Contract (linked) = 4 items
        Get<int>("ChecklistCompleted").Should().Be(4);
        Get<int>("ChecklistTotal").Should().Be(14);

        // Contract — 200_000 - 20_000 = 180_000 total; 50_000 paid; 130_000 remaining
        GetRef("ContractId").Should().Be(contractId);
        Get<decimal>("ContractTotal").Should().Be(180_000m);
        Get<decimal>("ContractPaid").Should().Be(50_000m);
        Get<decimal>("ContractRemaining").Should().Be(130_000m);

        // Latest visit was 2026-02-20 with next appointment 2026-03-15
        Get<string>("LatestVisitDate").Should().Be("2026-02-20");
        Get<string>("NextAppointmentDate").Should().Be("2026-03-15");
    }

    [Fact]
    public async Task GetOverview_Returns404_ForMissingCase()
    {
        var result = await _controller.GetOverview(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        GetMessage(notFound).Should().Be("الحالة غير موجودة");
    }

    [Fact]
    public async Task GetOverview_PicksLatestVisit()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Test", LastName = "Patient", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase { Id = orthoCaseId, PatientId = patientId, CaseNumber = "ORT-LV-001", IsActive = true });

        // Three visits with distinct dates — middle one inserted last to make sure ordering
        // is by VisitDate (not insertion order). Latest = 2026-03-10.
        _db.OrthoVisits.Add(new OrthoVisit
        {
            OrthoCaseId = orthoCaseId, VisitDate = new DateOnly(2026, 1, 10), VisitNumber = 1,
        });
        _db.OrthoVisits.Add(new OrthoVisit
        {
            OrthoCaseId = orthoCaseId, VisitDate = new DateOnly(2026, 3, 10), VisitNumber = 3,
            NextAppointmentDate = new DateOnly(2026, 4, 1),
        });
        _db.OrthoVisits.Add(new OrthoVisit
        {
            OrthoCaseId = orthoCaseId, VisitDate = new DateOnly(2026, 2, 15), VisitNumber = 2,
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetOverview(orthoCaseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        string LatestVisitDate() => (string)value.GetType().GetProperty("LatestVisitDate")!.GetValue(value)!;
        string? NextAppointmentDate() => (string?)value.GetType().GetProperty("NextAppointmentDate")!.GetValue(value);

        LatestVisitDate().Should().Be("2026-03-10", "the visit with the latest VisitDate must be picked regardless of insertion order");
        NextAppointmentDate().Should().Be("2026-04-01");
    }

    [Fact]
    public async Task GetOverview_PicksLatestCeph_ByAnalysisDate()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Ceph", LastName = "Case", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase { Id = orthoCaseId, PatientId = patientId, CaseNumber = "ORT-LC-001", IsActive = true });

        // Two cephs with distinct AnalysisDate values — later one must win.
        var latestId = Guid.NewGuid();
        _db.CephAnalyses.Add(new CephAnalysis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCaseId, AnalysisDate = new DateOnly(2026, 4, 1),
        });
        _db.CephAnalyses.Add(new CephAnalysis
        {
            Id = latestId, OrthoCaseId = orthoCaseId, AnalysisDate = new DateOnly(2026, 6, 1),
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetOverview(orthoCaseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        var latestCephId = value.GetType().GetProperty("LatestCephAnalysisId")!.GetValue(value);
        latestCephId.Should().Be(latestId);
    }

    [Fact]
    public async Task GetOverview_OldDiagnosisFlagsOutdated_WhenCephNewerThanSyncedId()
    {
        var (_, orthoCaseId, _, _, _, _) = await SeedFullCaseAsync();

        // Add an even newer ceph analysis — diagnosis still references the older cephId.
        var newerCephId = Guid.NewGuid();
        _db.CephAnalyses.Add(new CephAnalysis
        {
            Id = newerCephId, OrthoCaseId = orthoCaseId, AnalysisDate = new DateOnly(2026, 6, 1),
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetOverview(orthoCaseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        bool IsOutdated() => (bool)value.GetType().GetProperty("IsCephDiagnosisOutdated")!.GetValue(value)!;
        object? LatestId() => value.GetType().GetProperty("LatestCephAnalysisId")!.GetValue(value);

        LatestId().Should().Be(newerCephId);
        IsOutdated().Should().BeTrue("diagnosis still references the older cephId");
    }

    [Fact]
    public async Task GetOverview_AutoDerivesChecklist_WhenNoSavedChecklist()
    {
        // No RecordsChecklist row — projection's Checklist property will be null, and the
        // controller falls through to the auto-derive path (OrthoPhotoRecords helpers).
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Chk", LastName = "Case", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase { Id = orthoCaseId, PatientId = patientId, CaseNumber = "ORT-CK-001", IsActive = true });
        _db.OrthoClinicalPhotos.Add(new OrthoClinicalPhoto
        {
            OrthoCaseId = orthoCaseId, PhotoUrl = "/u/1.jpg", PhotoType = "Extraoral",
            Category = "Extraoral", Subtype = "FrontalRest",
        });
        await _db.SaveChangesAsync();

        var result = await _controller.GetOverview(orthoCaseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        int Completed() => (int)value.GetType().GetProperty("ChecklistCompleted")!.GetValue(value)!;
        int Total() => (int)value.GetType().GetProperty("ChecklistTotal")!.GetValue(value)!;

        Completed().Should().Be(1, "one tagged ExtraoralFrontal photo should auto-derive exactly one checklist item");
        Total().Should().Be(14);
    }

    [Fact]
    public async Task GetOverview_UsesSavedChecklist_WhenPresent()
    {
        var patientId = Guid.NewGuid();
        var orthoCaseId = Guid.NewGuid();
        _db.Patients.Add(new Patient { Id = patientId, FirstName = "Chk", LastName = "Saved", IsActive = true });
        _db.OrthoCases.Add(new OrthoCase { Id = orthoCaseId, PatientId = patientId, CaseNumber = "ORT-CK-002", IsActive = true });
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

        var result = await _controller.GetOverview(orthoCaseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = ok.Value!;
        int Completed() => (int)value.GetType().GetProperty("ChecklistCompleted")!.GetValue(value)!;
        Completed().Should().Be(6, "saved checklist counts exactly 6 true items regardless of photos");
    }
}
