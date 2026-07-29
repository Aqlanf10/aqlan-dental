using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.OrthoSurgical;

/// <summary>
/// Sprint A9 — unit tests for the Surgical VTO (Visual Treatment Objective) endpoints on
/// <see cref="OrthoSurgicalCasesController"/>: the strict approved-CephAnalysis creation
/// gate, the predicted-measurement computation (documented geometric relationships),
/// explicit orthodontist-only approval, and soft-delete. Mirrors the
/// <c>OrthoSurgicalCollaborationTests</c> setup (EF InMemory + Moq, <c>CreateDb</c>,
/// <c>SeedCase</c>, <c>Build</c> with <c>IPatientAccessService</c> mock where IsDoctor=false).
/// </summary>
public class OrthoSurgicalVtoTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-vto-{Guid.NewGuid()}")
            .Options);

    private static Mock<ICurrentUserService> User(UserRole role, Guid? userId = null)
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.UserId).Returns(userId ?? Guid.NewGuid());
        m.SetupGet(c => c.Role).Returns(role);
        m.SetupGet(c => c.IsAdmin).Returns(role == UserRole.Admin);
        m.SetupGet(c => c.IsAuthenticated).Returns(true);
        return m;
    }

    private static OrthoSurgicalCasesController Build(AppDbContext db, Mock<ICurrentUserService> user, Mock<IAuditService>? auditMock = null)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(p => p.IsDoctor).Returns(false);
        access.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var logger = new Mock<ILogger<OrthoSurgicalCasesController>>().Object;
        return new OrthoSurgicalCasesController(db, logger, access.Object, (auditMock ?? new Mock<IAuditService>()).Object, user.Object);
    }

    /// <summary>
    /// Seeds a full OrthoSurgicalCase with an approved CephAnalysis carrying the canonical
    /// Steiner measurements (SNA/SNB/ANB/Wits/Overjet) — the baseline the VTO computation
    /// reads from. Returns the case + the baseline measurements for assertions.
    /// </summary>
    private static async Task<(OrthoSurgicalCase c, CephAnalysis ceph, decimal SNA, decimal SNB, decimal Wits, decimal Overjet)>
        SeedCaseWithApprovedCephAsync(AppDbContext db, bool cephApproved = true)
    {
        var patient = new Patient { PatientNumber = "P-VTO", FirstName = "سامي", LastName = "المريض", IsActive = true };
        db.Patients.Add(patient);
        var orthoCase = new OrthoCase { PatientId = patient.Id, CaseNumber = "OC-VTO-1", IsActive = true };
        db.OrthoCases.Add(orthoCase);

        const decimal sna = 80m, snb = 76m, wits = -2m, overjet = 4m;
        var ceph = new CephAnalysis
        {
            OrthoCaseId = orthoCase.Id,
            AnalysisType = "Steiner",
            IsApproved = cephApproved,
            IsActive = true
        };
        db.CephAnalyses.Add(ceph);
        await db.SaveChangesAsync();

        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "SNA", MeasurementValue = sna, Unit = "°", IsActive = true },
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "SNB", MeasurementValue = snb, Unit = "°", IsActive = true },
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "Wits", MeasurementValue = wits, Unit = "mm", IsActive = true },
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "Overjet", MeasurementValue = overjet, Unit = "mm", IsActive = true });

        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-VTO-001",
            PatientId = patient.Id,
            OrthoCaseId = orthoCase.Id,
            CephAnalysisId = cephApproved ? ceph.Id : null, // caller controls linkage when ceph is unapproved
            Status = OrthoSurgicalStatus.VtoDraft,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        await db.SaveChangesAsync();
        return (c, ceph, sna, snb, wits, overjet);
    }

    private static void GivePermissions(AppDbContext db, UserRole role)
    {
        if (role == UserRole.Admin) return;
        db.RolePermissions.Add(new RolePermission
        {
            Role = role.ToString(),
            Resource = "ortho_surgical",
            CanView = true, CanCreate = true, CanEdit = true, CanApprove = true
        });
        db.SaveChanges();
    }

    // ── 1. Create with approved CephAnalysis — succeeds ─────────────────────────────
    [Fact]
    public async Task CreateVto_WithApprovedCephAnalysis_Succeeds()
    {
        await using var db = CreateDb();
        var (c, _, _, _, _, _) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest
        {
            MaxillaMoveMm = 4m,
            MandibleMoveMm = 6m,
            ChinMoveMm = 3m,
            RotationDegree = 1.5m,
            Notes = "Le Fort I + BSSO"
        });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.OrthoSurgicalVtos.SingleAsync(v => v.OrthoSurgicalCaseId == c.Id);
        saved.CephAnalysisId.Should().Be(c.CephAnalysisId);
        saved.MaxillaMoveMm.Should().Be(4m);
        saved.MandibleMoveMm.Should().Be(6m);
        saved.ChinMoveMm.Should().Be(3m);
        saved.RotationDegree.Should().Be(1.5m);
        saved.Notes.Should().Be("Le Fort I + BSSO");
        saved.CreatedBy.Should().NotBeNull();
        // CRITICAL: never auto-approved on create.
        saved.IsApprovedByOrthodontist.Should().BeFalse();
        saved.ApprovedAt.Should().BeNull();
    }

    // ── 2. Create without any CephAnalysisId on the case — 400 Arabic ───────────────
    [Fact]
    public async Task CreateVto_WithoutCephAnalysis_Returns400_Arabic()
    {
        await using var db = CreateDb();
        // Seed a case with NO CephAnalysisId linked.
        var patient = new Patient { PatientNumber = "P-VTO2", FirstName = "كريم", LastName = "المريض", IsActive = true };
        db.Patients.Add(patient);
        var orthoCase = new OrthoCase { PatientId = patient.Id, CaseNumber = "OC-VTO-2", IsActive = true };
        db.OrthoCases.Add(orthoCase);
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-VTO-002",
            PatientId = patient.Id,
            OrthoCaseId = orthoCase.Id,
            CephAnalysisId = null, // ← no linked analysis
            Status = OrthoSurgicalStatus.VtoDraft,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        await db.SaveChangesAsync();
        var controller = Build(db, User(UserRole.Admin));

        var result = await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 3m });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var msg = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value) as string;
        msg.Should().NotBeNullOrEmpty();
        msg.Should().Contain("محاكاة"); // Arabic-only message — no English leakage.
        (await db.OrthoSurgicalVtos.CountAsync()).Should().Be(0);
    }

    // ── 3. Create with unapproved CephAnalysis — 400 Arabic ─────────────────────────
    [Fact]
    public async Task CreateVto_WithUnapprovedCephAnalysis_Returns400_Arabic()
    {
        await using var db = CreateDb();
        var patient = new Patient { PatientNumber = "P-VTO3", FirstName = "ليلى", LastName = "المريضة", IsActive = true };
        db.Patients.Add(patient);
        var orthoCase = new OrthoCase { PatientId = patient.Id, CaseNumber = "OC-VTO-3", IsActive = true };
        db.OrthoCases.Add(orthoCase);
        var ceph = new CephAnalysis { OrthoCaseId = orthoCase.Id, AnalysisType = "Steiner", IsApproved = false, IsActive = true };
        db.CephAnalyses.Add(ceph);
        await db.SaveChangesAsync();
        var c = new OrthoSurgicalCase
        {
            CaseNumber = "OS-VTO-003",
            PatientId = patient.Id,
            OrthoCaseId = orthoCase.Id,
            CephAnalysisId = ceph.Id, // ← linked but NOT approved
            Status = OrthoSurgicalStatus.VtoDraft,
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(c);
        await db.SaveChangesAsync();

        var controller = Build(db, User(UserRole.Admin));
        var result = await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 3m });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var msg = bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value) as string;
        msg.Should().NotBeNullOrEmpty();
        // Same Arabic message as the no-ceph case (the strict gate uses one canonical message).
        msg.Should().Contain("معتمد");
        (await db.OrthoSurgicalVtos.CountAsync()).Should().Be(0);
    }

    // ── 4. Predicted measurements are computed from documented relationships ───────
    [Fact]
    public async Task CreateVto_ComputesPredictedMeasurements()
    {
        await using var db = CreateDb();
        var (c, _, sna, snb, wits, overjet) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        var controller = Build(db, User(UserRole.Admin));

        // +4mm maxilla, +6mm mandible, 2° rotation, +3mm chin (chin/rotation do NOT shift
        // SNA/SNB/ANB/Wits/Overjet — they're stored for the record only).
        var result = await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest
        {
            MaxillaMoveMm = 4m,
            MandibleMoveMm = 6m,
            ChinMoveMm = 3m,
            RotationDegree = 2m
        });

        result.Should().BeOfType<OkObjectResult>();
        var saved = await db.OrthoSurgicalVtos.SingleAsync(v => v.OrthoSurgicalCaseId == c.Id);

        // SNA: +1° per 2mm of maxilla → +4mm → +2°. Expected = 80 + 2 = 82.
        saved.PredictedSNA.Should().Be(sna + 2m);
        // SNB: +1° per 2mm of mandible → +6mm → +3°. Expected = 76 + 3 = 79.
        saved.PredictedSNB.Should().Be(snb + 3m);
        // ANB = SNA − SNB = 82 − 79 = 3.
        saved.PredictedANB.Should().Be((sna + 2m) - (snb + 3m));
        // Wits: +0.5mm per 1mm maxilla, −0.5mm per 1mm mandible → +2 − 3 = −1. Expected = -2 + (-1) = -3.
        saved.PredictedWits.Should().Be(wits + (4m * 0.5m) - (6m * 0.5m));
        // Overjet: decreases by maxilla+mandible movement → 4 − 4 − 6 = -6.
        saved.PredictedOverjet.Should().Be(overjet - 4m - 6m);
    }

    // ── 5. Approve by orthodontist — succeeds, sets explicit sign-off ───────────────
    [Fact]
    public async Task ApproveVto_ByOrthodontist_Succeeds()
    {
        await using var db = CreateDb();
        var (c, _, _, _, _, _) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        GivePermissions(db, UserRole.Orthodontist);
        var orthoUserId = Guid.NewGuid();
        var auditMock = new Mock<IAuditService>();

        var createController = Build(db, User(UserRole.Orthodontist, orthoUserId), auditMock);
        var createResult = await createController.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 4m });
        createResult.Should().BeOfType<OkObjectResult>();
        var createdVto = await db.OrthoSurgicalVtos.SingleAsync(v => v.OrthoSurgicalCaseId == c.Id);
        createdVto.IsApprovedByOrthodontist.Should().BeFalse("approval must be explicit, never auto-set on create");

        var approveResult = await createController.ApproveVto(c.Id, createdVto.Id);

        approveResult.Should().BeOfType<OkObjectResult>();
        var after = await db.OrthoSurgicalVtos.FindAsync(createdVto.Id);
        after!.IsApprovedByOrthodontist.Should().BeTrue();
        after.ApprovedAt.Should().NotBeNull();
        after.ApprovedByUserId.Should().Be(orthoUserId);

        auditMock.Verify(a => a.LogAsync(AuditAction.Approve, "OrthoSurgicalVto", createdVto.Id,
            It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()), Times.Once);
    }

    // ── 6. Approve by non-orthodontist (OralSurgeon) — 403 ──────────────────────────
    [Fact]
    public async Task ApproveVto_ByNonOrthodontist_Returns403()
    {
        await using var db = CreateDb();
        var (c, _, _, _, _, _) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        GivePermissions(db, UserRole.OralSurgeon);

        // Surgeon creates the VTO (edit permission is enough for create).
        var surgeonController = Build(db, User(UserRole.OralSurgeon));
        var createResult = await surgeonController.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 4m });
        createResult.Should().BeOfType<OkObjectResult>();
        var createdVto = await db.OrthoSurgicalVtos.SingleAsync(v => v.OrthoSurgicalCaseId == c.Id);

        // Surgeon attempts to approve — must be denied (orthodontist-only sign-off).
        var approveResult = await surgeonController.ApproveVto(c.Id, createdVto.Id);

        approveResult.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(403);
        var after = await db.OrthoSurgicalVtos.FindAsync(createdVto.Id);
        after!.IsApprovedByOrthodontist.Should().BeFalse("the surgeon must not be able to flip the orthodontist sign-off");
    }

    // ── 7. Delete — soft-deletes (IsActive=false, DeletedAt set), not hard-delete ───
    [Fact]
    public async Task DeleteVto_SoftDeletes()
    {
        await using var db = CreateDb();
        var (c, _, _, _, _, _) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        var controller = Build(db, User(UserRole.Admin));
        await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 4m });
        var createdVto = await db.OrthoSurgicalVtos.SingleAsync(v => v.OrthoSurgicalCaseId == c.Id);
        // Bypass the global soft-delete query filter to verify the row stays physically present.
        var beforeId = createdVto.Id;

        var result = await controller.DeleteVto(c.Id, beforeId);

        result.Should().BeOfType<OkObjectResult>();
        // The row is still physically present (soft-delete), with IsActive=false.
        var raw = await db.OrthoSurgicalVtos.IgnoreQueryFilters().FirstAsync(v => v.Id == beforeId);
        raw.IsActive.Should().BeFalse();
        raw.DeletedAt.Should().NotBeNull();
        raw.DeletedBy.Should().NotBeNull();
        // And the filtered collection (the public GET) no longer returns it.
        var listResult = await controller.GetVtos(c.Id);
        var ok = listResult.Should().BeOfType<OkObjectResult>().Subject;
        var data = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        data.Cast<object>().Should().BeEmpty();
    }

    // ── Bonus: GET list returns disclaimer + scenarios newest-first ─────────────────
    [Fact]
    public async Task GetVtos_ReturnsDisclaimerAndScenarios()
    {
        await using var db = CreateDb();
        var (c, _, _, _, _, _) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        var controller = Build(db, User(UserRole.Admin));
        await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 2m });
        await Task.Delay(20); // ensure CreatedAt ordering is meaningful
        await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 4m });

        var result = await controller.GetVtos(c.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var disclaimer = ok.Value!.GetType().GetProperty("disclaimer")!.GetValue(ok.Value) as string;
        disclaimer.Should().NotBeNullOrEmpty("the mandatory Arabic disclaimer must accompany every VTO response");
        disclaimer.Should().Contain("محاكاة");

        var data = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("data")!.GetValue(ok.Value)!;
        var items = data.Cast<object>().ToList();
        items.Should().HaveCount(2);
        // Every item also carries the disclaimer (so the frontend can render it on each card).
        items[0].GetType().GetProperty("Disclaimer")!.GetValue(items[0]).Should().Be(disclaimer);
        items[1].GetType().GetProperty("Disclaimer")!.GetValue(items[1]).Should().Be(disclaimer);
    }

    // ── Bonus: update recomputes predictions and refuses to mutate an approved VTO ──
    [Fact]
    public async Task UpdateVto_RecomputesPredictions_And_LocksWhenApproved()
    {
        await using var db = CreateDb();
        var (c, _, _, _, _, _) = await SeedCaseWithApprovedCephAsync(db, cephApproved: true);
        var controller = Build(db, User(UserRole.Admin));
        await controller.CreateVto(c.Id, new CreateOrthoSurgicalVtoRequest { MaxillaMoveMm = 4m });
        var vto = await db.OrthoSurgicalVtos.SingleAsync(v => v.OrthoSurgicalCaseId == c.Id);

        var updateResult = await controller.UpdateVto(c.Id, vto.Id, new UpdateOrthoSurgicalVtoRequest { MaxillaMoveMm = 8m });
        updateResult.Should().BeOfType<OkObjectResult>();
        var after = await db.OrthoSurgicalVtos.FindAsync(vto.Id);
        // +8mm maxilla → SNA +4° (vs +2° before). Recomputed correctly.
        after!.PredictedSNA.Should().Be(80m + 4m);

        // Approve then attempt another update — must be rejected (approved = immutable).
        await controller.ApproveVto(c.Id, vto.Id);
        var secondUpdate = await controller.UpdateVto(c.Id, vto.Id, new UpdateOrthoSurgicalVtoRequest { MaxillaMoveMm = 2m });
        secondUpdate.Should().BeOfType<BadRequestObjectResult>();
    }
}
