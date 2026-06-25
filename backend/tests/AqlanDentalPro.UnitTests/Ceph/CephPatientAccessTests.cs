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

    private async Task<(Guid PatientId, Guid AnalysisId)> SeedAnalysisAsync()
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
        };
        _db.CephAnalyses.Add(analysis);
        await _db.SaveChangesAsync();
        return (patient.Id, analysis.Id);
    }
}
