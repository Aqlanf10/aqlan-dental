using System.Text.Json;
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
/// Sprint A10a: read-only external 3D planning export package. The endpoint must stay inside
/// the existing Ortho-Surgical case API, avoid any database writes, avoid separate routes, and
/// avoid leaking patient names in the JSON handoff manifest.
/// </summary>
public class OrthoSurgicalExportPackageTests
{
    private static AppDbContext CreateDb()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"orthosurgical-export-{Guid.NewGuid()}")
            .Options);

    private static Mock<ICurrentUserService> AdminUser()
    {
        var m = new Mock<ICurrentUserService>();
        m.SetupGet(c => c.UserId).Returns(Guid.NewGuid());
        m.SetupGet(c => c.Role).Returns(UserRole.Admin);
        m.SetupGet(c => c.IsAdmin).Returns(true);
        m.SetupGet(c => c.IsAuthenticated).Returns(true);
        return m;
    }

    private static OrthoSurgicalCasesController Build(AppDbContext db)
    {
        var access = new Mock<IPatientAccessService>();
        access.SetupGet(p => p.IsDoctor).Returns(false);
        access.Setup(p => p.CanAccessPatientAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var logger = new Mock<ILogger<OrthoSurgicalCasesController>>().Object;
        return new OrthoSurgicalCasesController(db, logger, access.Object, new Mock<IAuditService>().Object, AdminUser().Object);
    }

    private static async Task<OrthoSurgicalCase> SeedExportCaseAsync(AppDbContext db)
    {
        var patient = new Patient
        {
            PatientNumber = "P-3D-001",
            FirstName = "Private",
            LastName = "Patient",
            IsActive = true
        };
        db.Patients.Add(patient);

        var orthoCase = new OrthoCase
        {
            PatientId = patient.Id,
            CaseNumber = "OC-3D-001",
            ApplianceType = "Fixed",
            CurrentStage = "Pre-surgical orthodontics",
            IsActive = true
        };
        db.OrthoCases.Add(orthoCase);

        var ceph = new CephAnalysis
        {
            OrthoCaseId = orthoCase.Id,
            AnalysisType = "Steiner",
            IsApproved = true,
            IsActive = true
        };
        db.CephAnalyses.Add(ceph);
        await db.SaveChangesAsync();

        db.CephMeasurements.AddRange(
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "SNA", MeasurementValue = 80m, Unit = "deg", IsActive = true },
            new CephMeasurement { AnalysisId = ceph.Id, MeasurementName = "SNB", MeasurementValue = 76m, Unit = "deg", IsActive = true });

        var surgicalCase = new OrthoSurgicalCase
        {
            CaseNumber = "OS-3D-001",
            PatientId = patient.Id,
            OrthoCaseId = orthoCase.Id,
            CephAnalysisId = ceph.Id,
            Status = OrthoSurgicalStatus.JointPlanApproved,
            DiagnosisSummary = "Class III surgical planning",
            IsActive = true
        };
        db.OrthoSurgicalCases.Add(surgicalCase);
        await db.SaveChangesAsync();

        db.JointPlans.Add(new JointPlan
        {
            OrthoSurgicalCaseId = surgicalCase.Id,
            ProcedureType = "Le Fort I + BSSO",
            SurgicalObjectives = "Correct skeletal discrepancy",
            IsActive = true
        });
        db.OrthoSurgicalVtos.Add(new OrthoSurgicalVto
        {
            OrthoSurgicalCaseId = surgicalCase.Id,
            CephAnalysisId = ceph.Id,
            MaxillaMoveMm = 3m,
            MandibleMoveMm = -2m,
            PredictedANB = 2m,
            IsApprovedByOrthodontist = true,
            IsActive = true
        });
        db.Radiographs.AddRange(
            new Radiograph
            {
                PatientId = patient.Id,
                OrthoCaseId = orthoCase.Id,
                XrayType = "CBCT",
                FileName = "case.cbct.dcm",
                FileUrl = "/uploads/cbct/case.dcm",
                IsActive = true
            },
            new Radiograph
            {
                PatientId = patient.Id,
                OrthoCaseId = orthoCase.Id,
                XrayType = "OPG",
                FileName = "pan.jpg",
                FileUrl = "/uploads/pan.jpg",
                IsActive = true
            });
        db.Documents.AddRange(
            new Document
            {
                PatientId = patient.Id,
                OrthoCaseId = orthoCase.Id,
                DocumentType = "3d_model",
                Title = "Upper and lower STL",
                FileName = "models.stl",
                FileUrl = "/uploads/models.stl",
                IsActive = true
            },
            new Document
            {
                PatientId = patient.Id,
                OrthoCaseId = orthoCase.Id,
                DocumentType = "consent",
                Title = "Consent",
                FileName = "consent.pdf",
                FileUrl = "/uploads/consent.pdf",
                IsActive = true
            });

        await db.SaveChangesAsync();
        return surgicalCase;
    }

    [Fact]
    public async Task ExportPackage_ReturnsReadOnlyManifest_WithoutPatientName()
    {
        await using var db = CreateDb();
        var surgicalCase = await SeedExportCaseAsync(db);
        var controller = Build(db);

        var result = await controller.GetExportPackage(surgicalCase.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(ok.Value);

        json.Should().Contain("aqlan-ortho-surgical-export-v1");
        json.Should().Contain("P-3D-001");
        json.Should().Contain("noSegmentation");
        json.Should().NotContain("Private");
        json.Should().NotContain("Patient\"");
    }

    [Fact]
    public async Task ExportPackage_IncludesOnlyCbctAndThreeDModelAssets()
    {
        await using var db = CreateDb();
        var surgicalCase = await SeedExportCaseAsync(db);
        var controller = Build(db);

        var result = await controller.GetExportPackage(surgicalCase.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var assets = doc.RootElement.GetProperty("assets");

        var cbct = assets.GetProperty("cbctRadiographs").EnumerateArray().ToList();
        cbct.Should().ContainSingle();
        cbct[0].GetProperty("fileName").GetString().Should().Be("case.cbct.dcm");

        var models = assets.GetProperty("threeDModels").EnumerateArray().ToList();
        models.Should().ContainSingle();
        models[0].GetProperty("fileName").GetString().Should().Be("models.stl");
    }

    [Fact]
    public async Task ExportPackage_MissingCase_ReturnsNotFound()
    {
        await using var db = CreateDb();
        var controller = Build(db);

        var result = await controller.GetExportPackage(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
