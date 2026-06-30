using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ortho;

/// <summary>
/// Sprint 8 — coverage for the previously-untested extraction-decision endpoints on
/// OrthoCasesController:
///   - GET /api/ortho-cases/{id}/extraction-decision  → GetExtractionDecision
///   - PUT /api/ortho-cases/{id}/extraction-decision  → UpsertExtractionDecision
///
/// Verifies: null when none exists; Arabic 404 for an unknown case; upsert creates a
/// single row and mirrors the value to OrthoCase.ExtractionDecisionValue; a second
/// upsert updates the same row (no duplicate); GET returns the saved decision.
///
/// Mirrors the controller-construction pattern in OrthoVisitUpdateDeleteTests.
/// </summary>
public class OrthoExtractionDecisionTests : IDisposable
{
    private readonly AppDbContext _db;

    public OrthoExtractionDecisionTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ortho-extraction-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);
    }

    public void Dispose() => _db.Dispose();

    private static OrthoCasesController BuildController(AppDbContext db)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Admin);
        currentUser.SetupGet(c => c.IsAdmin).Returns(true);

        var patientAccess = new Mock<IPatientAccessService>();
        patientAccess.SetupGet(p => p.IsDoctor).Returns(false);
        patientAccess.SetupGet(p => p.HasFullAccess).Returns(true);
        patientAccess.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);

        return new OrthoCasesController(
            new OrthoService(db, currentUser.Object),
            db,
            currentUser.Object,
            patientAccess.Object,
            new Mock<IAuditService>().Object,
            new OrthoCaseQueryService(db, NullLogger<OrthoCaseQueryService>.Instance));
    }

    private async Task<Guid> SeedCaseAsync()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            PatientNumber = $"P-{Guid.NewGuid():N}"[..12],
            FirstName = "سالم",
            LastName = "المريض",
            IsActive = true,
        };
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = patient.Id, IsActive = true };
        _db.Patients.Add(patient);
        _db.OrthoCases.Add(orthoCase);
        await _db.SaveChangesAsync();
        return orthoCase.Id;
    }

    private static string? GetProp(object value, string name) =>
        value.GetType().GetProperty(name)?.GetValue(value)?.ToString();

    [Fact]
    public async Task GetExtractionDecision_WhenNone_ReturnsNull()
    {
        var caseId = await SeedCaseAsync();
        var controller = BuildController(_db);

        var result = await controller.GetExtractionDecision(caseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeNull("no extraction decision has been recorded yet");
    }

    [Fact]
    public async Task UpsertExtractionDecision_UnknownCase_ReturnsNotFound()
    {
        var controller = BuildController(_db);

        var result = await controller.UpsertExtractionDecision(Guid.NewGuid(),
            new UpsertExtractionDecisionRequest { Decision = "extract" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpsertExtractionDecision_CreatesRow_AndMirrorsToCase()
    {
        var caseId = await SeedCaseAsync();
        var controller = BuildController(_db);

        var result = await controller.UpsertExtractionDecision(caseId,
            new UpsertExtractionDecisionRequest { Decision = "extract", DoctorNotes = "ازدحام شديد" });

        result.Should().BeOfType<OkObjectResult>();

        var rows = await _db.ExtractionDecisions.Where(e => e.OrthoCaseId == caseId).ToListAsync();
        rows.Should().ContainSingle();
        rows[0].Decision.Should().Be("extract");
        rows[0].DoctorNotes.Should().Be("ازدحام شديد");
        rows[0].DecidedAt.Should().NotBeNull();

        var orthoCase = await _db.OrthoCases.FindAsync(caseId);
        orthoCase!.ExtractionDecisionValue.Should().Be("extract",
            "the decision must be mirrored onto the OrthoCase for quick access");
    }

    [Fact]
    public async Task UpsertExtractionDecision_SecondCall_UpdatesSameRow_NoDuplicate()
    {
        var caseId = await SeedCaseAsync();
        var controller = BuildController(_db);

        await controller.UpsertExtractionDecision(caseId,
            new UpsertExtractionDecisionRequest { Decision = "extract" });
        await controller.UpsertExtractionDecision(caseId,
            new UpsertExtractionDecisionRequest { Decision = "non-extraction", DoctorNotes = "إعادة تقييم" });

        var rows = await _db.ExtractionDecisions.Where(e => e.OrthoCaseId == caseId).ToListAsync();
        rows.Should().ContainSingle("the upsert must update the existing decision, not create a duplicate");
        rows[0].Decision.Should().Be("non-extraction");
        rows[0].DoctorNotes.Should().Be("إعادة تقييم");

        var orthoCase = await _db.OrthoCases.FindAsync(caseId);
        orthoCase!.ExtractionDecisionValue.Should().Be("non-extraction");
    }

    [Fact]
    public async Task GetExtractionDecision_AfterUpsert_ReturnsSavedDecision()
    {
        var caseId = await SeedCaseAsync();
        var controller = BuildController(_db);
        await controller.UpsertExtractionDecision(caseId,
            new UpsertExtractionDecisionRequest { Decision = "extract", DoctorNotes = "ملاحظة" });

        var result = await controller.GetExtractionDecision(caseId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        GetProp(ok.Value!, "Decision").Should().Be("extract");
        GetProp(ok.Value!, "DoctorNotes").Should().Be("ملاحظة");
    }
}
