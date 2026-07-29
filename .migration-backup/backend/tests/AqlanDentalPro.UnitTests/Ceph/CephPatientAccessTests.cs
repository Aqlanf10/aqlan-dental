using AqlanDentalPro.API.Controllers;
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

public class CephPatientAccessTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPatientAccessService> _patientAccess = new();
    private readonly CephController _controller;

    public CephPatientAccessTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        var currentUser = new Mock<ICurrentUserService>();
        var service = new CephService(
            _db,
            currentUser.Object,
            new Mock<ILogger<CephService>>().Object);
        _controller = new CephController(service, _db, _patientAccess.Object, currentUser.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetById_UnassignedPatient_ReturnsForbid()
    {
        var seeded = await SeedAnalysisAsync();
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(false);

        var result = await _controller.GetById(seeded.AnalysisId);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task AutoTrace_UnassignedPatient_IsBlockedBeforeProviderCall()
    {
        var seeded = await SeedAnalysisAsync();
        _patientAccess
            .Setup(x => x.CanAccessPatientAsync(seeded.PatientId))
            .ReturnsAsync(false);

        var result = await _controller.AutoTrace(
            seeded.AnalysisId,
            new AqlanDentalPro.Application.DTOs.Ceph.CephAiTraceRequest { ImageWidth = 1000, ImageHeight = 1000 },
            null!,
            new Mock<ILogger<CephController>>().Object);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task List_DoctorReceivesOnlyAccessiblePatients()
    {
        var first = await SeedAnalysisAsync();
        var second = await SeedAnalysisAsync();
        _patientAccess
            .Setup(x => x.GetAccessiblePatientIdsAsync())
            .ReturnsAsync([first.PatientId]);

        var result = await _controller.List(null);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<List<AqlanDentalPro.Application.DTOs.Ceph.CephAnalysisListDto>>().Subject;

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(first.AnalysisId);
        rows.Should().NotContain(x => x.Id == second.AnalysisId);
    }

    [Fact]
    public async Task List_ProjectsExistingApprovalState()
    {
        var approved = await SeedAnalysisAsync(isApproved: true);
        var pending = await SeedAnalysisAsync(isApproved: false);
        _patientAccess
            .Setup(x => x.GetAccessiblePatientIdsAsync())
            .ReturnsAsync([approved.PatientId, pending.PatientId]);

        var result = await _controller.List(null);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var rows = ok.Value.Should().BeAssignableTo<List<AqlanDentalPro.Application.DTOs.Ceph.CephAnalysisListDto>>().Subject;

        rows.Should().Contain(x => x.Id == approved.AnalysisId && x.IsApproved);
        rows.Should().Contain(x => x.Id == pending.AnalysisId && !x.IsApproved);
    }

    [Fact]
    public async Task List_ClinicalTagsRequireAnalysisAndDoctorApproval()
    {
        var approved = await SeedAnalysisAsync(isApproved: true, diagnosisApproved: true, skeletalClass: "Class II");
        var analysisPending = await SeedAnalysisAsync(isApproved: false, diagnosisApproved: true, skeletalClass: "Class III");
        var diagnosisPending = await SeedAnalysisAsync(isApproved: true, diagnosisApproved: false, skeletalClass: "Class III");
        _patientAccess.Setup(x => x.GetAccessiblePatientIdsAsync())
            .ReturnsAsync([approved.PatientId, analysisPending.PatientId, diagnosisPending.PatientId]);

        var result = await _controller.List(null);
        var rows = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<List<AqlanDentalPro.Application.DTOs.Ceph.CephAnalysisListDto>>().Subject;

        rows.Single(x => x.Id == approved.AnalysisId).ClinicalTags
            .Should().ContainSingle(tag => tag.Key == "skeletal:class-ii");
        rows.Single(x => x.Id == analysisPending.AnalysisId).ClinicalTags.Should().BeEmpty();
        rows.Single(x => x.Id == diagnosisPending.AnalysisId).ClinicalTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Cohort_UsesAccessibleApprovedPatientsAndReturnsAggregateOnly()
    {
        var accessible = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var seeded = await SeedAnalysisAsync(
                isApproved: true,
                diagnosisApproved: true,
                skeletalClass: "Class II",
                measurementValue: 80 + i);
            accessible.Add(seeded.PatientId);
        }

        await SeedAnalysisAsync(
            isApproved: true,
            diagnosisApproved: true,
            skeletalClass: "Class III",
            measurementValue: 99);
        _patientAccess.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync([.. accessible]);

        var result = await _controller.Cohort(null, null, "steiner", null);
        var cohort = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AqlanDentalPro.Application.DTOs.Ceph.CephCohortResultDto>().Subject;

        cohort.Suppressed.Should().BeFalse();
        cohort.CohortSize.Should().Be(5);
        cohort.SkeletalDistribution.Should().ContainSingle(bucket =>
            bucket.Key == "skeletal:class-ii" && bucket.Count == 5 && bucket.Percentage == 100m);
        cohort.SkeletalDistribution.Should().NotContain(bucket => bucket.Key == "skeletal:class-iii");
        cohort.Measurements.Should().ContainSingle(row => row.MeasurementName == "SNA" && row.Count == 5);
    }

    [Fact]
    public async Task Cohort_FewerThanFivePatientsSuppressesCountsAndDistributions()
    {
        var accessible = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var seeded = await SeedAnalysisAsync(
                isApproved: true,
                diagnosisApproved: true,
                skeletalClass: "Class I",
                measurementValue: 82);
            accessible.Add(seeded.PatientId);
        }
        _patientAccess.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync([.. accessible]);

        var result = await _controller.Cohort(null, null, null, null);
        var cohort = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AqlanDentalPro.Application.DTOs.Ceph.CephCohortResultDto>().Subject;

        cohort.Suppressed.Should().BeTrue();
        cohort.CohortSize.Should().BeNull();
        cohort.SkeletalDistribution.Should().BeEmpty();
        cohort.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Cohort_UsesOnlyLatestApprovedRecordPerPatient()
    {
        var accessible = new List<Guid>();
        Guid firstAnalysisId = Guid.Empty;
        for (var i = 0; i < 5; i++)
        {
            var seeded = await SeedAnalysisAsync(
                isApproved: true,
                diagnosisApproved: true,
                skeletalClass: "Class I",
                measurementValue: 80 + i);
            accessible.Add(seeded.PatientId);
            if (i == 0) firstAnalysisId = seeded.AnalysisId;
        }

        var first = await _db.CephAnalyses.Include(a => a.OrthoCase)
            .SingleAsync(a => a.Id == firstAnalysisId);
        _db.CephAnalyses.Add(new CephAnalysis
        {
            OrthoCaseId = first.OrthoCaseId,
            AnalysisDate = first.AnalysisDate.AddDays(1),
            AnalysisType = "steiner",
            XrayFileUrl = "/uploads/latest.jpg",
            IsApproved = true,
            IsActive = true,
            Diagnosis = new CephDiagnosis
            {
                SkeletalClass = "Class III",
                VerticalPattern = "Normodivergent",
                IncisorInclination = "ميلان القواطع ضمن الحدود الطبيعية",
                DoctorApproved = true,
                IsActive = true,
            },
            Measurements =
            [
                new CephMeasurement
                {
                    MeasurementName = "SNA",
                    MeasurementValue = 90,
                    Unit = "°",
                    IsActive = true,
                },
            ],
        });
        await _db.SaveChangesAsync();
        _patientAccess.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync([.. accessible]);

        var result = await _controller.Cohort(null, null, "steiner", null);
        var cohort = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AqlanDentalPro.Application.DTOs.Ceph.CephCohortResultDto>().Subject;

        cohort.CohortSize.Should().Be(5);
        cohort.SkeletalDistribution.Should().Contain(bucket => bucket.Key == "skeletal:class-i" && bucket.Count == 4);
        cohort.SkeletalDistribution.Should().Contain(bucket => bucket.Key == "skeletal:class-iii" && bucket.Count == 1);
        cohort.Measurements.Should().ContainSingle(row => row.MeasurementName == "SNA" && row.Count == 5);
    }

    [Fact]
    public async Task Cohort_DuplicateMeasurementsFromOnePatientDoNotPassPrivacyThreshold()
    {
        var accessible = new List<Guid>();
        Guid firstAnalysisId = Guid.Empty;
        for (var i = 0; i < 5; i++)
        {
            var seeded = await SeedAnalysisAsync(
                isApproved: true,
                diagnosisApproved: true,
                skeletalClass: "Class I");
            accessible.Add(seeded.PatientId);
            if (i == 0) firstAnalysisId = seeded.AnalysisId;
        }

        for (var i = 0; i < 5; i++)
        {
            _db.CephMeasurements.Add(new CephMeasurement
            {
                AnalysisId = firstAnalysisId,
                MeasurementName = "SNA",
                MeasurementValue = 80 + i,
                Unit = "°",
                IsActive = true,
            });
        }
        await _db.SaveChangesAsync();
        _patientAccess.Setup(x => x.GetAccessiblePatientIdsAsync()).ReturnsAsync([.. accessible]);

        var result = await _controller.Cohort(null, null, "steiner", null);
        var cohort = result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<AqlanDentalPro.Application.DTOs.Ceph.CephCohortResultDto>().Subject;

        cohort.Suppressed.Should().BeFalse();
        cohort.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task Cohort_RejectsUnknownFreeTextTag()
    {
        var result = await _controller.Cohort(null, null, null, "doctor-note:custom");

        result.Should().BeOfType<BadRequestObjectResult>();
        _patientAccess.Verify(x => x.GetAccessiblePatientIdsAsync(), Times.Never);
    }

    private async Task<(Guid PatientId, Guid AnalysisId)> SeedAnalysisAsync(
        bool isApproved = false,
        bool diagnosisApproved = false,
        string? skeletalClass = null,
        decimal? measurementValue = null)
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
            IsApproved = isApproved,
            IsActive = true,
        };
        if (skeletalClass is not null || diagnosisApproved)
        {
            analysis.Diagnosis = new CephDiagnosis
            {
                SkeletalClass = skeletalClass,
                VerticalPattern = "Normodivergent",
                IncisorInclination = "ميلان القواطع ضمن الحدود الطبيعية",
                DoctorApproved = diagnosisApproved,
                IsActive = true,
            };
        }
        if (measurementValue.HasValue)
        {
            analysis.Measurements.Add(new CephMeasurement
            {
                MeasurementName = "SNA",
                MeasurementValue = measurementValue,
                Unit = "°",
                IsActive = true,
            });
        }
        _db.CephAnalyses.Add(analysis);
        await _db.SaveChangesAsync();
        return (patient.Id, analysis.Id);
    }
}
