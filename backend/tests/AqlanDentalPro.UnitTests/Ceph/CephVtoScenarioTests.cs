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

public sealed class CephVtoScenarioTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly CephService _service;
    private readonly CephController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public CephVtoScenarioTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _currentUser.Setup(x => x.UserId).Returns(_userId);
        _service = new CephService(_db, _currentUser.Object, new Mock<ILogger<CephService>>().Object);
        _controller = new CephController(_service, _db, _patientAccess.Object, _currentUser.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Save_RequiresApprovedAnalysis()
    {
        var seeded = await SeedAsync(approved: false, calibrated: true, includeIncisors: true);

        var result = await _service.SaveVtoScenarioAsync(seeded.AnalysisId, Request());

        result.Status.Should().Be("analysis_not_approved");
        (await _db.CephVtoScenarios.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false, true, "calibration_missing")]
    [InlineData(true, false, "incisors_missing")]
    public async Task Save_RequiresCalibrationAndSavedIncisorLandmarks(
        bool calibrated,
        bool includeIncisors,
        string expectedStatus)
    {
        var seeded = await SeedAsync(approved: true, calibrated, includeIncisors);

        var result = await _service.SaveVtoScenarioAsync(seeded.AnalysisId, Request());

        result.Status.Should().Be(expectedStatus);
        result.Scenario.Should().BeNull();
    }

    [Fact]
    public async Task Save_CreatesImmutableVersionedSnapshots_WithCalculatedOverjet()
    {
        var seeded = await SeedAsync(approved: true, calibrated: true, includeIncisors: true);

        var first = await _service.SaveVtoScenarioAsync(seeded.AnalysisId, Request());
        var second = await _service.SaveVtoScenarioAsync(seeded.AnalysisId, new SaveCephVtoScenarioRequest
        {
            ScenarioGroupId = first.Scenario!.ScenarioGroupId,
            Name = "خطة القواطع المعدلة",
            UpperIncisorMoveMm = 2m,
            LowerIncisorMoveMm = -1m,
            Notes = "الإصدار الثاني",
        });

        first.Status.Should().Be("ok");
        first.Scenario.VersionNumber.Should().Be(1);
        first.Scenario.OverjetBeforeMm.Should().Be(3m);
        first.Scenario.OverjetAfterMm.Should().Be(4.5m);
        first.Scenario.CreatedByUserId.Should().Be(_userId);
        second.Status.Should().Be("ok");
        second.Scenario!.VersionNumber.Should().Be(2);
        second.Scenario.OverjetBeforeMm.Should().Be(3m);
        second.Scenario.OverjetAfterMm.Should().Be(6m);

        var stored = await _db.CephVtoScenarios
            .OrderBy(x => x.VersionNumber)
            .ToListAsync();
        stored.Should().HaveCount(2);
        stored[0].Name.Should().Be("خطة القواطع");
        stored[0].UpperIncisorMoveMm.Should().Be(1.5m);
        stored[1].ScenarioGroupId.Should().Be(stored[0].ScenarioGroupId);

        var listed = await _service.ListVtoScenariosAsync(seeded.AnalysisId);
        listed.Select(x => x.VersionNumber).Should().BeEquivalentTo([2, 1]);
        listed.Should().OnlyContain(x => x.Disclaimer == CephService.VtoDisclaimerAr);
    }

    [Fact]
    public async Task Controller_RejectsNonDoctorStaffEvenWithPatientAccess()
    {
        var seeded = await SeedAsync(approved: true, calibrated: true, includeIncisors: true);
        _patientAccess.Setup(x => x.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _patientAccess.Setup(x => x.IsDoctor).Returns(false);
        _currentUser.Setup(x => x.IsAdmin).Returns(false);

        var response = await _controller.SaveVtoScenario(seeded.AnalysisId, Request());

        var forbidden = response.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(403);
        (await _db.CephVtoScenarios.CountAsync()).Should().Be(0);
    }

    private static SaveCephVtoScenarioRequest Request() => new()
    {
        Name = "خطة القواطع",
        UpperIncisorMoveMm = 1.5m,
        LowerIncisorMoveMm = 0m,
        Notes = "محاكاة هندسية فقط",
    };

    private async Task<(Guid PatientId, Guid AnalysisId)> SeedAsync(
        bool approved,
        bool calibrated,
        bool includeIncisors)
    {
        var patient = new Patient
        {
            FirstName = "VTO",
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
            IsApproved = approved,
            Notes = calibrated ? "{\"PixelsPerMm\":10,\"ImageWidth\":1000,\"ImageHeight\":800}" : null,
            IsActive = true,
        };
        if (includeIncisors)
        {
            analysis.Landmarks.Add(Landmark("U1T", 130m, 100m));
            analysis.Landmarks.Add(Landmark("U1A", 120m, 200m));
            analysis.Landmarks.Add(Landmark("L1T", 100m, 100m));
            analysis.Landmarks.Add(Landmark("L1A", 110m, 200m));
        }
        _db.CephAnalyses.Add(analysis);
        await _db.SaveChangesAsync();
        return (patient.Id, analysis.Id);
    }

    private static CephLandmark Landmark(string key, decimal x, decimal y) => new()
    {
        LandmarkKey = key,
        LandmarkName = key,
        XCoord = x,
        YCoord = y,
        IsActive = true,
    };
}
