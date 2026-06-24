using AqlanDentalPro.API.Controllers;
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
/// CEPH-EPIC batch C-B — analysis version snapshots.
///
/// Verifies that:
/// - Saving a snapshot persists landmarks + measurements + diagnosis as JSON.
/// - Listing snapshots returns only the snapshots of the requested analysis.
/// - Loading an unknown version returns a 404 with an Arabic message.
/// - PatientAccessFilter is enforced — a doctor without access to the parent
///   analysis's patient gets a 403 before the service is invoked.
/// - Honest AI labels: the simulation setting key default is DISABLED, and
///   the existing simulation/audit-log behavior is preserved.
/// </summary>
public class CephVersionsTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly CephController _controller;
    private readonly CephService _service;

    public CephVersionsTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        currentUser.Setup(u => u.IsAdmin).Returns(true);
        _service = new CephService(_db, currentUser.Object, new Mock<ILogger<CephService>>().Object);
        _controller = new CephController(_service, _db, _patientAccess.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SaveVersion_PersistsSnapshot_LandmarksMeasurementsDiagnosis()
    {
        var seeded = await SeedAnalysisWithStateAsync();
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(true);

        var result = await _controller.SaveVersion(seeded.AnalysisId,
            new CreateCephVersionRequest { Label = "قبل العلاج" });

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var dto = created.Value.Should().BeAssignableTo<CephVersionListDto>().Subject;
        dto.Label.Should().Be("قبل العلاج");
        dto.CephAnalysisId.Should().Be(seeded.AnalysisId);
        dto.SnapshotDate.Should().NotBeNullOrEmpty();

        // Verify the snapshot row in the DB carries the JSON payloads.
        var row = await _db.CephAnalysisVersions.SingleAsync(v => v.Id == dto.Id);
        row.Label.Should().Be("قبل العلاج");
        row.LandmarksJson.Should().Contain("S");
        // Note: System.Text.Json escapes non-ASCII (Arabic) as \uXXXX by default,
        // so we verify round-trip via deserialization instead of substring match.
        var landmarkPayload = System.Text.Json.JsonSerializer.Deserialize<List<CephLandmarkDto>>(row.LandmarksJson);
        landmarkPayload.Should().NotBeNull();
        landmarkPayload.Should().Contain(l => l.Key == "S" && l.Name == "السرج");
        row.MeasurementsJson.Should().Contain("SNA");
        row.DiagnosisJson.Should().NotBeNull();
        var diagnosisPayload = System.Text.Json.JsonSerializer.Deserialize<CephDiagnosisDto>(row.DiagnosisJson!);
        diagnosisPayload.Should().NotBeNull();
        diagnosisPayload!.SkeletalClass.Should().Be("Class II");
        row.CreatedByUserId.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVersions_ReturnsList_ForAnalysis()
    {
        var seeded = await SeedAnalysisWithStateAsync();

        // Save two snapshots with different labels.
        await _service.SaveVersionAsync(seeded.AnalysisId, new CreateCephVersionRequest { Label = "قبل" });
        await _service.SaveVersionAsync(seeded.AnalysisId, new CreateCephVersionRequest { Label = "بعد 6 أشهر" });

        // A snapshot of a DIFFERENT analysis must NOT appear in this list.
        var otherAnalysis = await SeedAnalysisWithStateAsync();
        await _service.SaveVersionAsync(otherAnalysis.AnalysisId,
            new CreateCephVersionRequest { Label = "تحليل آخر" });

        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(true);

        var result = await _controller.ListVersions(seeded.AnalysisId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<List<CephVersionListDto>>().Subject;
        list.Should().HaveCount(2);
        list.Should().OnlyContain(v => v.CephAnalysisId == seeded.AnalysisId);
        list.Should().Contain(v => v.Label == "قبل");
        list.Should().Contain(v => v.Label == "بعد 6 أشهر");
        list.Should().NotContain(v => v.Label == "تحليل آخر");
    }

    [Fact]
    public async Task GetVersion_UnknownId_Returns404_Arabic()
    {
        var seeded = await SeedAnalysisWithStateAsync();
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(true);

        var result = await _controller.GetVersion(seeded.AnalysisId, Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var msg = notFound.Value.Should().BeAssignableTo<dynamic>().Subject;
        // The response shape is { message: "النسخة غير موجودة" } (anonymous object).
        ((string)msg.message).Should().Be("النسخة غير موجودة");
    }

    [Fact]
    public async Task SaveVersion_CrossPatient_Returns403()
    {
        var seeded = await SeedAnalysisWithStateAsync();
        // Doctor does NOT have access to this patient → 403 BEFORE the service
        // is called (PatientAccessFilter pattern, mirrors CephPatientAccessTests).
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(false);

        var result = await _controller.SaveVersion(seeded.AnalysisId,
            new CreateCephVersionRequest { Label = "يجب أن يُرفض" });

        result.Should().BeOfType<ForbidResult>();
        // And no snapshot was written.
        (await _db.CephAnalysisVersions.AnyAsync(v => v.CephAnalysisId == seeded.AnalysisId))
            .Should().BeFalse("the cross-patient attempt must not persist anything");
    }

    [Fact]
    public async Task GetVersion_ReturnsFullSnapshot_WithDeserializedPayloads()
    {
        var seeded = await SeedAnalysisWithStateAsync();
        var saved = await _service.SaveVersionAsync(seeded.AnalysisId,
            new CreateCephVersionRequest { Label = "نسخة 1" });
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(true);

        var result = await _controller.GetVersion(seeded.AnalysisId, saved!.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeAssignableTo<CephVersionDetailDto>().Subject;
        detail.Label.Should().Be("نسخة 1");
        detail.Landmarks.Should().NotBeEmpty();
        detail.Landmarks.Should().Contain(l => l.Key == "S");
        detail.Measurements.Should().NotBeEmpty();
        detail.Measurements.Should().Contain(m => m.Name == "SNA");
        detail.Diagnosis.Should().NotBeNull();
        detail.Diagnosis!.SkeletalClass.Should().Be("Class II");
    }

    [Fact]
    public async Task SaveVersion_EmptyLabel_Returns400_Arabic()
    {
        var seeded = await SeedAnalysisWithStateAsync();
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(true);

        var result = await _controller.SaveVersion(seeded.AnalysisId,
            new CreateCephVersionRequest { Label = "   " });

        var bad = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var msg = bad.Value.Should().BeAssignableTo<dynamic>().Subject;
        ((string)msg.message).Should().Be("اسم النسخة مطلوب");
    }

    /// <summary>
    /// Improvement 1 (close C-A): the AI simulation template MUST be disabled
    /// by default. This test pins that behavior — IsSimulationEnabledAsync
    /// returns false when no Settings row flips it on. The frontend relies on
    /// the 403 ("المحاكاة التجريبية معطلة من الإعدادات") to keep the feature
    /// hidden by default.
    /// </summary>
    [Fact]
    public async Task Simulation_IsDisabledByDefault_PerCepEpicC_A()
    {
        // No Settings row with "ceph.simulation_enabled" → disabled.
        var enabled = await _service.IsSimulationEnabledAsync();
        enabled.Should().BeFalse(
            "CEPH-EPIC C-A requires the simulation to be HIDDEN by default — " +
            "the owner must explicitly opt in via the Settings key");
    }

    private async Task<(Guid PatientId, Guid AnalysisId)> SeedAnalysisWithStateAsync()
    {
        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = "مريض",
            LastName = "السيفالو",
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            CaseNumber = $"ORT-{Guid.NewGuid():N}".Substring(0, 12),
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(),
            OrthoCaseId = orthoCase.Id,
            AnalysisType = "steiner",
            AnalysisDate = DateOnly.FromDateTime(DateTime.UtcNow),
            XrayFileUrl = "/uploads/test.jpg",
            IsActive = true,
        };
        _db.AddRange(patient, orthoCase, analysis);
        _db.CephLandmarks.Add(new CephLandmark
        {
            AnalysisId = analysis.Id,
            LandmarkKey = "S",
            LandmarkName = "السرج",
            XCoord = 100m,
            YCoord = 200m,
            IsActive = true,
        });
        _db.CephLandmarks.Add(new CephLandmark
        {
            AnalysisId = analysis.Id,
            LandmarkKey = "N",
            LandmarkName = "الناسيون",
            XCoord = 150m,
            YCoord = 220m,
            IsActive = true,
        });
        _db.CephMeasurements.Add(new CephMeasurement
        {
            AnalysisId = analysis.Id,
            MeasurementName = "SNA",
            MeasurementValue = 84m,
            NormalValue = 82m,
            StdDeviation = 2m,
            Unit = "°",
            IsActive = true,
        });
        _db.CephDiagnoses.Add(new CephDiagnosis
        {
            AnalysisId = analysis.Id,
            SkeletalClass = "Class II",
            FinalDiagnosis = "تنافر هيكلي من الصنف الثاني",
            IsActive = true,
        });
        await _db.SaveChangesAsync();
        return (patient.Id, analysis.Id);
    }
}
