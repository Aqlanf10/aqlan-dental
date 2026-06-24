using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Ortho;
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

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Sprint 3 — Tests for ortho visit edit (PUT) and delete (DELETE) endpoints:
///   - PUT  /api/ortho-cases/{id}/visits/{visitId}  → UpdateVisit
///   - DELETE /api/ortho-cases/{id}/visits/{visitId} → DeleteVisit
///
/// Verifies:
///   - Field updates + linked-Visit sync (CLIN-05) on edit
///   - Soft-delete + linked-Visit unlink (NOT hard-delete — Visit row preserved)
///   - Arabic 404 when the visit is unknown or belongs to a different OrthoCase
///   - Audit log entries on both update and delete
///
/// Mirrors the controller-construction pattern in OrthoCasesControllerCreateAccessTests.
/// </summary>
public class OrthoVisitUpdateDeleteTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoVisitUpdateDeleteTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ortho-visit-edit-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
    }

    public void Dispose() => _db.Dispose();

    private static Mock<ICurrentUserService> AdminCurrentUser()
    {
        var mock = new Mock<ICurrentUserService>();
        mock.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        mock.SetupGet(c => c.Role).Returns(UserRole.Admin);
        mock.SetupGet(c => c.IsAdmin).Returns(true);
        return mock;
    }

    private static Mock<IPatientAccessService> FullAccess()
    {
        var mock = new Mock<IPatientAccessService>();
        mock.SetupGet(p => p.IsDoctor).Returns(false);
        mock.SetupGet(p => p.HasFullAccess).Returns(true);
        mock.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        return mock;
    }

    private static OrthoCasesController BuildController(
        AppDbContext db,
        Mock<ICurrentUserService>? currentUser = null,
        Mock<IPatientAccessService>? patientAccess = null,
        Mock<IAuditService>? audit = null)
    {
        currentUser ??= AdminCurrentUser();
        patientAccess ??= FullAccess();
        audit ??= new Mock<IAuditService>();
        return new OrthoCasesController(
            new OrthoService(db, currentUser.Object),
            db,
            currentUser.Object,
            patientAccess.Object,
            audit.Object);
    }

    private async Task<(Guid caseId, Guid visitId, Guid linkedVisitId, Guid doctorId)> SeedVisitWithLinkedRow()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = $"P-{Guid.NewGuid():N}".Substring(0, 12),
            FirstName = "سليم",
            LastName = "المريض",
            IsActive = true
        };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "ortho_user",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            Role = UserRole.Orthodontist,
            IsActive = true
        };
        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "د. التقويم",
            IsActive = true
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = "OR-VISIT-001",
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            Status = OrthoCaseStatus.Active,
            IsActive = true
        };
        var visitDate = new DateOnly(2025, 1, 15);
        var orthoVisit = new OrthoVisit
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            VisitNumber = 1,
            VisitDate = visitDate,
            VisitType = "Adjustment",
            CurrentStage = "Alignment",
            WireUpper = "0.014 NiTi",
            WireLower = "0.014 NiTi",
            ClinicalNotes = "أول زيارة",
            DoctorId = doctor.Id,
            IsActive = true
        };
        var linkedVisit = new Visit
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            DoctorId = doctor.Id,
            VisitDate = visitDate,
            VisitType = "Adjustment",
            Specialty = Specialty.Orthodontics,
            OrthoCaseId = orthoCase.Id,
            ChiefComplaint = "Adjustment",
            ClinicalNotes = "أول زيارة",
            WireUpper = "0.014 NiTi",
            WireLower = "0.014 NiTi",
            CurrentStage = "Alignment",
            IsActive = true
        };
        _db.AddRange(user, doctor, patient, orthoCase, orthoVisit, linkedVisit);
        await _db.SaveChangesAsync();
        return (orthoCase.Id, orthoVisit.Id, linkedVisit.Id, doctor.Id);
    }

    private static CreateOrthoVisitRequest BuildUpdateRequest(string visitDate, string wireUpper, string wireLower,
        string stage, string notes) => new()
        {
            VisitDate = visitDate,
            VisitType = "Activation",
            WireUpper = wireUpper,
            WireLower = wireLower,
            CurrentStage = stage,
            ClinicalNotes = notes,
            NextAppointmentType = "ضبط"
        };

    private static string ExtractMessage(object? value)
    {
        value.Should().NotBeNull("the 4xx response must carry a payload");
        var prop = value!.GetType().GetProperty("message");
        prop.Should().NotBeNull("the 4xx response must carry a 'message' field (Arabic)");
        return (string)prop!.GetValue(value)!;
    }

    // ── UpdateVisit ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateVisit_UpdatesFields_AndSyncsLinkedVisit()
    {
        var (caseId, visitId, linkedVisitId, _) = await SeedVisitWithLinkedRow();
        var controller = BuildController(_db);

        var req = BuildUpdateRequest(
            visitDate: "2025-01-22",
            wireUpper: "0.018 NiTi",
            wireLower: "0.016 SS",
            stage: "Leveling",
            notes: "تم تغيير السلك العلوي");

        var result = await controller.UpdateVisit(caseId, visitId, req);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var dto = ok.Value.Should().BeAssignableTo<OrthoVisitListDto>().Subject;
        dto.WireUpper.Should().Be("0.018 NiTi");
        dto.WireLower.Should().Be("0.016 SS");
        dto.CurrentStage.Should().Be("Leveling");
        dto.VisitDate.Should().Be("2025-01-22");
        dto.ClinicalNotes.Should().Be("تم تغيير السلك العلوي");

        // OrthoVisit row updated
        var orthoVisit = await _db.OrthoVisits.FindAsync(visitId);
        orthoVisit.Should().NotBeNull();
        orthoVisit!.VisitDate.Should().Be(new DateOnly(2025, 1, 22));
        orthoVisit.WireUpper.Should().Be("0.018 NiTi");
        orthoVisit.WireLower.Should().Be("0.016 SS");
        orthoVisit.CurrentStage.Should().Be("Leveling");
        orthoVisit.IsActive.Should().BeTrue("update does NOT soft-delete");

        // Linked Visit row synced — same row (id preserved), date + wires + stage + notes refreshed
        var linkedVisit = await _db.Visits.FindAsync(linkedVisitId);
        linkedVisit.Should().NotBeNull();
        linkedVisit!.Id.Should().Be(linkedVisitId, "the linked Visit must be UPDATED in place, not duplicated");
        linkedVisit.VisitDate.Should().Be(new DateOnly(2025, 1, 22), "date sync must follow the OrthoVisit");
        linkedVisit.WireUpper.Should().Be("0.018 NiTi");
        linkedVisit.WireLower.Should().Be("0.016 SS");
        linkedVisit.CurrentStage.Should().Be("Leveling");
        linkedVisit.ClinicalNotes.Should().Be("تم تغيير السلك العلوي");
        linkedVisit.OrthoCaseId.Should().Be(caseId, "the link must remain intact after update");
        linkedVisit.IsActive.Should().BeTrue("the linked Visit must NOT be soft-deleted");

        // Case stage mirrored (matches AddVisitAsync behaviour)
        var orthoCase = await _db.OrthoCases.FindAsync(caseId);
        orthoCase!.CurrentStage.Should().Be("Leveling");
    }

    [Fact]
    public async Task UpdateVisit_UnknownVisitId_Returns404_Arabic()
    {
        var (caseId, _, _, _) = await SeedVisitWithLinkedRow();
        var controller = BuildController(_db);

        var req = BuildUpdateRequest("2025-01-22", "0.018 NiTi", "0.016 SS", "Leveling", "ملاحظات");

        var result = await controller.UpdateVisit(caseId, Guid.NewGuid(), req);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value)
            .Should().Contain("زيارة التقويم")
            .And.Contain("غير موجودة");
    }

    [Fact]
    public async Task UpdateVisit_VisitBelongsToDifferentCase_Returns404()
    {
        var (caseA, visitIdA, _, _) = await SeedVisitWithLinkedRow();
        // Second case (different patient) — visitIdA does NOT belong to caseB
        var patientB = new Patient { Id = Guid.NewGuid(), FirstName = "B", LastName = "B", IsActive = true };
        var caseB = new OrthoCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = "OR-VISIT-002",
            PatientId = patientB.Id,
            Status = OrthoCaseStatus.Active,
            IsActive = true
        };
        _db.AddRange(patientB, caseB);
        await _db.SaveChangesAsync();

        var controller = BuildController(_db);
        var req = BuildUpdateRequest("2025-01-22", "0.018 NiTi", "0.016 SS", "Leveling", "ملاحظات");

        var result = await controller.UpdateVisit(caseB.Id, visitIdA, req);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);

        // Original visit on caseA must be UNTOUCHED (no field changes, no link severance)
        var original = await _db.OrthoVisits.FindAsync(visitIdA);
        original!.CurrentStage.Should().Be("Alignment", "the update must not mutate a visit on a different case");
        original.WireUpper.Should().Be("0.014 NiTi");
        original.OrthoCaseId.Should().Be(caseA);
    }

    [Fact]
    public async Task UpdateVisit_AuditsTheChange()
    {
        var (caseId, visitId, _, _) = await SeedVisitWithLinkedRow();
        var auditMock = new Mock<IAuditService>();
        var controller = BuildController(_db, audit: auditMock);

        var req = BuildUpdateRequest("2025-01-22", "0.018 NiTi", "0.016 SS", "Leveling", "ملاحظات");

        await controller.UpdateVisit(caseId, visitId, req);

        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.Update,
                "OrthoVisit",
                visitId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "UpdateVisit must produce exactly one audit LogAsync call");
    }

    // ── DeleteVisit ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteVisit_SoftDeletes_AndNullsLinkedVisitOrthoCaseId()
    {
        var (caseId, visitId, linkedVisitId, _) = await SeedVisitWithLinkedRow();
        var auditMock = new Mock<IAuditService>();
        var controller = BuildController(_db, audit: auditMock);

        var result = await controller.DeleteVisit(caseId, visitId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ExtractMessage(ok.Value).Should().Contain("تم حذف");

        // OrthoVisit soft-deleted (IsActive=false, DeletedAt/By set) — NOT hard-deleted
        var orthoVisit = await _db.OrthoVisits.IgnoreQueryFilters().FirstAsync(v => v.Id == visitId);
        orthoVisit.IsActive.Should().BeFalse("soft-delete only — never hard-delete");
        orthoVisit.DeletedAt.Should().NotBeNull();
        orthoVisit.OrthoCaseId.Should().Be(caseId, "the caseId FK must remain set on the OrthoVisit row");

        // Linked Visit: preserved (NOT hard-deleted) but unlinked (OrthoCaseId = null)
        var linkedVisit = await _db.Visits.FindAsync(linkedVisitId);
        linkedVisit.Should().NotBeNull("the linked Visit row must be PRESERVED — it may carry payments");
        linkedVisit!.IsActive.Should().BeTrue("the Visit row itself is not soft-deleted");
        linkedVisit.OrthoCaseId.Should().BeNull("the link to the OrthoCase must be severed (unlinked)");

        // Audit log entry for the delete
        auditMock.Verify(
            a => a.LogAsync(
                AuditAction.Delete,
                "OrthoVisit",
                visitId,
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            Times.Once,
            "DeleteVisit must produce exactly one audit LogAsync call");
    }

    [Fact]
    public async Task DeleteVisit_UnknownId_Returns404_Arabic()
    {
        var (caseId, _, _, _) = await SeedVisitWithLinkedRow();
        var controller = BuildController(_db);

        var result = await controller.DeleteVisit(caseId, Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);
        ExtractMessage(notFound.Value)
            .Should().Contain("زيارة التقويم")
            .And.Contain("غير موجودة");
    }

    [Fact]
    public async Task DeleteVisit_VisitBelongsToDifferentCase_Returns404()
    {
        var (_, visitIdA, _, _) = await SeedVisitWithLinkedRow();
        // Different case
        var patientB = new Patient { Id = Guid.NewGuid(), FirstName = "B", LastName = "B", IsActive = true };
        var caseB = new OrthoCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = "OR-VISIT-003",
            PatientId = patientB.Id,
            Status = OrthoCaseStatus.Active,
            IsActive = true
        };
        _db.AddRange(patientB, caseB);
        await _db.SaveChangesAsync();

        var controller = BuildController(_db);
        var result = await controller.DeleteVisit(caseB.Id, visitIdA);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(404);

        // Visit A still active (no deletion)
        var visitA = await _db.OrthoVisits.IgnoreQueryFilters().FirstAsync(v => v.Id == visitIdA);
        visitA.IsActive.Should().BeTrue("a delete on a different case must NOT touch this visit");
    }
}
