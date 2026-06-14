using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Validators;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Data.Seed;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AqlanDentalPro.UnitTests.Ceph;

/// <summary>
/// Jarabak (Björk) analysis: the saddle/articular/gonial angles, their sum, and
/// the posterior/anterior facial-height ratio must be computed and persisted
/// from the same landmark set the rest of the engine uses. The ratio is a pure
/// length ratio (S-Go / N-Me) so it must work even on an uncalibrated image.
/// </summary>
public class CephJarabakAnalysisTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static CephService CreateService(AppDbContext db)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.UserId).Returns(Guid.NewGuid());
        user.Setup(u => u.IsAdmin).Returns(true);
        return new CephService(db, user.Object, new Mock<ILogger<CephService>>().Object);
    }

    // A plausible lateral-ceph layout (image coords: +y is down).
    private static readonly (string Key, double X, double Y)[] Landmarks =
    [
        ("S",  100, 100),
        ("N",  220, 90),
        ("Ar", 90,  150),
        ("Go", 110, 270),
        ("Me", 185, 320),
    ];

    private static double Angle(
        (double x, double y) p1, (double x, double y) v, (double x, double y) p3)
    {
        double ax = p1.x - v.x, ay = p1.y - v.y;
        double bx = p3.x - v.x, by = p3.y - v.y;
        double dot = ax * bx + ay * by;
        double mag = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
        return Math.Acos(Math.Clamp(dot / mag, -1, 1)) * 180.0 / Math.PI;
    }

    private static double Dist((double x, double y) a, (double x, double y) b)
        => Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.y - a.y) * (b.y - a.y));

    private static async Task<Guid> SeedAndComputeAsync(AppDbContext db, string analysisType, double pixelsPerMm)
    {
        var orthoCase = new OrthoCase { Id = Guid.NewGuid(), PatientId = Guid.NewGuid(), CaseNumber = "OC-J", IsActive = true };
        var analysis = new CephAnalysis
        {
            Id = Guid.NewGuid(), OrthoCaseId = orthoCase.Id,
            AnalysisDate = DateOnly.FromDateTime(DateTime.Today), AnalysisType = analysisType, IsActive = true,
        };
        db.AddRange(orthoCase, analysis);
        await db.SaveChangesAsync();

        await CreateService(db).SaveLandmarksAsync(analysis.Id, new SaveLandmarksRequest
        {
            PixelsPerMm = pixelsPerMm,
            ImageWidth = 800,
            ImageHeight = 600,
            Landmarks = Landmarks
                .Select(l => new LandmarkInput { Key = l.Key, Name = l.Key, X = l.X, Y = l.Y })
                .ToList(),
        });
        return analysis.Id;
    }

    private static (double x, double y) P(string key)
    {
        var l = Landmarks.Single(l => l.Key == key);
        return (l.X, l.Y);
    }

    [Fact]
    public async Task Jarabak_ComputesAllFivePolygonMeasurements()
    {
        await using var db = CreateDb();
        var id = await SeedAndComputeAsync(db, "jarabak", pixelsPerMm: 3.0);

        var names = await db.CephMeasurements
            .Where(m => m.AnalysisId == id && m.IsActive)
            .Select(m => m.MeasurementName)
            .ToListAsync();

        names.Should().Contain(["Saddle-Angle", "Articular-Angle", "Gonial-Angle", "Bjork-Sum", "Jarabak-Ratio"]);
        names.Should().OnlyContain(n =>
            n == "Saddle-Angle" || n == "Articular-Angle" || n == "Gonial-Angle"
            || n == "Bjork-Sum" || n == "Jarabak-Ratio");
    }

    [Fact]
    public async Task Jarabak_AngleValuesMatchGeometry_AndSumIsConsistent()
    {
        await using var db = CreateDb();
        var id = await SeedAndComputeAsync(db, "jarabak", pixelsPerMm: 3.0);

        var ms = await db.CephMeasurements
            .Where(m => m.AnalysisId == id && m.IsActive)
            .ToDictionaryAsync(m => m.MeasurementName, m => (double)m.MeasurementValue!.Value);

        ms["Saddle-Angle"].Should().BeApproximately(Angle(P("N"), P("S"), P("Ar")), 0.2);
        ms["Articular-Angle"].Should().BeApproximately(Angle(P("S"), P("Ar"), P("Go")), 0.2);
        ms["Gonial-Angle"].Should().BeApproximately(Angle(P("Ar"), P("Go"), P("Me")), 0.2);
        ms["Bjork-Sum"].Should().BeApproximately(
            ms["Saddle-Angle"] + ms["Articular-Angle"] + ms["Gonial-Angle"], 0.2);
    }

    [Fact]
    public async Task Jarabak_RatioIsPercent_AndIndependentOfCalibration()
    {
        await using var db = CreateDb();
        // pixelsPerMm = 0 → image is NOT calibrated. The ratio must still compute.
        var id = await SeedAndComputeAsync(db, "jarabak", pixelsPerMm: 0);

        var ratio = await db.CephMeasurements
            .SingleAsync(m => m.AnalysisId == id && m.MeasurementName == "Jarabak-Ratio" && m.IsActive);

        var expected = Dist(P("S"), P("Go")) / Dist(P("N"), P("Me")) * 100;
        ((double)ratio.MeasurementValue!.Value).Should().BeApproximately(expected, 0.2);
        ratio.Unit.Should().Be("%");
    }

    [Fact]
    public async Task FullAnalysis_IncludesJarabakGroup()
    {
        await using var db = CreateDb();
        var id = await SeedAndComputeAsync(db, "full", pixelsPerMm: 3.0);

        var hasJarabak = await db.CephMeasurements
            .AnyAsync(m => m.AnalysisId == id && m.IsActive && m.MeasurementName == "Bjork-Sum");

        hasJarabak.Should().BeTrue("the 'full' analysis must run Jarabak alongside the other six analyses");
    }

    // ─── Uncalibrated save path (Codex review) ───────────────────────────────

    [Fact]
    public void SaveLandmarksValidator_AcceptsUncalibratedZeroPixelsPerMm()
    {
        var req = new SaveLandmarksRequest
        {
            PixelsPerMm = 0, ImageWidth = 800, ImageHeight = 600,
            Landmarks = [new LandmarkInput { Key = "S", X = 1, Y = 1 }],
        };
        new SaveLandmarksRequestValidator().Validate(req).IsValid
            .Should().BeTrue("scale-independent analyses (Jarabak) must be savable without calibration");
    }

    [Fact]
    public void SaveLandmarksValidator_RejectsNegativePixelsPerMm()
    {
        var req = new SaveLandmarksRequest
        {
            PixelsPerMm = -1, ImageWidth = 800, ImageHeight = 600,
            Landmarks = [new LandmarkInput { Key = "S", X = 1, Y = 1 }],
        };
        new SaveLandmarksRequestValidator().Validate(req).IsValid.Should().BeFalse();
    }

    // ─── Norm backfill for already-seeded clinics (Codex review) ─────────────

    [Fact]
    public async Task Backfill_InsertsMissingJarabakNorms_WithoutTouchingExistingRows()
    {
        await using var db = CreateDb();
        // Simulate a clinic seeded before Jarabak existed, with one customized row.
        db.CephNorms.Add(new CephNorm
        {
            MeasurementName = "SNA", AnalysisGroup = "steiner", NameAr = "مخصص",
            NormalValue = 79, StdDeviation = 3, Unit = "°", IsActive = true,
        });
        await db.SaveChangesAsync();

        var inserted = await CephNormSeeder.BackfillMissingDefaultsAsync(db);

        inserted.Should().BeGreaterThan(0);
        (await db.CephNorms.AnyAsync(n => n.MeasurementName == "Bjork-Sum" && n.AnalysisGroup == "jarabak"))
            .Should().BeTrue("missing Jarabak norms must be backfilled");
        (await db.CephNorms.AnyAsync(n => n.MeasurementName == "Jarabak-Ratio" && n.AnalysisGroup == "jarabak"))
            .Should().BeTrue();

        var sna = await db.CephNorms.SingleAsync(n => n.MeasurementName == "SNA");
        sna.NameAr.Should().Be("مخصص", "existing customized rows must not be overwritten");
        sna.NormalValue.Should().Be(79);
    }

    [Fact]
    public async Task Backfill_IsIdempotent_SecondRunInsertsNothing()
    {
        await using var db = CreateDb();
        await CephNormSeeder.SeedIfEmptyAsync(db);

        var second = await CephNormSeeder.BackfillMissingDefaultsAsync(db);

        second.Should().Be(0, "a fully seeded table has no missing factory rows");
    }
}
