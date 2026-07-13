using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.API.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

public class CephPaAnalysisTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static CephService CreateService(AppDbContext db)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(x => x.UserId).Returns(Guid.NewGuid());
        user.Setup(x => x.IsAdmin).Returns(true);
        return new CephService(db, user.Object, new Mock<ILogger<CephService>>().Object);
    }

    private static readonly (string Key, double X, double Y)[] Points =
    [
        ("ZR", 0, 0), ("ZL", 100, 10), ("Cg", 50, 5),
        ("ANS", 60, 6), ("JR", 20, 22), ("JL", 80, 38),
        ("AgR", 10, 50), ("AgL", 90, 70), ("Me", 55, 80),
        ("UDM", 52, 40), ("LDM", 48, 55),
        ("U6R", 25, 30), ("U6L", 75, 40),
        ("L6R", 20, 48), ("L6L", 80, 62),
    ];

    private static async Task<Guid> SeedAndCompute(AppDbContext db, double pixelsPerMm)
    {
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), CaseNumber = "PA-1", IsActive = true };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id, AnalysisType = "pa",
            AnalysisDate = new DateOnly(2026, 7, 13), IsActive = true,
        };
        db.AddRange(orthoCase, analysis);
        await db.SaveChangesAsync();
        await CreateService(db).SaveLandmarksAsync(analysis.Id, new SaveLandmarksRequest
        {
            PixelsPerMm = pixelsPerMm,
            ImageWidth = 1000,
            ImageHeight = 1200,
            Landmarks = Points.Select(p => new LandmarkInput { Key = p.Key, Name = p.Key, X = p.X, Y = p.Y }).ToList(),
        });
        return analysis.Id;
    }

    [Fact]
    public async Task ComputesWidthsMidlinesCantsAndBilateralAsymmetryFromTiltedReference()
    {
        await using var db = CreateDb();
        var id = await SeedAndCompute(db, 10);
        var rows = await db.CephMeasurements.Where(x => x.AnalysisId == id && x.IsActive)
            .ToDictionaryAsync(x => x.MeasurementName);

        rows.Should().HaveCount(11);
        ((double)rows["PA-ANS-MSR"].MeasurementValue!.Value).Should().BeApproximately(1.0, 0.05);
        ((double)rows["PA-JJ-Width"].MeasurementValue!.Value).Should().BeApproximately(6.2, 0.05);
        ((double)rows["PA-J-Vertical-Asymmetry"].MeasurementValue!.Value).Should().BeApproximately(1.0, 0.05);
        ((double)rows["PA-Maxillary-Cant"].MeasurementValue!.Value).Should().BeApproximately(5.6, 0.05);
        rows.Values.Should().OnlyContain(x => x.NormalValue == null && x.Classification == "unclassified",
            "PA values are descriptive unless an age/sex-specific norm is explicitly configured");
        var diagnosis = await db.CephDiagnoses.SingleAsync(x => x.AnalysisId == id && x.IsActive);
        diagnosis.SkeletalClass.Should().BeNull();
        diagnosis.VerticalPattern.Should().BeNull();
        diagnosis.AiRecommendation.Should().Contain("وصفي").And.Contain("يسار المريض");
    }

    [Fact]
    public async Task WithoutCalibrationKeepsAnglesAndOmitsEveryLinearMeasurement()
    {
        await using var db = CreateDb();
        var id = await SeedAndCompute(db, 0);
        var rows = await db.CephMeasurements.Where(x => x.AnalysisId == id && x.IsActive).ToListAsync();

        rows.Select(x => x.MeasurementName).Should().BeEquivalentTo(["PA-Maxillary-Cant", "PA-Mandibular-Cant"]);
        rows.Should().OnlyContain(x => x.Unit == "°");
    }

    [Fact]
    public async Task PaAnalysisDoesNotRunLateralMeasurementGroups()
    {
        await using var db = CreateDb();
        var id = await SeedAndCompute(db, 10);
        var names = await db.CephMeasurements.Where(x => x.AnalysisId == id && x.IsActive)
            .Select(x => x.MeasurementName).ToListAsync();

        names.Should().OnlyContain(x => x.StartsWith("PA-"));
    }

    [Fact]
    public void PdfOverlayContainsPaReferenceWidthsAndOcclusalPlanes()
    {
        var landmarks = Points.Select(p => new CephLandmark
        {
            LandmarkKey = p.Key, XCoord = (decimal)p.X, YCoord = (decimal)p.Y, IsActive = true,
        }).ToList();

        var svg = CephReportPdfGenerator.BuildOverlaySvg(landmarks, 1000, 1200);

        svg.Should().Contain("#C4B5FD").And.Contain("#FCD34D")
            .And.Contain("#FB923C").And.Contain("#2DD4BF").And.Contain("#14B8A6");
        svg.Should().Contain(">ZR</text>").And.Contain(">Me</text>");
    }
}
