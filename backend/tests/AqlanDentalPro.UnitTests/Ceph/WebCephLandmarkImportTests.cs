using System.Text;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class WebCephLandmarkImportTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CephService _service;
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _analysisId = Guid.NewGuid();

    public WebCephLandmarkImportTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new CephService(
            _db,
            new Mock<ICurrentUserService>().Object,
            new Mock<ILogger<CephService>>().Object);

        var patient = new Patient
        {
            Id = _patientId,
            FirstName = "Test",
            LastName = "Patient",
            IsActive = true,
        };
        var orthoCase = new OrthoCase
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            CaseNumber = "ORT-WC-001",
            IsActive = true,
        };
        var analysis = new CephAnalysis
        {
            Id = _analysisId,
            OrthoCaseId = orthoCase.Id,
            AnalysisType = "full",
            XrayFileUrl = "/uploads/test-ceph.jpg",
            IsActive = true,
        };
        _db.AddRange(patient, orthoCase, analysis);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Parser_ReadsCurrentHtmlXlsShape_AndMapsCanonicalAndExtraKeys()
    {
        using var stream = ExportStream(
            Row("S", "0.00 mm", "0.00 mm"),
            Row("A-point", "71.48 mm", "47.80 mm"),
            Row("Ba", "-30.15 mm", "39.77 mm"));

        var parsed = WebCephLandmarkImportParser.Parse(stream);

        parsed.RecordingDate.Should().Be("2026-07-13");
        parsed.Landmarks.Should().HaveCount(3);
        parsed.Landmarks.Should().ContainEquivalentOf(
            new WebCephLandmarkImportRow("A-point", "A", 71.48, 47.80));
        parsed.Landmarks.Should().ContainEquivalentOf(
            new WebCephLandmarkImportRow("Ba", "WC_Ba", -30.15, 39.77));
    }

    [Fact]
    public void Parser_RejectsArbitraryOrEmptyFiles()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("not a landmark table"));

        var action = () => WebCephLandmarkImportParser.Parse(stream);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Parser_PreservesRowsWhoseNormalizedExternalKeysCollide()
    {
        using var stream = ExportStream(
            Row("Custom point", "1.00 mm", "2.00 mm"),
            Row("Custom-point", "3.00 mm", "4.00 mm"));

        var parsed = WebCephLandmarkImportParser.Parse(stream);

        parsed.Landmarks.Select(item => item.LandmarkKey)
            .Should().Equal("WC_Custom_point", "WC_Custom_point_2");
    }

    [Fact]
    public async Task Import_AnchorsMillimetresAtS_PreservesEverySupportedRowAndSource()
    {
        using var stream = ExportStream(
            Row("S", "0.00 mm", "0.00 mm"),
            Row("A-point", "10.00 mm", "5.00 mm"),
            Row("Ba", "-5.00 mm", "2.00 mm"));

        var summary = await _service.ImportWebCephLandmarksAsync(
            _analysisId,
            stream,
            new WebCephLandmarkImportOptions
            {
                PixelsPerMm = 2,
                AnchorX = 100,
                AnchorY = 100,
                ImageWidth = 500,
                ImageHeight = 500,
            });

        summary.Should().NotBeNull();
        summary!.Parsed.Should().Be(3);
        summary.Imported.Should().Be(3);
        summary.SkippedOutOfBounds.Should().Be(0);

        var landmarks = await _db.CephLandmarks
            .Where(item => item.AnalysisId == _analysisId && item.IsActive)
            .ToListAsync();
        landmarks.Should().Contain(item => item.LandmarkKey == "S"
            && item.XCoord == 100m && item.YCoord == 100m);
        landmarks.Should().Contain(item => item.LandmarkKey == "A"
            && item.XCoord == 120m && item.YCoord == 110m);
        landmarks.Should().Contain(item => item.LandmarkKey == "WC_Ba"
            && item.XCoord == 90m && item.YCoord == 104m
            && item.PlacementSource == "webceph-import"
            && item.SourceLandmarkKey == "Ba"
            && !item.IsReviewed);
    }

    [Fact]
    public async Task SavedDoctorCorrection_ProducesModelSpecificMillimetreAccuracyEvidence()
    {
        var saved = await _service.SaveLandmarksAsync(
            _analysisId,
            new SaveLandmarksRequest
            {
                PixelsPerMm = 5,
                ImageWidth = 1000,
                ImageHeight = 1000,
                Landmarks =
                [
                    new LandmarkInput
                    {
                        Key = "S",
                        Name = "Sella",
                        X = 103,
                        Y = 104,
                        IsAiPlaced = false,
                        PlacementSource = "ai",
                        SourceModelId = "gemini/test-model",
                        AiProposalX = 100,
                        AiProposalY = 100,
                        IsReviewed = true,
                        Confidence = 0.9,
                        Reasoning = "center of sella",
                    },
                ],
            });

        saved.Should().BeTrue();
        var stored = await _db.CephLandmarks.SingleAsync(item => item.IsActive);
        stored.ReviewErrorMm.Should().Be(1m);
        stored.SourceModelId.Should().Be("gemini/test-model");
        stored.Reasoning.Should().Be("center of sella");

        var accuracy = await _service.GetAiAccuracyAsync([_patientId]);
        accuracy.ReviewedPointCount.Should().Be(1);
        accuracy.ReviewedAnalysisCount.Should().Be(1);
        accuracy.SufficientSample.Should().BeFalse();
        accuracy.Models.Should().ContainSingle();
        accuracy.Models[0].SufficientSample.Should().BeFalse();
        accuracy.Models[0].MeanErrorMm.Should().Be(1);
        accuracy.Models[0].WithinOneMmPercent.Should().Be(100);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task AccuracySample_IsEvaluatedPerModelVersion(
        bool splitAcrossModelVersions,
        bool expectedSufficient)
    {
        var original = await _db.CephAnalyses.FindAsync(_analysisId);
        var analysisIds = new[] { _analysisId, Guid.NewGuid(), Guid.NewGuid() };
        _db.CephAnalyses.AddRange(analysisIds.Skip(1).Select(id => new CephAnalysis
        {
            Id = id,
            OrthoCaseId = original!.OrthoCaseId,
            AnalysisType = "full",
            XrayFileUrl = "/uploads/test-ceph.jpg",
            IsActive = true,
        }));

        for (var analysisIndex = 0; analysisIndex < analysisIds.Length; analysisIndex++)
        {
            for (var pointIndex = 0; pointIndex < 10; pointIndex++)
            {
                _db.CephLandmarks.Add(new CephLandmark
                {
                    AnalysisId = analysisIds[analysisIndex],
                    LandmarkKey = $"P{analysisIndex}_{pointIndex}",
                    PlacementSource = "ai",
                    SourceModelId = splitAcrossModelVersions && analysisIndex == 2
                        ? "gemini/model-b"
                        : "gemini/model-a",
                    IsReviewed = true,
                    ReviewErrorMm = 0.5m,
                });
            }
        }
        await _db.SaveChangesAsync();

        var accuracy = await _service.GetAiAccuracyAsync([_patientId]);

        accuracy.ReviewedPointCount.Should().Be(30);
        accuracy.ReviewedAnalysisCount.Should().Be(3);
        accuracy.SufficientSample.Should().Be(expectedSufficient);
        accuracy.Models.Count(model => model.SufficientSample)
            .Should().Be(expectedSufficient ? 1 : 0);
    }

    [Fact]
    public async Task SavingNewLandmarkSnapshot_RevokesPreviousApproval()
    {
        var analysis = await _db.CephAnalyses.FindAsync(_analysisId);
        analysis!.IsApproved = true;
        analysis.ApprovedAt = DateTime.UtcNow;
        analysis.ApprovedByUserId = Guid.NewGuid();
        await _db.SaveChangesAsync();

        await _service.SaveLandmarksAsync(
            _analysisId,
            new SaveLandmarksRequest
            {
                PixelsPerMm = 5,
                ImageWidth = 1000,
                ImageHeight = 1000,
                Landmarks =
                [
                    new LandmarkInput
                    {
                        Key = "S",
                        Name = "Sella",
                        X = 100,
                        Y = 100,
                        PlacementSource = "manual",
                        IsReviewed = true,
                    },
                ],
            });

        analysis.IsApproved.Should().BeFalse();
        analysis.ApprovedAt.Should().BeNull();
        analysis.ApprovedByUserId.Should().BeNull();
    }

    private static MemoryStream ExportStream(params string[] landmarkRows)
    {
        var html = "\uFEFF" +
            "<tr class=\"measurement-row tr-info\"><td class=\"td-content\"><table><tbody><tr>" +
            "<td class=\"td-header\">Test Patient</td><td class=\"td-header\">25Y</td>" +
            "<td class=\"td-header\">2026-07-13</td></tr></tbody></table></td></tr>" +
            string.Concat(landmarkRows);
        return new MemoryStream(Encoding.UTF8.GetBytes(html));
    }

    private static string Row(string name, string x, string y) =>
        "<tr class=\"measurement-row\"><td class=\"td-content\"><table><tbody><tr>" +
        $"<td class=\"td-body\">{name}</td><td class=\"td-body\">{x}</td>" +
        $"<td class=\"td-body\">{y}</td></tr></tbody></table></td></tr>";
}
