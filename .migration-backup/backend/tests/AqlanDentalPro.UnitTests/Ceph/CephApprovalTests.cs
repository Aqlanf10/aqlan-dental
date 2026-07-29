using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// CEPH-EPIC clinical approval gate (Sprint 6).
/// The final PDF report cannot be issued until an authorized doctor/admin
/// approves the analysis. Mirrors the CephPatientAccessTests construction style
/// (InMemory provider, mocked IPatientAccessService + ICurrentUserService).
/// </summary>
public class CephApprovalTests : IDisposable
{
    private const string NotApprovedMessage = "لا يمكن إصدار التقرير النهائي قبل اعتماد الطبيب للتحليل";

    private readonly AppDbContext _db;
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly CephController _controller;

    public CephApprovalTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);
        var service = new CephService(
            _db,
            _currentUser.Object,
            new Mock<ILogger<CephService>>().Object);
        _controller = new CephController(service, _db, _patientAccess.Object, _currentUser.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── PDF gate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetReportPdf_NotApproved_ReturnsBadRequestWithArabicMessage()
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);

        var result = await _controller.GetReportPdf(
            seeded.AnalysisId,
            new CephReportPdfGenerator(_db),
            new Mock<ILogger<CephController>>().Object);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain(NotApprovedMessage);
    }

    [Fact]
    public async Task GetReportPdf_AfterApproval_ProceedsToGenerationAndReturnsPdf()
    {
        var seeded = await SeedAnalysisAsync(approved: true);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);

        var result = await _controller.GetReportPdf(
            seeded.AnalysisId,
            new CephReportPdfGenerator(_db),
            new Mock<ILogger<CephController>>().Object);

        // No longer the not-approved BadRequest — generation proceeds and yields a PDF file.
        result.Should().NotBeOfType<BadRequestObjectResult>();
        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        System.Text.Encoding.ASCII.GetString(file.FileContents, 0, 4).Should().Be("%PDF");
    }

    // ── Approve authorization ───────────────────────────────────────────────────

    [Fact]
    public async Task Approve_ByAssignedDoctor_SetsApprovalFields()
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        var doctorUserId = Guid.NewGuid();
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _patientAccess.Setup(x => x.IsDoctor).Returns(true);
        _currentUser.Setup(u => u.IsAdmin).Returns(false);
        _currentUser.Setup(u => u.UserId).Returns(doctorUserId);

        var result = await _controller.Approve(seeded.AnalysisId, new ApproveCephAnalysisRequest { Notes = "تمت المراجعة" });

        result.Should().BeOfType<OkObjectResult>();
        var analysis = await _db.CephAnalyses.AsNoTracking().FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.IsApproved.Should().BeTrue();
        analysis.ApprovedByUserId.Should().Be(doctorUserId);
        analysis.ApprovedAt.Should().NotBeNull();
        analysis.ApprovalNotes.Should().Be("تمت المراجعة");
    }

    [Fact]
    public async Task Approve_ByAdmin_Succeeds()
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        var adminUserId = Guid.NewGuid();
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _patientAccess.Setup(x => x.IsDoctor).Returns(false);
        _currentUser.Setup(u => u.IsAdmin).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(adminUserId);

        var result = await _controller.Approve(seeded.AnalysisId, null);

        result.Should().BeOfType<OkObjectResult>();
        var analysis = await _db.CephAnalyses.AsNoTracking().FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.IsApproved.Should().BeTrue();
        analysis.ApprovedByUserId.Should().Be(adminUserId);
    }

    [Fact]
    public async Task Approve_ByNonAuthorizedStaff_Returns403()
    {
        // Reception/accountant: passes patient access (full access) but is neither doctor nor admin.
        var seeded = await SeedAnalysisAsync(approved: false);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _patientAccess.Setup(x => x.IsDoctor).Returns(false);
        _currentUser.Setup(u => u.IsAdmin).Returns(false);
        _currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var result = await _controller.Approve(seeded.AnalysisId, null);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(403);
        var analysis = await _db.CephAnalyses.AsNoTracking().FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.IsApproved.Should().BeFalse("an unauthorized user must not approve the analysis");
    }

    [Fact]
    public async Task Approve_UnassignedDoctor_ReturnsForbidBeforeApproving()
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(false);

        var result = await _controller.Approve(seeded.AnalysisId, null);

        result.Should().BeOfType<ForbidResult>();
        var analysis = await _db.CephAnalyses.AsNoTracking().FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Approve_AlreadyApproved_IsIdempotentOk()
    {
        var seeded = await SeedAnalysisAsync(approved: true);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _patientAccess.Setup(x => x.IsDoctor).Returns(false);
        _currentUser.Setup(u => u.IsAdmin).Returns(true);
        _currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());

        var result = await _controller.Approve(seeded.AnalysisId, null);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.ToString().Should().Contain("معتمد مسبقاً");
    }

    [Theory]
    [InlineData("ai")]
    [InlineData("webceph-import")]
    public async Task Approve_WithUnreviewedExternalLandmark_ReturnsBadRequest(string source)
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        _db.CephLandmarks.Add(new CephLandmark
        {
            AnalysisId = seeded.AnalysisId,
            LandmarkKey = "S",
            LandmarkName = "Sella",
            XCoord = 100,
            YCoord = 100,
            PlacementSource = source,
            IsReviewed = false,
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _currentUser.Setup(x => x.IsAdmin).Returns(true);

        var result = await _controller.Approve(seeded.AnalysisId, null);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("المتبقي: 1");
        (await _db.CephAnalyses.FindAsync(seeded.AnalysisId))!.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task Approve_IncompletePaAnalysis_ReturnsBadRequest()
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        var analysis = await _db.CephAnalyses.FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.AnalysisType = "pa";
        await _db.SaveChangesAsync();
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _currentUser.Setup(x => x.IsAdmin).Returns(true);

        var result = await _controller.Approve(seeded.AnalysisId, null);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("النقاط الـ15");
        analysis.IsApproved.Should().BeFalse();
    }

    [Fact]
    public async Task GetReportPdf_ApprovedButIncompletePaAnalysis_ReturnsBadRequest()
    {
        var seeded = await SeedAnalysisAsync(approved: true);
        var analysis = await _db.CephAnalyses.FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.AnalysisType = "pa";
        await _db.SaveChangesAsync();
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);

        var result = await _controller.GetReportPdf(
            seeded.AnalysisId,
            new CephReportPdfGenerator(_db),
            new Mock<ILogger<CephController>>().Object);

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.ToString().Should().Contain("تقرير PA");
    }

    [Fact]
    public async Task Approve_CompletePaAnalysis_Succeeds()
    {
        var seeded = await SeedAnalysisAsync(approved: false);
        var analysis = await _db.CephAnalyses.FirstAsync(x => x.Id == seeded.AnalysisId);
        analysis.AnalysisType = "pa";
        analysis.Notes = "{\"PixelsPerMm\":5,\"ImageWidth\":800,\"ImageHeight\":1000}";
        var keys = new[] { "Cg", "ZR", "ZL", "ANS", "JR", "JL", "AgR", "AgL", "Me", "UDM", "LDM", "U6R", "U6L", "L6R", "L6L" };
        _db.CephLandmarks.AddRange(keys.Select((key, index) => new CephLandmark
        {
            AnalysisId = analysis.Id, LandmarkKey = key, LandmarkName = key,
            XCoord = index * 10, YCoord = index * 5, IsActive = true,
        }));
        _db.CephMeasurements.Add(new CephMeasurement
        {
            AnalysisId = analysis.Id, MeasurementName = "PA-JJ-Width",
            MeasurementValue = 60, Unit = "mm", Classification = "unclassified", IsActive = true,
        });
        await _db.SaveChangesAsync();
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _currentUser.Setup(x => x.IsAdmin).Returns(true);
        _currentUser.Setup(x => x.UserId).Returns(Guid.NewGuid());

        var result = await _controller.Approve(seeded.AnalysisId, null);

        result.Should().BeOfType<OkObjectResult>();
        analysis.IsApproved.Should().BeTrue();
    }

    private async Task<(Guid PatientId, Guid AnalysisId)> SeedAnalysisAsync(bool approved)
    {
        var patient = new Patient
        {
            FirstName = "Ceph",
            LastName = Guid.NewGuid().ToString("N"),
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Patient = patient,
            CaseNumber = $"ORT-{Guid.NewGuid():N}",
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            OrthoCase = orthoCase,
            AnalysisType = "steiner",
            XrayFileUrl = "/uploads/test.jpg",
            IsActive = true,
            IsApproved = approved,
            ApprovedByUserId = approved ? Guid.NewGuid() : null,
            ApprovedAt = approved ? DateTime.UtcNow : null,
        };
        _db.CephAnalyses.Add(analysis);
        await _db.SaveChangesAsync();
        return (patient.Id, analysis.Id);
    }
}
