using AqlanDentalPro.API.Controllers;
using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public sealed class CephAssessmentHandoffTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly CephService _service;
    private readonly CephController _controller;

    public CephAssessmentHandoffTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);
        _service = new CephService(_db, _currentUser.Object, new Mock<ILogger<CephService>>().Object);
        _controller = new CephController(_service, _db, _patientAccess.Object, _currentUser.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Import_RequiresApprovedAnalysis()
    {
        var seeded = await SeedAsync(analysisApproved: false, diagnosisApproved: true);

        var result = await _service.ImportAssessmentProblemsAsync(seeded.AnalysisId, Request("Class II"));

        result.Status.Should().Be("analysis_not_approved");
        (await _db.ProblemListItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_RequiresDoctorApprovedDiagnosis()
    {
        var seeded = await SeedAsync(analysisApproved: true, diagnosisApproved: false);

        var result = await _service.ImportAssessmentProblemsAsync(seeded.AnalysisId, Request("Class II"));

        result.Status.Should().Be("diagnosis_not_approved");
        (await _db.ProblemListItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_AddsOnlySubmittedItemsAndSkipsExistingDescriptions()
    {
        var seeded = await SeedAsync(analysisApproved: true, diagnosisApproved: true);
        _db.ProblemListItems.Add(new ProblemListItem
        {
            OrthoCaseId = seeded.CaseId,
            Category = "skeletal",
            Description = "علاقة هيكلية: الصنف الثاني",
            Severity = "moderate",
            SortOrder = 1,
        });
        await _db.SaveChangesAsync();
        var request = new ImportCephAssessmentProblemsRequest
        {
            Items =
            [
                new() { Category = "skeletal", Description = " علاقة هيكلية: الصنف الثاني ", Severity = "moderate" },
                new() { Category = "dental", Description = "ميلان القواطع: بروز علوي", Severity = "severe" },
            ],
        };

        var result = await _service.ImportAssessmentProblemsAsync(seeded.AnalysisId, request);

        result.Status.Should().Be("ok");
        result.Added.Should().Be(1);
        result.SkippedExisting.Should().Be(1);
        var stored = await _db.ProblemListItems.OrderBy(item => item.SortOrder).ToListAsync();
        stored.Should().HaveCount(2);
        stored[1].Description.Should().Be("ميلان القواطع: بروز علوي");
        stored[1].SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task Controller_RejectsNonDoctorStaffEvenWithPatientAccess()
    {
        var seeded = await SeedAsync(analysisApproved: true, diagnosisApproved: true);
        _patientAccess.Setup(access => access.CanAccessPatientAsync(seeded.PatientId)).ReturnsAsync(true);
        _patientAccess.Setup(access => access.IsDoctor).Returns(false);
        _currentUser.Setup(user => user.IsAdmin).Returns(false);

        var response = await _controller.ImportAssessmentProblems(seeded.AnalysisId, Request("Class III"));

        var forbidden = response.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(403);
        (await _db.ProblemListItems.CountAsync()).Should().Be(0);
    }

    private static ImportCephAssessmentProblemsRequest Request(string description) => new()
    {
        Items = [new() { Category = "skeletal", Description = description, Severity = "moderate" }],
    };

    private async Task<(Guid PatientId, Guid CaseId, Guid AnalysisId)> SeedAsync(
        bool analysisApproved,
        bool diagnosisApproved)
    {
        var patient = new Patient
        {
            FirstName = "Assessment",
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
            IsApproved = analysisApproved,
            IsActive = true,
        };
        analysis.Diagnosis = new CephDiagnosis
        {
            Analysis = analysis,
            DoctorApproved = diagnosisApproved,
            IsActive = true,
        };
        _db.CephAnalyses.Add(analysis);
        await _db.SaveChangesAsync();
        return (patient.Id, orthoCase.Id, analysis.Id);
    }
}
